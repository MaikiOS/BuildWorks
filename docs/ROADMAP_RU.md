# ROADMAP BuildWorks

Это публичная сводка направления. Статус функции означает наличие и уровень проверки, а не обещание даты.

## Сейчас: стабилизация альфы 0.19.x

- [x] точное размещение одной native-детали;
- [x] XYZ-гизма, вращение по трём осям и числовой ввод;
- [x] равномерный масштаб 1–400%;
- [x] индексный молоток, поиск, категории, материалы, недавнее и избранное;
- [x] библиотека составных чертежей;
- [x] отдельный Blueprint Editor;
- [x] дерево деталей и вложенных групп;
- [x] локальные опоры групп и отдельная мировая опора чертежа;
- [x] editor/world Array и Contour;
- [x] Undo/Redo основных изменений;
- [x] автоматический Unity Workbench и host-contract проверки;
- [ ] ручной smoke 0.19.33: поверхность скосов/крыш/мебели, Q/E ниже сетки, свежий Array;
- [ ] полный save/exit/reload gate;
- [ ] host + remote client multiplayer gate;
- [ ] расширенная матрица vanilla и modded prefab.

## Следующий продуктовый этап: сохраняемые операции

- [ ] Store v10 с необязательными recipes;
- [ ] недеструктивный стек операций группы;
- [ ] сохраняемый Array с устойчивыми ID копий;
- [ ] Line guide;
- [ ] перенос текущего Contour на общий evaluator;
- [ ] Bake в обычные детали одной Undo-операцией;
- [ ] Align/Target после доказанной базовой модели.

## Кривые и направляющие

- [ ] Polyline guide;
- [ ] Arc guide;
- [ ] Bezier guide с устойчивыми tangent/normal frames;
- [ ] повтор rigid pieces без деформации;
- [ ] отдельные оригинальные adaptive pieces с общей preview/commit mesh;
- [ ] проверка UV, collider, snap points и multiplayer reconstruction.

## Обмен и совместимость

- [ ] безопасный import/export чертежей;
- [ ] manifest зависимостей prefab/material/source;
- [ ] сохранение неизвестных данных без потерь;
- [ ] понятное восстановление при отсутствии нужного мода;
- [ ] отдельный контракт совместимости с EarthWorks/TerrainRamp без скрытого изменения земли.

## Позже

- [ ] компактный redesign мирового F9 HUD;
- [ ] дополнительные MoGraph-подобные операции после Array/Guide foundation;
- [ ] оригинальные материалы и production-ready adaptive building set;
- [ ] локализации и публичная упаковка для mod manager.

Не входят в ближайший этап: node graph, expression language, arbitrary external references, деформация чужих prefab, full Fields/dynamics и автоматическая установка сторонних модов.

