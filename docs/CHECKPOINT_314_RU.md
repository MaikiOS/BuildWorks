# 314 — реальное прерывание кредита и причина блокировки после Load

22.10.2026, QA .277/SHA256
`BD157024BAC4E366360AF2EC345DDB295A48048291EFC1EB9BE57EDEA9443DBC`.
Игра владельца закрыта перед guarded deploy. Только TerrainRamp-1.0-Test,
cloud=false, QA save root, существующие BW_QA_43d3719a/world1137745799/player376043676.

Новый собственный проект `fcc0daaa42a540889cdde1b4af89917d`: обычный план двух стен,
E/табличка → достройка4Wood67→63 → отмена с удалением без native drops → отдельная
сохранённая sealed выдача/terminal. Никаких grants/reset/старой повторной выдачи.

Hook306 ровно один раз остановил подтверждение index1 после native сохранения
инвентаря+квитанции: Wood65, credit Intent, обе sealed sources ещё существуют.
Настоящий verifier312 из e470a7a подтвердил исходный canonical .fch и повторную пару
после штатного Quit. Это native controlled exception, не power-loss/kill.
Артефакты: `checkpoint-314/saved-intent`, `after-exit-intent`, proof logs.

Свежий процесс сохранил Intent/65Wood/9receipts, но E отказал до повторной вставки:
`Exact completed transfer is required before profile readback.` Read-only инспекция
выявила только Hammer: сохранено9412 сотых, native Load даёт94.1199951, повторная
native сериализация —9411. Live inventory точно совпадает с существующим native
Load comparator; canonical файл всё ещё содержит оригинальную After-пару и receipt.
Причина: pending recovery ошибочно требовала повторную сериализацию старой пары.
Доказательство: `readback-difference-report.json`, `before-compat-recovery.log`.

Для безопасного завершения собственного QA-проекта на установленной .277 выполнен
независимо проверенный однократный constructor finalizer. Он ограничен exact
project/operation/index/entry/player/world/version/исходной ошибкой, вызывает ту же
извлечённую новую reader logic и настоящий `ConfirmSavedCredit` с полным canonical
evidence. Ни AddItem, ни Save, ни ручной правки inventory/journal/receipt в helper нет.
После одного trigger получен profile-credited/SaveAttempts0/Wood65, штатный cleanup2.
Helper снят; index0 затем подобран штатно: Wood67/10receipts, обе sources0/cleanup2.
Это временное QA-восстановление .277, НЕ native проверка новой DLL .279.

Run172053/PID25968 и run173722/PID18364: normal logout→main_menu→Application.Quit,
процессы отсутствуют вне sandbox; по3 исходных DLL моста восстановлены/хеши сверены.
Все115 прежних project-файлов неизменны. Последний run завершён; bridge не оставлен.
Устаревшее ожидание trigger1 при втором выходе отказало до logout; исправленный
сценарий ожидал0, прошёл4/4. Все failed/corrected отчёты сохранены.

Срез315 исправляет production reader: оригинальная точная дисковая After-пара плюс
точная native Load-проекция либо прежняя raw live pair; canonical receipts/custody
и generation сохраняются. Новая DLL требует отдельного свежего native случая.
