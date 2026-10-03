# Ресурсы знаков привязки BuildWorks

[English](snap-point-assets.md) | **Русский**

## Текущая система точек

.52 сохраняет янтарный `snap-native` и светлый узел `snap-helper` обычных углов
габаритов/середин. Выбранная A получает резной обод `snap-selected`, не меняя
происхождение. Дополнительные резкие углы меша — маленькие полупрозрачные тёплые
точки LineRenderer, не крупный PNG узла. Глубина ослабляет неактивные точки;
наведение/выбор усиливают их, выбранная получает тот же обод. `snap-geometry` —
исторический рисунок .50, не текущая экспериментальная точка. Типов происхождения
два: ваниль/наши; цветов угла/середины/центра нет. Мировой F9 не меняется.

Три оригинальных прозрачных растровых PNG 1254 × 1254 сгенерированы по отдельности
через imagegen. Геометрический знак — правка нашего узла, не новый ванильный.
Регистрация всех (0.5,0.5). Нейтральный цвет шейдера, уменьшенные уровни текстуры,
трёхлинейная фильтрация и импорт стенда без сжатия соответствуют встроенному декодеру.
Размер от модели в 3D включён по умолчанию; в экранном режиме радиусы прямоугольников
ваниль/наши — 16/9 пикселей. Радиус обода — 1,3× ванильной или 2,1× нашей.
Появление у курсора, глубина и согласованные области нажатия работают в обоих режимах.
Проверка рендера не заменяет приёмку Valheim; концепт не считается результатом игры.

## Точные запросы генерации .50

Ниже настоящие английские запросы генерации/редактирования, не восстановленные описания.
### snap-helper

Use case: stylized-concept. Asset type: one production raster snap-point icon for BuildWorks Nordic construction editor, transparent square canvas, NOT a mockup or concept board. Create a compact pale IVORY forged carpenter's joining knot: four short chunky interwoven hooked lobes, softly rounded angular Viking carving, one broad dark incised groove. All lobes balanced symmetrically around an OPEN small transparent centre exactly at canvas centre. Recognizable as ONE generic attachment point, NOT an L-shaped corner, axis, compass or star. Slender charcoal outside rim, warm ivory enamel face, restrained shallow bevel. Very readable at only 18-28 pixels, strong silhouette and generous negative space. Motif occupies 85% canvas. No background, letters, numbers, scene, other icons, photoreal texture, fine ornament, rivets, blue/red/green/purple/pink, glitter, drop shadow, glow or specular white highlights. This is a standalone final sprite; geometry centre is its attachment centre.

### snap-selected

Use case: stylized-concept. Asset type: one production raster selected-pivot OVERLAY for BuildWorks Nordic construction editor, transparent square canvas, NOT a mockup. Draw a warm IVORY carved Nordic locking collar: an OPEN thin forged outer circular band with four broad incised chevron notches and a small clearly recognizable symmetrical locking rune at exact centre, with two short horizontal prongs. Mostly transparent interior, so underlying amber or ivory attachment point remains identifiable. Centre rune occupies no more than 22% motif width, band radius 43% canvas width, balanced centre exactly50%50%. Short chunky restrained bevel, dark charcoal outline and incision, warm ivory face. Readable at24-36 pixels. No filled medallion, background, letters, numbers, extra icons, scene, fine decorative loops, blue/red/green/purple/pink, jewels, drop shadow, rays, sparkle, bloom, metallic white highlights. Overlay shape must read unmistakably as SELECTED AND LOCKED pivot and be visibly different from an unselected simple point.

### snap-geometry

Edit target: attached production snap helper knot sprite. Change ONLY ivory enamel faces to muted warm amber-brass. Preserve exact hooked knot silhouette, dark charcoal outline, symmetric central registration, alpha transparency, canvas and margins, carved groove and bevel. No additions. This is a smaller experimental geometry-point sprite, NOT native circular socket; MUST keep the same four-hook helper knot shape so it remains distinguishable from native snap sign. No extra icons, text or background.

## Исторические ресурсы .48/.49

Цвета, регистрация и размеры ниже — история, не текущая легенда редактора.
Старые файлы оставлены для воспроизводимости.

Шесть новых растровых PNG созданы по отдельности через imagegen для .48.
[Прежний концепт](../images/snap-point-style-proposal-v1.png) сохранён как история.
Это не вектор и не скриншот игры. Каждый файл 1254 × 1254 с прозрачным фоном.
Alpha, декодирование, настоящий sprite и попадание проверены в Unity.
Читаемость на игровом расстоянии ещё требует ответа Ostrix.

После скриншотов .47 Ostrix утвердил компактную систему вложенных знаков.
Экранный радиус ванильных теперь 16 px, наших — 9 px при настройках по умолчанию.
Опора и активная отметка — отдельные слои: тип нашей точки не заменяется.
Настоящие координаты сохранены. Читаемость в игре ещё требует одобрения Ostrix.

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

