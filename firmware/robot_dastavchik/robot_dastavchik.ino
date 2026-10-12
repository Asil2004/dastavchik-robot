/*
  Dastavchik (yetkazib beruvchi) robot — Arduino Uno + L293D Motor Shield (AFMotor)
  4 ta mecanum g'ildirak, 4 ta HC-SR04 ultratovush sensori.
  Planshet USB (OTG) orqali buyruq beradi; Arduino motorlarni boshqaradi va
  to'siqqa urilishdan himoya qiladi (planshet nima desa ham).

  Kerakli kutubxona: "Adafruit Motor Shield library" (AFMotor.h, V1 shield uchun)
  Arduino IDE -> Tools -> Manage Libraries -> "Adafruit Motor Shield library" (v1).

  Serial: 115200 bod, har bir buyruq '\n' bilan tugaydi.
    V vx vy w   harakat: vx=oldinga(+)/orqaga(-), vy=o'ngga(+)/chapga(-),
                w=soat yo'nalishida burilish(+). Har biri -255..255
    S           to'xtash
    M T         planshet rejimi (standart)
    M A         avtonom rejim (o'zi to'siqlardan qochib yuradi)
    P           tekshirish -> "OK" javobi
  Arduino har 100 ms da yuboradi:
    D old o'ng orqa chap rejim bloklangan   (masofalar sm da, 999 = hech narsa yo'q)
*/
#include <AFMotor.h>

// ---------- Motorlar: shield'dagi M1..M4 qaysi g'ildirakka ulangan ----------
// Agar g'ildirak teskari aylansa, tegishli INV qiymatini true qiling.
// (Sizning ulanishingiz bo'yicha: M1 old-chap, M2 orqa-chap, M3 old-o'ng, M4 orqa-o'ng)
AF_DCMotor mFL(1);  // old-chap
AF_DCMotor mBL(2);  // orqa-chap
AF_DCMotor mFR(3);  // old-o'ng
AF_DCMotor mBR(4);  // orqa-o'ng
const bool INV_FL = false, INV_FR = false, INV_BR = false, INV_BL = false;

// ---------- Ultratovush sensorlar (TRIG, ECHO) ----------
// L293D shield 3,4,5,6,7,8,11,12 pinlarini band qiladi. Bo'sh pinlar: A0-A5, 2, 9, 10, 13.
// Sizning ulanishingiz: Old A3/A2, O'ng 10/9, Orqa A5/A4, Chap A1/A0 (TRIG/ECHO)
const uint8_t TRIG[4] = {A3, 10, A5, A1};  // 0=old, 1=o'ng, 2=orqa, 3=chap
const uint8_t ECHO[4] = {A2, 9,  A4, A0};
const uint8_t ESTOP_PIN = 2;               // favqulodda to'xtatish tugmasi (GND ga), ixtiyoriy

// ---------- Sozlamalar ----------
const int STOP_CM = 15;         // shundan yaqin bo'lsa o'sha tomonga yurmaydi
const int SLOW_CM = 45;         // shundan yaqin bo'lsa sekinlashadi
const int MIN_PWM = 90;         // TT motor shundan past PWM da qimirlamaydi
const unsigned long WATCHDOG_MS = 600;  // planshetdan buyruq kelmasa to'xtaydi
const int AUTO_SPEED = 150;

enum { F = 0, R = 1, B = 2, L = 3 };
int dist[4] = {999, 999, 999, 999};
char mode = 'T';
int cmdVx = 0, cmdVy = 0, cmdW = 0;
unsigned long lastCmd = 0, lastTele = 0, lastPing = 0;
uint8_t sensorIdx = 0;
bool blocked = false;
char buf[40];
uint8_t bufLen = 0;

void setMotor(AF_DCMotor &m, int s, bool inv) {
  if (inv) s = -s;
  if (s == 0) { m.setSpeed(0); m.run(RELEASE); return; }
  int a = abs(s);
  a = MIN_PWM + (long)a * (255 - MIN_PWM) / 255;   // o'lik zonani o'tkazib yuborish
  m.setSpeed(a);
  m.run(s > 0 ? FORWARD : BACKWARD);
}

// Mecanum kinematikasi (roliklar "X" shaklida, yuqoridan qaralganda)
void drive(int vx, int vy, int w) {
  long fl = (long)vx + vy + w;
  long fr = (long)vx - vy - w;
  long bl = (long)vx - vy + w;
  long br = (long)vx + vy - w;
  long mx = max(max(abs(fl), abs(fr)), max(abs(bl), abs(br)));
  if (mx > 255) { fl = fl * 255 / mx; fr = fr * 255 / mx; bl = bl * 255 / mx; br = br * 255 / mx; }
  setMotor(mFL, fl, INV_FL);
  setMotor(mFR, fr, INV_FR);
  setMotor(mBL, bl, INV_BL);
  setMotor(mBR, br, INV_BR);
}

