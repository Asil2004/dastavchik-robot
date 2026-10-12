/*
  «QALQON — ROBOT-PATRUL» (Navoiy viloyati IIB & NDKTU)
  Avtonom ko'chma robot-stansiya laboratoriya prototipi mikrodasturi
  Arduino Uno + L293D Motor Shield (AFMotor) + 4x Mecanum + 4x HC-SR04

  Serial protokol (115200 bod, '\n' bilan tugaydi):
    V vx vy w   harakat: vx=oldinga(+)/orqaga(-), vy=o'ngga(+)/chapga(-),
                w=soat yo'nalishida burilish(+). Har biri -255..255
    S           favqulodda to'xtash
    M T         bort/operator boshqaruvi rejimi (standart)
    M A         avtonom patrul rejimi (ultratovush orqali to'siqlardan qochib patrullik)
    L 1 / L 0   politsiya mayoqchasi (miltillovchi chiroq / sirena, Pin 13) yoqish/o'chirish
    P           tekshirish -> "OK" javobi
  Arduino har 100 ms da telemetriya yuboradi:
    D old o'ng orqa chap rejim bloklangan   (masofalar sm da, 999 = to'siq yo'q)
*/
#include <AFMotor.h>

AF_DCMotor mFL(1);  // M1: old-chap
AF_DCMotor mBL(2);  // M2: orqa-chap
AF_DCMotor mFR(3);  // M3: old-o'ng
AF_DCMotor mBR(4);  // M4: orqa-o'ng
const bool INV_FL = false, INV_FR = false, INV_BR = false, INV_BL = false;

// Ultratovush sensorlar (TRIG, ECHO): Old A3/A2, O'ng 10/9, Orqa A5/A4, Chap A1/A0
const uint8_t TRIG[4] = {A3, 10, A5, A1};
const uint8_t ECHO[4] = {A2, 9,  A4, A0};
const uint8_t ESTOP_PIN = 2;    // Favqulodda STOP tugmasi (GND ga)
const uint8_t BEACON_PIN = 13;  // Politsiya mayoqchasi / sirena rele pini

const int STOP_CM = 15;         // 15 sm dan yaqin to'siqqa yurish bloklanadi
const int SLOW_CM = 45;         // 45 sm dan yaqinlashganda tezlik 50% pasayadi
const int MIN_PWM = 90;
const unsigned long WATCHDOG_MS = 600;
const int AUTO_SPEED = 150;

enum { F = 0, R = 1, B = 2, L = 3 };
int dist[4] = {999, 999, 999, 999};
char mode = 'T';
int cmdVx = 0, cmdVy = 0, cmdW = 0;
unsigned long lastCmd = 0, lastTele = 0, lastPing = 0;
uint8_t sensorIdx = 0;
bool blocked = false;
bool beaconOn = false;
char buf[40];
uint8_t bufLen = 0;

void setMotor(AF_DCMotor &m, int s, bool inv) {
  if (inv) s = -s;
  if (s == 0) { m.setSpeed(0); m.run(RELEASE); return; }
  int a = abs(s);
  a = MIN_PWM + (long)a * (255 - MIN_PWM) / 255;
  m.setSpeed(a);
  m.run(s > 0 ? FORWARD : BACKWARD);
}

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
  unsigned long us = pulseIn(ECHO[i], HIGH, 12000UL);
  if (us == 0) return 999;
  return us / 58;
}

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
  drive(vx, vy, w);
}

unsigned long autoUntil = 0;
int autoVx = 0, autoVy = 0, autoW = 0;
void autonomous() {
  unsigned long now = millis();
  if (now < autoUntil) { applySafe(autoVx, autoVy, autoW); return; }
  if (dist[F] > SLOW_CM) { autoVx = AUTO_SPEED; autoVy = 0; autoW = 0; autoUntil = now + 100; }
  else if (dist[R] > SLOW_CM || dist[L] > SLOW_CM) {
    autoVx = 0; autoW = 0;
    autoVy = (dist[R] >= dist[L]) ? AUTO_SPEED : -AUTO_SPEED;
    autoUntil = now + 500;
  } else {
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
    case 'L':
      beaconOn = (s[2] == '1');
      if (!beaconOn) digitalWrite(BEACON_PIN, LOW);
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
  pinMode(BEACON_PIN, OUTPUT);
  drive(0, 0, 0);
  Serial.println(F("READY"));
}

void loop() {
  readSerial();
  dist[sensorIdx] = readCm(sensorIdx);
  sensorIdx = (sensorIdx + 1) & 3;
  readSerial();

  if (beaconOn || blocked) {
    digitalWrite(BEACON_PIN, (millis() / 150) & 1);
  }

  if (mode == 'A') {
    autonomous();
  } else {
    if (millis() - lastCmd > WATCHDOG_MS) { cmdVx = cmdVy = cmdW = 0; }
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
