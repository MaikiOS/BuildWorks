# .224 — плавный возврат с локальной историей массы

2026-10-18 · [English](CHECKPOINT_224.md) · [Все14 блоков](LIVE_CHECKLIST_RU.md)

QA **0.19.224.0** установлена только в TerrainRamp-1.0-Test, SHA `53AF6E79410FD02397AE83CDD47D86B760105B007CDC92D507186F9C0DAFE64F`. Обычная стройка сохраняет прежний backend; постоянный проект/табличка этим этапом не подключены. Новый локальный candidate bundle — `6F2A58531F61A3E12FCE61A549546092B699815B85E2457DA31C3F261F8F6F3B`. Embedded bundle, утверждённый архив, transport, noise, порог768, стоимость и права прежние.

## Причина и исправление

На одинаковой canonical стене .223 восстанавливалась иначе в середине анимации: при поперечном проходе в6с damage legacy0,4038086 против Spatial3D0,6386719; max-разница0,3309326. Вдоль — max0,0361328. Конечный zero у обоих одинаковый; проверка только конца не обнаруживала различие характера возврата.

Прежний max27 выбирал самый медленный отдельный X-bin. Теперь для каждой из9 YZ-позиций считается суммарная текущая масса уникального локального окна X `[x−1,x,x+1]` и её **общий исторический peak**. Это max(sum), а не сумма индивидуальных peaks. Отдельный UpdateSpatialPeak читает все окна до dispatch очистки Away; гонки чтения соседей с очисткой нет. Новых buffers нет; старые live peaks не переиспользуются, требуется новый запуск/generation. Новый capability guard до allocations проверяет наличие и64×1×1 всех4 spatial kernels; bundle .223 отвергается. Legacy/default API прежние.

## Проверено

- Normal/QA0warnings/0errors, Geometry132 группы, host/localization1104/991, compiled QA-control/isolation прошли; независимые correctness/Ponytail PASSED. `verify` skill отсутствует.
- Два12с canonical маршрута: поперёк и вдоль. **78 byte-exact сравнений legacy с frozen v11 и78 SpatialWindow с legacy**: полные smoke/flow/surface при13 моментах каждого маршрута. По61 sample на маршрут/layout; кривые max/sum/active cells совпали. Это конкретная тонкая поX лабораторная стена, не все prefab/оси/повороты и не готовая художественная приёмка.
- GPU: прежние3 полных rotated/compound цикла168 exact pool-vs-dense, полные16384 слова stage/commit и rejected commit. Реальные28owners/7resident/8192blocks —98 041 636B, лимит98 304 000 сохранён. Отдельно разновременные peaks1000/1000 дают1000, одновременная масса2000 даёт2000; пустые окна очищают историю; x0/x15 без повторного счёта. Дальняя позиция поX1,8м возвращается0,458251953 при исходном damage0,5.
- Настоящий ValheimQA: **11/11 +66/66**, четыре прохода через одну8м стену из4 woodwall,4 ролика/343 кадров. Во время hold smoke/surface ненулевые; после выходов и в дополнительных свежих срезах alpha/recovery flags0. **383488 inner RGBA texel** совпали с reachable published payload, identity/sequence/tick/clock проверены. Native читает Ledger[2] flags, не все Away/Peak; gutters/interpolation/final pixels этим не доказаны. Инвентарь прежний, closed counters0.
- Штатные logout→menu→Quit;3DLL моста восстановлены. Audit: 4137 обычных файлов прежние по size/exact UTC ticks (не content hashes), protected 134/143, изменения только обычных QA saves/control и cfg version header. Мышь/клавиатура, выдача, native строительство/сброс/рельеф/удаление существ не использовались.

## Сохранённые отказы и границы

Первый9с замер остановился на требовании exact zero: legacy2,63e−5/spatial8,58e−4, оба ниже0,001. Горизонт увеличен до12с без изменения порога/solver. Оригинальный отказ и CSV сохранены. Тест прежнего/нового bundle сначала загрузил оба одновременно; Unity отказала `another AssetBundle with the same files is already loaded`. Исправлен порядок загрузки, исходный отказ сохранён. Сборка в песочнице получила NuGet `Access to the path is denied`; та же сборка вне песочницы прошла. Вспомогательная Python-команда получила SyntaxError; исправлен только анализатор, не критерии.

Первый GABS start отказал при atomic rename runtime.json (`Access is denied`), оставив preflight/gamePid0. Реального Valheim не было. После штатного закрытия только собственного GABS-клиента новое подключение само подтвердило stopped/stale-runtime-cleaned; только затем запуск повторён. Токены, identities и подготовка не сбрасывались; оба MCP-лога и исходная runtime-квитанция сохранены.

Локальные доказательства: BuildWorks/artifacts/fog-recovery-curves-224 (CSV, график, baseline bundle .223, window-fields/verification.json), fog-spatial-window-224/verified (полный pool/history/separation), checkpoint-224 (сборки/install/audit/source manifest); Default/ValheimQA/artifacts/20261018-162845 (сценарии, JSON,4MP4/PNG/JSONL, логи, restore). Записи около6–7 capture fps — не FPS игры. Общие Unity shutdown leak notices не используются как доказательство отсутствия утечек.

**Открыто:** matched-camera визуальное сравнение с утверждённым v11 на настоящих prefab, включая тонкую поZ woodwall, наклон/масштаб/повторный контакт, произвольную геометрию и другие ориентации; FPS/GPU timing/driver memory; дом28/его storage reserve; постоянный проект/табличка/оплата/возврат/reload/сервер. ОкноX конечно; совпадение с глобальной legacy X-колонкой доказано лишь для данных canonical маршрутов. Все14 блоков сохранены; Goal API active.