int readCm(uint8_t i) {
  digitalWrite(TRIG[i], LOW);  delayMicroseconds(2);
  digitalWrite(TRIG[i], HIGH); delayMicroseconds(10);
  digitalWrite(TRIG[i], LOW);
  unsigned long us = pulseIn(ECHO[i], HIGH, 12000UL);  // ~2 m gacha (MAX_DISTANCE 200 kabi)
  if (us == 0) return 999;
  return us / 58;
}

// Bitta o'q bo'yicha tezlikni to'siqqa qarab cheklash
int limitAxis(int v, int dPlus, int dMinus) {
  int d = v > 0 ? dPlus : dMinus;
  if (v == 0) return 0;
  if (d < STOP_CM) { blocked = true; return 0; }
  if (d < SLOW_CM) return v / 2;
  return v;
}

void applySafe(int vx, int vy, int w) {
  blocked = false;
  if (digitalRead(ESTOP_PIN) == LOW) { blocked = true; drive(0, 0, 0); return; }
  vx = limitAxis(vx, dist[F], dist[B]);
  vy = limitAxis(vy, dist[R], dist[L]);
  drive(vx, vy, w);  // joyida burilish ruxsat etiladi
}

// ---------- Avtonom rejim: to'siqlardan qochib yurish ----------
unsigned long autoUntil = 0;
int autoVx = 0, autoVy = 0, autoW = 0;
void autonomous() {
  unsigned long now = millis();
  if (now < autoUntil) { applySafe(autoVx, autoVy, autoW); return; }
  if (dist[F] > SLOW_CM) { autoVx = AUTO_SPEED; autoVy = 0; autoW = 0; autoUntil = now + 100; }
  else if (dist[R] > SLOW_CM || dist[L] > SLOW_CM) {
    // mecanum afzalligi: burilmasdan yonga siljiydi
    autoVx = 0; autoW = 0;
    autoVy = (dist[R] >= dist[L]) ? AUTO_SPEED : -AUTO_SPEED;
    autoUntil = now + 500;
  } else {
    // hamma tomon yopiq -> joyida 90° ga yaqin burilish
    autoVx = 0; autoVy = 0; autoW = AUTO_SPEED; autoUntil = now + 700;
  }
  applySafe(autoVx, autoVy, autoW);
}

void handleLine(char *s) {
  switch (s[0]) {
    case 'V': {
      int a = 0, b = 0, c = 0;
      if (sscanf(s + 1, "%d %d %d", &a, &b, &c) == 3) {
        cmdVx = constrain(a, -255, 255); cmdVy = constrain(b, -255, 255); cmdW = constrain(c, -255, 255);
        lastCmd = millis();
      }
      break;
    }
    case 'S': cmdVx = cmdVy = cmdW = 0; lastCmd = millis(); break;
    case 'M':
      if (s[2] == 'A' || s[2] == 'T') { mode = s[2]; cmdVx = cmdVy = cmdW = 0; autoUntil = 0; }
      break;
    case 'P': Serial.println(F("OK")); lastPing = millis(); break;
  }
}

void readSerial() {
  while (Serial.available()) {
    char ch = Serial.read();
    if (ch == '\r') continue;
    if (ch == '\n') { buf[bufLen] = 0; if (bufLen) handleLine(buf); bufLen = 0; }
    else if (bufLen < sizeof(buf) - 1) buf[bufLen++] = ch;
  }
}

void setup() {
  Serial.begin(115200);
  for (uint8_t i = 0; i < 4; i++) { pinMode(TRIG[i], OUTPUT); pinMode(ECHO[i], INPUT); }
  pinMode(ESTOP_PIN, INPUT_PULLUP);
  drive(0, 0, 0);
  Serial.println(F("READY"));
}

void loop() {
  readSerial();

  // Har siklda bitta sensor o'qiladi (bir-biriga xalaqit bermasligi uchun navbat bilan)
  dist[sensorIdx] = readCm(sensorIdx);
  sensorIdx = (sensorIdx + 1) & 3;
  readSerial();

  if (mode == 'A') {
    autonomous();
  } else {
    if (millis() - lastCmd > WATCHDOG_MS) { cmdVx = cmdVy = cmdW = 0; }  // aloqa uzildi
    applySafe(cmdVx, cmdVy, cmdW);
  }

  if (millis() - lastTele >= 100) {
    lastTele = millis();
    Serial.print('D');
    for (uint8_t i = 0; i < 4; i++) { Serial.print(' '); Serial.print(dist[i]); }
    Serial.print(' '); Serial.print(mode);
    Serial.print(' '); Serial.println(blocked ? 1 : 0);
  }
}
