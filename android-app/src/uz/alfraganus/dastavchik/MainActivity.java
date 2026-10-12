package uz.alfraganus.dastavchik;

import android.Manifest;
import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.hardware.usb.UsbConstants;
import android.hardware.usb.UsbDevice;
import android.hardware.usb.UsbDeviceConnection;
import android.hardware.usb.UsbEndpoint;
import android.hardware.usb.UsbInterface;
import android.hardware.usb.UsbManager;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.webkit.JavascriptInterface;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;

import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;

public class MainActivity extends Activity {
    private static final String ACTION_USB_PERMISSION = "uz.alfraganus.dastavchik.USB_PERMISSION";
    private static final String VIRTUAL_HOST = "appassets.androidplatform.net";
    private WebView webView;
    private UsbManager usbManager;
    private UsbDeviceConnection usbConn;
    private UsbEndpoint epIn, epOut;
    private volatile boolean reading = false;
    private int currentBaud = 115200;

    private final BroadcastReceiver usbReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            String action = intent.getAction();
            if (ACTION_USB_PERMISSION.equals(action)) {
                UsbDevice dev = intent.getParcelableExtra(UsbManager.EXTRA_DEVICE);
                if (intent.getBooleanExtra(UsbManager.EXTRA_PERMISSION_GRANTED, false) && dev != null) {
                    String res = openDevice(dev, currentBaud);
                    if ("OK".equals(res)) {
                        runJs("window.__onAndroidUsbConnected && window.__onAndroidUsbConnected();");
                    } else {
                        runJs("window.__onAndroidUsbError && window.__onAndroidUsbError('" + res + "');");
                    }
                } else {
                    runJs("window.__onAndroidUsbError && window.__onAndroidUsbError('USB ruxsat berilmadi');");
                }
            } else if (UsbManager.ACTION_USB_DEVICE_DETACHED.equals(action)) {
                closeUsb();
                runJs("window.__onAndroidUsbDisconnected && window.__onAndroidUsbDisconnected();");
            }
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        usbManager = (UsbManager) getSystemService(Context.USB_SERVICE);

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            if (checkSelfPermission(Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
                requestPermissions(new String[]{Manifest.permission.CAMERA}, 101);
            }
        }

        IntentFilter filter = new IntentFilter(ACTION_USB_PERMISSION);
        filter.addAction(UsbManager.ACTION_USB_DEVICE_DETACHED);
        if (Build.VERSION.SDK_INT >= 33) {
            registerReceiver(usbReceiver, filter, Context.RECEIVER_NOT_EXPORTED);
        } else {
            registerReceiver(usbReceiver, filter);
        }

