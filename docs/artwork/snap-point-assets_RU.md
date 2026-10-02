# Ресурсы знаков привязки BuildWorks

[English](snap-point-assets.md) | **Русский**

Шесть оригинальных растровых PNG созданы через imagegen для .46 по
[утверждённому концепту](../images/snap-point-style-proposal-v1.png).
Это не вектор и не скриншот игры. Каждый файл 1254 × 1254 с прозрачным фоном.
Alpha, декодирование, настоящий sprite и попадание проверены в Unity.
Читаемость на игровом расстоянии ещё требует ответа Ostrix.

В .47 те же рисунки отображаются крупнее и ярче без наведения;
координаты/pivot сохранены. Отзыв по .46 показал слишком тусклую отрисовку;
после исправления читаемость в игре ещё требует проверки.

Каталог: `src/BuildWorks/Assets/BlueprintEditorIcons/`.
Золотой snap-native — ванильный замок; синий snap-corner — угол;
зелёный snap-midpoint — середина; розовый snap-centre — центр;
фиолетовый snap-pin — опора Shift; белый snap-active — активная точка.
Координаты настоящие. Обод ванильной и центр нашей имеют разные области
попадания; знаки не скрываются и не раздвигаются.

## Запросы отдельных мотивов

Английские запросы ниже сохранены из генерации каждого файла. Общее направление:
оригинальная скандинавская кованая деталь с широкой читаемой формой, открытым
центром, ограниченной гравировкой и прозрачным фоном. Ресурсы игры/других модов
не копировались.

### snap-native

`snap-native.png`

A gold interlocking timber-joint clasp: two opposed curved carved-metal jaw plates surrounding an OPEN transparent centre. Bronze-gold silhouette with ONE thick engraved interlace on each jaw. No disk fill.

### snap-corner

`snap-corner.png`

A BLUE notched forged right-angle carpentry bracket, like an L-shaped joint with a single broad engraved interlace and two large notches. Its inner hollow corner is the exact canvas centre. Keep most centre area open.

### snap-midpoint

`snap-midpoint.png`

A GREEN opposed pair of iron joint clamps, one above and one below the EXACT centre. Broad simple bevel and ONE engraved knot each. Open transparent central joint gap.

### snap-centre

`snap-centre.png`

A PINK carpenter compass rosette: four short forged pointed arms and a narrow engraved circular rim surrounding an open centre. Nordic joinery craft, not a generic cross.

### snap-pin

`snap-pin.png`

A PURPLE fixed pivot stake: narrow long forged Scandinavian pin with locking notch and a small open ring centred EXACTLY on canvas centre. Amethyst rim, sharp lower stake, two broad decorative grooves. Distinct silhouette.

### snap-active

`snap-active.png`

A WHITE luminous compact source hook: two opposing curved ivory-metal carved hooks around open exact centre. One broad Nordic interlace groove, bright pale bevel. Active attachment point, not arrow.

## Регистрация опоры

Готовый snap-pin имеет открытый внешний обод вокруг заполненного аметистового
центра, замковую насечку и не имеет общего фонового диска.
Центр кольца не в геометрической середине PNG:
sprite pivot `(0.496, 0.634)` регистрирует его точно на модели; остальные
используют `(0.5, 0.5)`. DrawArtwork учитывает pivot, не смещая настоящую точку.
Изменение рисунка требует повторных проверок alpha, попадания и малого размера.
