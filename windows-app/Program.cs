using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DastavchikRobot
{
    public class MainForm : Form
    {
        private ComboBox cmbPorts;
        private Button btnRefresh, btnConnect, btnSim, btnMode, btnStop, btnOpenWeb, btnOpenDocs;
        private TrackBar trkSpeed;
        private Label lblSpeed, lblStatus, lblCmd, lblSF, lblSR, lblSB, lblSL, lblBlk;
        private SerialPort serial;
        private System.Windows.Forms.Timer sendTimer, uiTimer;
        private int vx = 0, vy = 0, vw = 0;
        private bool simMode = false;
        private char robotMode = 'T';
        private int[] dist = new int[] { 999, 999, 999, 999 };
        private bool blocked = false;
        private ConcurrentQueue<string> webReadQueue = new ConcurrentQueue<string>();
        private int httpPort = 0;
        private HttpListener listener;

        public MainForm()
        {
            Text = "QALQON — Robot-Patrul | Navoiy viloyati IIB Situatsion Markazi";
            Size = new Size(760, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(13, 17, 23);
            ForeColor = Color.FromArgb(230, 237, 243);
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            StartLocalServer();
            BuildUi();
            RefreshPorts();

            sendTimer = new System.Windows.Forms.Timer { Interval = 150 };
            sendTimer.Tick += (s, e) => SendDriveTick();
            sendTimer.Start();

            uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
            uiTimer.Tick += (s, e) => UpdateTelemetryUi();
            uiTimer.Start();

            Shown += (s, e) => OpenAppWindow("/index.html");
        }

        private void BuildUi()
        {
            var lblTitle = new Label
            {
                Text = "🚔 QALQON — ROBOT-PATRUL (Navoiy IIB)",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                Location = new Point(16, 14),
                AutoSize = true
            };
            lblStatus = new Label
            {
                Text = "ulanmagan",
                BackColor = Color.FromArgb(33, 38, 45),
                ForeColor = Color.FromArgb(139, 148, 158),
                Location = new Point(420, 15),
                Size = new Size(160, 26),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lblTitle);
            Controls.Add(lblStatus);

            var cardLeft = CreateCard(16, 52, 360, 410);
            cmbPorts = new ComboBox
            {
                Location = new Point(12, 14),
                Size = new Size(110, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(33, 38, 45),
                ForeColor = Color.White
            };
            btnRefresh = CreateBtn("🔄", 128, 12, 42, 32, Color.FromArgb(33, 38, 45));
            btnRefresh.Click += (s, e) => RefreshPorts();

            btnConnect = CreateBtn("COM ulanish", 176, 12, 170, 32, Color.FromArgb(47, 129, 247));
            btnConnect.Click += (s, e) => ToggleSerial();

            btnSim = CreateBtn("3D Patrul Simulyator", 12, 52, 158, 34, Color.FromArgb(33, 38, 45));
            btnSim.Click += (s, e) => ToggleSim();

            btnMode = CreateBtn("Rejim: Operator", 176, 52, 110, 34, Color.FromArgb(33, 38, 45));
            btnMode.Click += (s, e) =>
            {
                robotMode = robotMode == 'T' ? 'A' : 'T';
                SendLine("M " + robotMode);
            };

            btnStop = CreateBtn("STOP", 292, 52, 54, 34, Color.FromArgb(248, 81, 73));
            btnStop.Click += (s, e) => { vx = vy = vw = 0; SendLine("S"); SendLine("M T"); robotMode = 'T'; };

            lblSpeed = new Label { Text = "Tezlik: 165", Location = new Point(12, 96), AutoSize = true, ForeColor = Color.FromArgb(139, 148, 158) };
            trkSpeed = new TrackBar { Minimum = 80, Maximum = 255, Value = 165, Location = new Point(100, 90), Size = new Size(240, 36), TickStyle = TickStyle.None };
            trkSpeed.Scroll += (s, e) => lblSpeed.Text = "Tezlik: " + trkSpeed.Value;

            cardLeft.Controls.AddRange(new Control[] { cmbPorts, btnRefresh, btnConnect, btnSim, btnMode, btnStop, lblSpeed, trkSpeed });

            var dirs = new Tuple<string, int, int, int>[]
            {
                Tuple.Create("↖", 1, -1, 0), Tuple.Create("▲", 1, 0, 0), Tuple.Create("↗", 1, 1, 0),
                Tuple.Create("◀", 0, -1, 0), Tuple.Create("■", 0, 0, 0), Tuple.Create("▶", 0, 1, 0),
                Tuple.Create("↙", -1, -1, 0), Tuple.Create("▼", -1, 0, 0), Tuple.Create("↘", -1, 1, 0),
                Tuple.Create("⟲", 0, 0, -1), null, Tuple.Create("⟳", 0, 0, 1)
            };

            for (int i = 0; i < dirs.Length; i++)
            {
                if (dirs[i] == null) continue;
                int r = i / 3, c = i % 3;
                var b = CreateBtn(dirs[i].Item1, 12 + c * 114, 134 + r * 60, 106, 54, Color.FromArgb(33, 38, 45));
                b.Font = new Font("Segoe UI Symbol", 16f, FontStyle.Bold);
                int dx = dirs[i].Item2, dy = dirs[i].Item3, dw = dirs[i].Item4;
                b.MouseDown += (s, e) => { vx = dx; vy = dy; vw = dw; };
                b.MouseUp += (s, e) => { vx = 0; vy = 0; vw = 0; };
                cardLeft.Controls.Add(b);
            }

            lblCmd = new Label { Text = "Buyruq: V 0 0 0", Location = new Point(12, 380), AutoSize = true, ForeColor = Color.FromArgb(139, 148, 158) };
            cardLeft.Controls.Add(lblCmd);
            Controls.Add(cardLeft);

            var cardRight = CreateCard(390, 52, 340, 410);
            var lblSensTitle = new Label { Text = "Perimetr Sensorlari (sm)", Font = new Font("Segoe UI", 11f, FontStyle.Bold), Location = new Point(14, 12), AutoSize = true };
            cardRight.Controls.Add(lblSensTitle);

            lblSF = CreateSensorBox(122, 44);
            lblSL = CreateSensorBox(14, 96);
            lblBlk = CreateSensorBox(122, 96); lblBlk.Text = "✓";
            lblSR = CreateSensorBox(230, 96);
            lblSB = CreateSensorBox(122, 148);
            cardRight.Controls.AddRange(new Control[] { lblSF, lblSL, lblBlk, lblSR, lblSB });

            var lblHint = new Label
            {
                Text = "«QALQON» IIB Situatsion Markazi, 3D Patrul Xaritasi, SOS, Kamera va Muhandislik Chizmalari:",
                Location = new Point(14, 212),
                Size = new Size(312, 42),
                ForeColor = Color.FromArgb(139, 148, 158)
            };
            btnOpenWeb = CreateBtn("🚔 «QALQON» Situatsion Markazni Ochish", 14, 260, 312, 44, Color.FromArgb(47, 129, 247));
            btnOpenWeb.Click += (s, e) => OpenAppWindow("/index.html");

            btnOpenDocs = CreateBtn("🚨 Mayoqcha / Sirena (Pin 13)", 14, 314, 312, 40, Color.FromArgb(210, 50, 60));
            bool beaconState = false;
            btnOpenDocs.Click += (s, e) =>
            {
                beaconState = !beaconState;
                SendLine(beaconState ? "L 1" : "L 0");
                btnOpenDocs.Text = beaconState ? "🚨 Mayoqcha: YOQIQ (O'chirish)" : "🚨 Mayoqcha / Sirena (Pin 13)";
            };

            var lblServerInfo = new Label
            {
                Text = "Situatsion Markaz Serveri: http://127.0.0.1:" + httpPort,
                Location = new Point(14, 368),
                AutoSize = true,
                ForeColor = Color.FromArgb(46, 160, 67)
            };

            cardRight.Controls.AddRange(new Control[] { lblHint, btnOpenWeb, btnOpenDocs, lblServerInfo });
            Controls.Add(cardRight);
        }

        private Panel CreateCard(int x, int y, int w, int h)
        {
            return new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.FromArgb(22, 27, 34) };
        }

        private Button CreateBtn(string text, int x, int y, int w, int h, Color bg)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private Label CreateSensorBox(int x, int y)
        {
            return new Label
            {
                Text = "–",
                Location = new Point(x, y),
                Size = new Size(96, 44),
                BackColor = Color.FromArgb(33, 38, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private void RefreshPorts()
        {
            cmbPorts.Items.Clear();
            foreach (var p in SerialPort.GetPortNames()) cmbPorts.Items.Add(p);
            if (cmbPorts.Items.Count > 0) cmbPorts.SelectedIndex = 0;
        }

        private void ToggleSerial()
        {
            if (serial != null && serial.IsOpen)
            {
                try { serial.Close(); } catch { }
                serial = null;
                btnConnect.Text = "COM ulanish";
                lblStatus.Text = "uzildi";
                lblStatus.BackColor = Color.FromArgb(33, 38, 45);
                lblStatus.ForeColor = Color.FromArgb(139, 148, 158);
                return;
            }
            if (cmbPorts.SelectedItem == null)
            {
                MessageBox.Show("COM port topilmadi. USB kabelni tekshiring yoki '3D Patrul Simulyator' tugmasini bosing.", "Xabar");
                return;
            }
            try
            {
                simMode = false;
                serial = new SerialPort(cmbPorts.SelectedItem.ToString(), 115200) { NewLine = "\n", DtrEnable = true, RtsEnable = true };
                serial.DataReceived += OnSerialData;
                serial.Open();
                btnConnect.Text = "Uzish (" + serial.PortName + ")";
                lblStatus.Text = "ulangan (" + serial.PortName + ")";
                lblStatus.BackColor = Color.FromArgb(46, 160, 67);
                lblStatus.ForeColor = Color.White;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ulanib bo'lmadi: " + ex.Message, "Xato");
            }
        }

        private void ToggleSim()
        {
            simMode = !simMode;
            if (simMode)
            {
                dist = new int[] { 120, 95, 150, 80 };
                lblStatus.Text = "simulyator (test)";
                lblStatus.BackColor = Color.FromArgb(210, 153, 34);
                lblStatus.ForeColor = Color.Black;
            }
            else
            {
                lblStatus.Text = "ulanmagan";
                lblStatus.BackColor = Color.FromArgb(33, 38, 45);
                lblStatus.ForeColor = Color.FromArgb(139, 148, 158);
            }
        }

        private void OnSerialData(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string chunk = serial.ReadExisting();
                webReadQueue.Enqueue(chunk);
                foreach (var raw in chunk.Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("D "))
                    {
                        var p = line.Split(' ');
                        if (p.Length >= 7)
                        {
                            int.TryParse(p[1], out dist[0]);
                            int.TryParse(p[2], out dist[1]);
                            int.TryParse(p[3], out dist[2]);
                            int.TryParse(p[4], out dist[3]);
                            robotMode = p[5][0];
                            blocked = p[6] == "1";
                        }
                    }
                }
            }
            catch { }
        }

        private void SendDriveTick()
        {
            int s = trkSpeed.Value;
            string line = string.Format("V {0} {1} {2}", vx * s, vy * s, (int)(vw * s * 0.7));
            lblCmd.Text = "Buyruq: " + line;
            SendLine(line);
            if (simMode)
            {
                string tele = string.Format("D {0} {1} {2} {3} {4} {5}\n", dist[0], dist[1], dist[2], dist[3], robotMode, blocked ? 1 : 0);
                webReadQueue.Enqueue(tele);
            }
        }

        private void SendLine(string cmd)
        {
            if (serial != null && serial.IsOpen)
            {
                try { serial.Write(cmd + "\n"); } catch { }
            }
        }

        private void UpdateTelemetryUi()
        {
            if (!simMode && (serial == null || !serial.IsOpen)) return;
            UpdateSensorLabel(lblSF, dist[0]);
            UpdateSensorLabel(lblSR, dist[1]);
            UpdateSensorLabel(lblSB, dist[2]);
            UpdateSensorLabel(lblSL, dist[3]);
            lblBlk.Text = blocked ? "⛔" : "✓";
            btnMode.Text = "Rejim: " + (robotMode == 'A' ? "Avtonom" : "Operator");
        }

        private void UpdateSensorLabel(Label lbl, int d)
        {
            lbl.Text = d >= 999 ? "—" : d.ToString();
            lbl.BackColor = d < 15 ? Color.FromArgb(248, 81, 73) : Color.FromArgb(33, 38, 45);
        }

        private void StartLocalServer()
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                httpPort = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();

                listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + httpPort + "/");
                listener.Start();
                var th = new Thread(HttpLoop) { IsBackground = true };
                th.Start();
            }
            catch { }
        }

        private void HttpLoop()
        {
            while (listener != null && listener.IsListening)
            {
                try
                {
                    var ctx = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleHttp(ctx));
                }
                catch { break; }
            }
        }

        private void HandleHttp(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath;
                if (path == "/api/connect")
                {
                    bool ok = (serial != null && serial.IsOpen) || simMode;
                    if (!ok)
                    {
                        Invoke(new Action(() =>
                        {
                            if (cmbPorts.Items.Count > 0) ToggleSerial();
                            else ToggleSim();
                        }));
                        ok = true;
                    }
                    WriteResp(ctx, "application/json", ok ? "{\"ok\":true}" : "{\"ok\":false}");
                    return;
                }
                if (path == "/api/read")
                {
                    var sb = new StringBuilder();
                    string item;
                    while (webReadQueue.TryDequeue(out item)) sb.Append(item);
                    WriteResp(ctx, "text/plain", sb.ToString());
                    return;
                }
                if (path == "/api/write")
                {
                    using (var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    {
                        string body = sr.ReadToEnd().Trim();
                        if (!string.IsNullOrEmpty(body)) SendLine(body);
                    }
                    WriteResp(ctx, "text/plain", "OK");
                    return;
                }

                if (path == "/") path = "/index.html";
                string resName = path.TrimStart('/').Replace('/', '.');
                string mime = "text/html; charset=utf-8";
                if (path.EndsWith(".js")) mime = "application/javascript; charset=utf-8";
                else if (path.EndsWith(".jpg") || path.EndsWith(".jpeg")) mime = "image/jpeg";

                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (s == null)
                    {
                        ctx.Response.StatusCode = 404;
                        ctx.Response.Close();
                        return;
                    }
                    ctx.Response.ContentType = mime;
                    s.CopyTo(ctx.Response.OutputStream);
                    ctx.Response.Close();
                }
            }
            catch { }
        }

        private void WriteResp(HttpListenerContext ctx, string mime, string text)
        {
            byte[] b = Encoding.UTF8.GetBytes(text);
            ctx.Response.ContentType = mime;
            ctx.Response.OutputStream.Write(b, 0, b.Length);
            ctx.Response.Close();
        }

        private void OpenAppWindow(string subPath)
        {
            string url = "http://127.0.0.1:" + httpPort + subPath;
            string[] browsers = new string[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe"
            };
            foreach (var br in browsers)
            {
                if (File.Exists(br))
                {
                    Process.Start(br, "--app=" + url + " --window-size=1280,820");
                    return;
                }
            }
            Process.Start(url);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { if (listener != null) listener.Stop(); } catch { }
            try { if (serial != null && serial.IsOpen) serial.Close(); } catch { }
            base.OnFormClosing(e);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