        webView = new WebView(this);
        WebSettings ws = webView.getSettings();
        ws.setJavaScriptEnabled(true);
        ws.setDomStorageEnabled(true);
        ws.setDatabaseEnabled(true);
        ws.setMediaPlaybackRequiresUserGesture(false);
        ws.setAllowFileAccess(true);
        ws.setAllowContentAccess(true);
        ws.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                Uri uri = request.getUrl();
                if (uri != null && VIRTUAL_HOST.equals(uri.getHost())) {
                    String path = uri.getPath();
                    if (path == null || path.equals("/") || path.isEmpty()) {
                        path = "/index.html";
                    }
                    String assetPath = "planshet" + path;
                    try {
                        InputStream is = getAssets().open(assetPath);
                        String mime = "text/html";
                        if (path.endsWith(".js")) mime = "application/javascript";
                        else if (path.endsWith(".css")) mime = "text/css";
                        else if (path.endsWith(".png")) mime = "image/png";
                        return new WebResourceResponse(mime, "UTF-8", is);
                    } catch (Exception e) {
                        return new WebResourceResponse("text/plain", "UTF-8", null);
                    }
                }
                return super.shouldInterceptRequest(view, request);
            }
        });

        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onPermissionRequest(final PermissionRequest request) {
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        request.grant(request.getResources());
                    }
                });
            }
        });

        webView.addJavascriptInterface(new UsbBridge(), "AndroidUsb");
        setContentView(webView);
        webView.loadUrl("https://" + VIRTUAL_HOST + "/index.html");
    }

    @Override
    public void onBackPressed() {
        if (webView != null && webView.canGoBack()) {
            webView.goBack();
        } else {
            super.onBackPressed();
        }
    }

    private void runJs(final String js) {
        if (webView != null) {
            webView.post(new Runnable() {
                @Override
                public void run() {
                    webView.evaluateJavascript(js, null);
                }
            });
        }
    }

    private synchronized String openDevice(UsbDevice dev, int baud) {
        closeUsb();
        UsbInterface dataIf = null;
        UsbInterface ctrlIf = null;
        for (int i = 0; i < dev.getInterfaceCount(); i++) {
            UsbInterface intf = dev.getInterface(i);
            if (intf.getInterfaceClass() == UsbConstants.USB_CLASS_COMM) ctrlIf = intf;
            UsbEndpoint in = null, out = null;
            for (int j = 0; j < intf.getEndpointCount(); j++) {
                UsbEndpoint ep = intf.getEndpoint(j);
                if (ep.getType() == UsbConstants.USB_ENDPOINT_XFER_BULK) {
                    if (ep.getDirection() == UsbConstants.USB_DIR_IN) in = ep;
                    else out = ep;
                }
            }
            if (in != null && out != null) {
                dataIf = intf;
                epIn = in;
                epOut = out;
            }
        }
        if (dataIf == null) return "USB bulk interfeys topilmadi";
        usbConn = usbManager.openDevice(dev);
        if (usbConn == null) return "USB qurilmani ochib bolmadi";
        if (ctrlIf != null && ctrlIf != dataIf) usbConn.claimInterface(ctrlIf, true);
        usbConn.claimInterface(dataIf, true);

        if (dev.getVendorId() == 0x1A86) {
            usbConn.controlTransfer(0xC0, 0x5F, 0, 0, new byte[8], 8, 500);
            usbConn.controlTransfer(0x40, 0xA1, 0, 0, null, 0, 500);
            int factor = 1532620800 / baud;
            int divisor = 3;
            while (factor > 0xFFF0 && divisor > 0) { factor >>= 3; divisor--; }
            factor = 0x10000 - factor;
            divisor |= 0x80;
            usbConn.controlTransfer(0x40, 0x9A, 0x1312, (factor & 0xFF00) | divisor, null, 0, 500);
            usbConn.controlTransfer(0x40, 0x9A, 0x0F2C, factor & 0xFF, null, 0, 500);
            usbConn.controlTransfer(0x40, 0x9A, 0x2518, 0x00C3, null, 0, 500);
            usbConn.controlTransfer(0x40, 0xA4, ~((1 << 5) | (1 << 6)) & 0xFF, 0, null, 0, 500);
        } else {
            int ctrlIdx = ctrlIf != null ? ctrlIf.getId() : dataIf.getId();
            byte[] line = new byte[]{
                (byte) (baud & 0xFF), (byte) ((baud >> 8) & 0xFF),
                (byte) ((baud >> 16) & 0xFF), (byte) ((baud >> 24) & 0xFF),
                0, 0, 8
            };
            usbConn.controlTransfer(0x21, 0x20, 0, ctrlIdx, line, line.length, 500);
            usbConn.controlTransfer(0x21, 0x22, 0x03, ctrlIdx, null, 0, 500);
        }

        reading = true;
        new Thread(new Runnable() {
            @Override
            public void run() {
                byte[] buf = new byte[64];
                while (reading && usbConn != null) {
                    int n = usbConn.bulkTransfer(epIn, buf, buf.length, 200);
                    if (n > 0) {
                        String s = new String(buf, 0, n, StandardCharsets.UTF_8)
                                .replace("\\", "\\\\").replace("'", "\\'").replace("\r", "").replace("\n", "\\n");
                        runJs("window.__onAndroidUsbData && window.__onAndroidUsbData('" + s + "');");
                    }
                }
            }
        }).start();
        return "OK";
    }

    private synchronized void closeUsb() {
        reading = false;
        if (usbConn != null) {
            try { usbConn.close(); } catch (Exception ignored) {}
            usbConn = null;
        }
    }

    public class UsbBridge {
        @JavascriptInterface
        public String connect(int baud) {
            currentBaud = baud;
            HashMap<String, UsbDevice> map = usbManager.getDeviceList();
            if (map == null || map.isEmpty()) return "USB OTG orqali ulangan Arduino topilmadi";
            UsbDevice target = null;
            for (UsbDevice d : map.values()) {
                int vid = d.getVendorId();
                if (vid == 0x2341 || vid == 0x2A03 || vid == 0x1A86 || vid == 0x10C4 || vid == 0x0403) {
                    target = d;
                    break;
                }
                if (target == null) target = d;
            }
            if (target == null) return "Arduino topilmadi";
            if (!usbManager.hasPermission(target)) {
                Intent permIntent = new Intent(ACTION_USB_PERMISSION);
                permIntent.setPackage(getPackageName());
                int flags = Build.VERSION.SDK_INT >= 31 ? PendingIntent.FLAG_MUTABLE : 0;
                PendingIntent pi = PendingIntent.getBroadcast(MainActivity.this, 0, permIntent, flags);
                usbManager.requestPermission(target, pi);
                return "PENDING";
            }
            return openDevice(target, baud);
        }

        @JavascriptInterface
        public void write(String data) {
            UsbDeviceConnection c = usbConn;
            UsbEndpoint out = epOut;
            if (c != null && out != null && data != null) {
                byte[] b = data.getBytes(StandardCharsets.UTF_8);
                c.bulkTransfer(out, b, b.length, 200);
            }
        }
    }

    @Override
    protected void onDestroy() {
        closeUsb();
        try { unregisterReceiver(usbReceiver); } catch (Exception ignored) {}
        super.onDestroy();
    }
}
