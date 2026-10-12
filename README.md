# Dastavchik robot: ulanish va ishga tushirish

## Tayyor ilovalar (APK va EXE) va Web manzil
- **Android APK (Planshet / Smartfon uchun):** [`dist/dastavchik-robot.apk`](dist/dastavchik-robot.apk)
- **Windows EXE (Kompyuterda test va boshqaruv uchun):** [`dist/DastavchikRobot.exe`](dist/DastavchikRobot.exe)
- **Web boshqaruv paneli (GitHub Pages):** [https://asil2004.github.io/dastavchik-robot/planshet/index.html](https://asil2004.github.io/dastavchik-robot/planshet/index.html)
- **QR stansiyalar chop etish:** [https://asil2004.github.io/dastavchik-robot/planshet/stansiyalar.html](https://asil2004.github.io/dastavchik-robot/planshet/stansiyalar.html)

## Tuzilma
Planshet (miya, ekran, marshrutlar)  --USB OTG kabel-->  Arduino Uno + L293D shield  -->  4 motor, 4 ta HC-SR04

## Motorlar (L293D shield) — sizning ulanishingiz
| Shield | G'ildirak |
|---|---|
| M1 | old-chap |
| M2 | orqa-chap |
| M3 | old-o'ng |
| M4 | orqa-o'ng |

"Oldinga" bosilganda qaysidir g'ildirak orqaga aylansa, kod boshidagi o'sha g'ildirakning `INV_..` qiymatini `true` qiling.
Mecanum roliklari yuqoridan qaraganda "X" shaklini hosil qilishi kerak.

## Sensorlar (HC-SR04, 4 ta) — sizning ulanishingiz
| Sensor | TRIG | ECHO |
|---|---|---|
| Old | A3 | A2 |
| O'ng | 10 | 9 |
| Orqa | A5 | A4 |
| Chap | A1 | A0 |

Favqulodda STOP tugmasi (ixtiyoriy): pin 2 dan GND gacha. Bosilganda robot to'xtaydi.

## Quvvat
- Motorlarni Arduino USB tokidan EMAS, shield'dagi EXT_PWR kontaktidan oziqlantiring (2S Li-ion 7.4V yoki 6xAA).
- Batareya ulanganda shield'dagi PWR jumperini olib qo'ying. Shunda Arduino planshet USB'sidan, motorlar batareyadan quvvat oladi.
- Planshet USB orqali Arduino'ni quvvatlaydi (~50 mA), bu muammo emas.

## Ishga tushirish
1. Arduino IDE: Library Manager orqali "Adafruit Motor Shield library" (V1) ni o'rnating.
2. `firmware/robot_dastavchik/robot_dastavchik.ino` ni Uno'ga yuklang.
3. Avval kompyuterda Serial Monitor (115200, Newline) yoki `dist/DastavchikRobot.exe` orqali tekshiring: `P` -> `OK`, `V 150 0 0` -> oldinga yuradi (0.6 s dan keyin o'zi to'xtaydi), `M A` -> avtonom rejim.
4. Planshet yoki telefon (Android): `dist/dastavchik-robot.apk` ilovasini o'rnating yoki Google Chrome'da `planshet/index.html` sahifasini https manzildan oching.
   OTG kabel bilan Arduino'ni ulang, "USB ga ulanish" tugmasini bosing va ruxsat bering.
   Asl Arduino Uno ham, CH340 chipli klon ham ishlaydi. Ulanganda Arduino qayta yuklanadi, 2 soniya kuting.
5. **Arduinosiz test qilish:** `Simulyator (Test)` tugmasini hamda kamerada `Virtual test kamera (Simulyator)` rejimini tanlab, robot ulanmagan holda ham barcha funksiyalarni tekshirishingiz mumkin.

## Tashqi kamera ulash usullari
Smartfon yoki planshet robotning tepasida qulay burchakda tursa, polga qaratilgan **tashqi kamerani** 2 xil usulda ulash mumkin:
1. **USB Web-kamera / Endoskop (UVC kamera) — USB OTG Hub orqali:**
   - Smartfonga **USB OTG Hub** (razvetvitel) ulanadi: birinchi portga Arduino Uno, ikkinchi portga USB veb-kamera ulanadi.
   - Boshqaruv panelidagi kamera ro'yxatidan (`🔄` tugmasini bosib) ulangan USB kamerani tanlaysiz va **"Kamerani yoqish"** tugmasini bosasiz.
2. **Wi-Fi / IP Kamera (ESP32-CAM yoki ikkinchi smartfon):**
   - Robotning pastki qismiga **ESP32-CAM** yoki ikkinchi telefon (*IP Webcam* ilovasi bilan) o'rnatiladi.
   - Kamera ro'yxatidan **"Wi-Fi / IP kamera (URL)"** ni tanlab, oqim manzilini (`http://192.168.43.1:8080/video`) kiritasiz.

## Xavfsizlik qoidalari (Arduino ichida, planshet nima desa ham ishlaydi)
- Qaysi tomonda to'siq 15 sm dan yaqin bo'lsa, o'sha tomonga yurmaydi. 45 sm dan yaqin bo'lsa sekinlashadi.
- Planshetdan 0.6 s buyruq kelmasa (kabel chiqib ketsa, ilova yopilsa) robot to'xtaydi.

## Ko'rish tizimi (kamera)
- **Chiziq:** polga 3–5 sm enli qora izolenta yopishtiring (to'q polda oq lenta ishlating va "Chiziq och rangli" belgisini qo'ying). Robot chiziq bo'ylab yuradi. Mecanum g'ildiraklar tufayli chiziqdan chetga chiqsa, yonga siljib tuzatadi.
- **Stansiyalar:** `stansiyalar.html` sahifasida QR kodlarni chop eting (BAZA, 101, 102...). Ularni chiziq yonidagi devorga, kamera ko'radigan balandlikda yopishtiring.
- **Yetkazish:** "Kamerani yoqish" tugmasini bosing, manzilga 101 deb yozing va "Yetkazish" tugmasini bosing. Robot chiziq bo'ylab yuradi va 101 QR kodiga yetganda to'xtaydi.
- **Qaytish:** "Bazaga qaytish" tugmasi bosilganda robot 180° burilib, chiziq bo'ylab BAZA QR kodigacha qaytadi.