Muted amber-brass native socket: TWO thin opposed C-shaped forged jaw segments forming a slim outer circular clasp. Large EMPTY central aperture, at least 65% of full motif width, for a blue corner icon to fit INSIDE without overlap. Each jaw has one broad notch. It must read as a connection socket, NOT a wreath, wings, laurel, jewellery, or filled medallion. Uniform dark outside and inside rim. Warm gold face, not dazzling white.

### snap-corner

`snap-corner.png`

Bright azure-blue carpentry corner: a compact forged L-shaped right-angle bracket with two short equally thick arms, one broad angular Nordic notch in each. The EMPTY INSIDE elbow is exactly at canvas centre (50%,50%); bracket lies mainly BELOW and LEFT of that centre; both arm ends balanced around it. Do NOT place the elbow at the bottom-left of the canvas. Compact silhouette that can fit in an outer gold ring. No long arms or detailed knots.

### snap-midpoint

`snap-midpoint.png`

Fresh jade-green midpoint clamp: TWO small opposed forged wedge jaws directly ABOVE and BELOW the exact canvas centre. Equal chunky short bars with inward triangular notches; open central gap, no other arms. Each has a single broad incised line. Symmetric, compact, clear green face and dark outline, can fit inside a gold outer socket.

### snap-centre

`snap-centre.png`

Soft rose-pink Nordic carpenter compass: FOUR short tapered forged points around a SMALL hollow central diamond precisely at canvas centre. Broad dark edge, pink enamel face and one simple incised groove per point. Compact balanced star, no surrounding circle, no long needles, no glitter. Recognisable at 20 pixels, not ornate rosette.

### snap-pin

`snap-pin.png`

Amethyst-purple fixed pivot OVERLAY: a narrow locking pin with a hollow small diamond head exactly at canvas centre, two short prongs extending vertically upward and downward. Mostly transparent canvas, narrow silhouette. Purple colour and dark outline, one strong notch. Designed as a small centre overlay so the gold/blue/green BASE POINT remains visible around it. No large background disk, no long spear, no gemstone covering the centre.

### snap-active

`snap-active.png`

Ivory-white active-source OVERLAY: FOUR SHORT inward-pointing forged corner ticks around an EMPTY transparent central opening exactly at canvas centre. Compact square aperture and one broad Nordic cut notch, graphite outer edge. Thin open shape that fits inside a larger snap icon, no filled disk, no rays, no sparkle, no runic letters.

## Регистрация и уменьшение рисунка

У синей скобы pivot `(0.43, 0.42)` задаёт внутренний угол; у остальных новых
PNG — `(0.5, 0.5)`. DrawArtwork регистрирует мотив, не прозрачный прямоугольник.
Добавленная опора — состояние, не новая ванильная точка: исходные типы видны.
Радиус опоры 10,5 px, белых активных скоб 6,5 px. Это радиусы прямоугольника,
не непрозрачной формы; прозрачные поля также уменьшают видимый размер.

## Почему генерация отличается от игры

Изображение 1254 px превращается в ванильный прямоугольник 32 px или наш 18 px.
Мелкая гравировка не может остаться столь же подробной. Смещённый в PNG мотив
также выглядит смещённым при правильных координатах точки.
Шейдер .47 умножал RGB на 1,8/1,65/1,8, меняя цвет и контраст; в .48 множитель
белый нейтральный. Стенд ранее изменял размер PNG и сжимал их иначе, чем декодер
встроенного ресурса. Теперь оба пути сохраняют 1254 px, уменьшенные уровни текстуры
(mipmaps) и трёхлинейную фильтрацию; на стенде нет сжатия. Эти уровни уменьшают
мерцание мелких штрихов, но не возвращают потерянную детализацию.

Проверяем настоящую отрисовку на дереве и сетке: совпадающие типы, активное
состояние и опору, не увеличенный концепт. Тесты Unity требуют видимости цветов
наших и ванильных знаков в одной координате. Внешний host/ввод подменены:
камера Valheim, масштаб экрана и физическое управление остаются игровой проверкой.

## Общий запрос генерации

Use case: stylized-concept. Asset: one production raster snap-point icon for a Nordic Valheim-style construction editor, NOT a concept board. Transparent background, square canvas. Orthographic front-facing painted forged-metal UI emblem, no perspective. Readable when reduced to 20-28 pixels: broad simple silhouette, dark charcoal outer outline, strongly colored enamel face, one LARGE shallow engraved notch/interlace only. No photoreal fine texture, cracks, tiny rivets, bloom, drop shadow, text, letters, scene, labels, borders, or additional icons. All components centred precisely on the geometric canvas centre; centre of attachment is OPEN transparent and obvious. Occupy 80% of canvas, balanced 10% margins. Keep colour stable without white specular highlights. Restrained game-like bevel.
