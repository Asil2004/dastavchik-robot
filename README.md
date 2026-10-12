# Dastavchik robot: ulanish va ishga tushirish

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
3. Avval kompyuterda Serial Monitor (115200, Newline) orqali tekshiring: `P` -> `OK`, `V 150 0 0` -> oldinga yuradi (0.6 s dan keyin o'zi to'xtaydi), `M A` -> avtonom rejim.
4. Planshet yoki telefon (Android, Google Chrome): `planshet/index.html` sahifasini https manzildan oching.
   OTG kabel bilan Arduino'ni ulang, "USB ga ulanish" tugmasini bosing, ro'yxatdan Arduino'ni tanlang va ruxsat bering.
   Asl Arduino Uno ham, CH340 chipli klon ham ishlaydi. Ulanganda Arduino qayta yuklanadi, 2 soniya kuting.
   Kompyuterdagi Chrome'da ham shu sahifa ishlaydi (sinash uchun qulay).

## Xavfsizlik qoidalari (Arduino ichida, planshet nima desa ham ishlaydi)
- Qaysi tomonda to'siq 15 sm dan yaqin bo'lsa, o'sha tomonga yurmaydi. 45 sm dan yaqin bo'lsa sekinlashadi.
- Planshetdan 0.6 s buyruq kelmasa (kabel chiqib ketsa, ilova yopilsa) robot to'xtaydi.

## Ko'rish tizimi (kamera)
Telefon yoki planshet robotning old tomoniga mahkamlanadi, orqa kamerasi oldinga-pastga, polga qaraydi (kamera polning robotdan 20–60 sm oldidagi qismini ko'rsin).
- **Chiziq:** polga 3–5 sm enli qora izolenta yopishtiring (to'q polda oq lenta ishlating va "Chiziq och rangli" belgisini qo'ying). Robot chiziq bo'ylab yuradi. Mecanum g'ildiraklar tufayli chiziqdan chetga chiqsa, yonga siljib tuzatadi.
- **Stansiyalar:** `stansiyalar.html` sahifasida QR kodlarni chop eting (BAZA, 101, 102...). Ularni chiziq yonidagi devorga, kamera ko'radigan balandlikda yopishtiring.
- **Yetkazish:** "Kamerani yoqish" tugmasini bosing, manzilga 101 deb yozing va "Yetkazish" tugmasini bosing. Robot chiziq bo'ylab yuradi va 101 QR kodiga yetganda to'xtaydi.
- **Qaytish:** "Bazaga qaytish" tugmasi bosilganda robot 180° burilib, chiziq bo'ylab BAZA QR kodigacha qaytadi.
- Odam yoki to'siq chiqsa, ultratovush sensorlar robotni to'xtatadi. Yo'l ochilgach robot davom etadi.
- Chiziq yo'qolsa, robot 3 soniya qidiradi. Topolmasa, to'xtaydi.
