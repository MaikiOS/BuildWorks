# Срез308 — восстановление сохранённого зачисления и видимость процесса

22.10.2026. Production-код/версия не менялись. QA.273 остаётся установленной; .274 — кандидат306.

`SavedCreditRecovery.ps1` извлекает фактическую ветку saved receipt из конструктора `BlueprintConstructionPayoutCollection`. Подставляются только границы журнала/профиля/игрового контекста.17 проверок PASS: sparse index1 восстанавливается через полный saved inventory proof; уже подтверждённая строка использует подтверждение существующего credit; отсутствующие/чужие/только loaded квитанции, отсутствующий слот, несовпадение summary/байтов и отказ capture/readback запрещают подтверждение. Изменение receipt/custody во время capture также запрещено. Контроль с удалением `Matches(credited)` обнаруживает небезопасное принятие. Проверка включена в HostContract, который прошёл на текущем игровом API/.274. `verify` отсутствует; независимый correctness/Ponytail review PASS, reviewer повторил17checks и negative control.

Доказательства: `artifacts/checkpoint-308/recovery.json`, `recovery.log`, `recovery-negative.log`, `host.log`. Это не native save/restart: фактические source/context/readback APIs остаются подставными. Сохранённый credit нельзя выдавать повторно при неопределённом состоянии; новые обходы/вставки не добавлялись.

Установка .274 не состоялась: до чтения токена/вызова Deploy guard выдал `An existing game session must not be changed.` Внутри sandbox Get-Process вернул0, вне sandbox — прежний Valheim PID57236, start2026-10-22T17:51:00.7863531+03:00. Вывод307 в передаче о завершении процесса был неверным и исправлен. Действия с процессом/вводом/профилем владельца не выполнялись; bridge Prepare, запуск, изменение DLL/сохранений не происходили.

Отсутствие игры проверять в полном контексте видимости процессов; отрицательный ответ sandbox не является доказательством. Guarded Deploy/Prepare не обходить. Следующий шаг после настоящего закрытия игры — установка .274 с SHA, native сообщение без станции, controlled saved-credit interruption306 → normal Quit → новый процесс без hook → точный inventory/receipt и cleanup → повторная загрузка. До этого требования8.7–8.8 остаются открытыми.
