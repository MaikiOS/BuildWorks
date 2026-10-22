# Срез312 — файловое доказательство сохранённого возврата

22.10.2026. Production-код и кандидат **.277** не менялись; проверенная установленная QA — **.273**. Valheim владельца в Default-Compat-Test подтверждён вне sandbox, PID57236; Deploy/Prepare/запуск не выполнялись.

Для следующего контролируемого прерывания306 подготовлена команда StoreTests `--saved-credit-evidence`. Прежняя разовая extraction305 принимала только уже подтверждённую строку; новая команда умеет явно требовать `intent` либо `credited`. Она копирует только файлы выбранного проекта во временный каталог, читает их настоящим PayoutJournal.Read со всеми связанными проверками и сверяет профиль настоящим NativePlayerProfileReadback.TryVerifyInventoryAndCustomData. Нужны точные AfterInfo/AfterBytes, каноническая квитанция и переданный SHA512 payload профиля. Исходные файлы повторно сверяются; подтверждение кредита, cleanup и игровые действия не вызываются.

Сначала собрать StoreTests, затем из BuildWorks:

```powershell
dotnet build ./tests/BuildWorks.StoreTests/BuildWorks.StoreTests.csproj -c Release
./tests/BuildWorks.StoreTests/bin/Release/net472/BuildWorks.StoreTests.exe --saved-credit-evidence $projects $world $project $profile $name $index intent $capturedProfilePayloadSHA512
```

Все переменные брать из текущей изолированной QA-сессии и выбранного НОВОГО проекта. `intent` применять после triggers=1 подготовленного hook306, до нового E и подтверждения журнала. `$capturedProfilePayloadSHA512` — `savedProfileHash` этого срабатывания, не SHA256 всего файла и не значение прежнего прогона. Для последующего подтверждённого состояния использовать `credited` и соответствующий текущий captured payload hash. Команда не устанавливает происхождение переданных параметров: идентичности/PID/cloud, срабатывание hook, свежий процесс, native E, отсутствие повторной вставки и cleanup подтверждаются отдельными игровыми отчётами. На новом процессе сначала проверить сохранённую пару, затем обычный E; после cleanup проверить ещё один fresh replay. Не перевыдавать settled-проект305 и не менять старые журналы.

Проверки:

- StoreTests build:0 ошибок/0 предупреждений. `verify` отсутствует; выполнены прямой обзор и адресные проверки.
- `tests/BuildWorks.CaptureChecks/SavedCreditEvidence.ps1`:11 PASS на сохранённых файлах .273. Успешно сверена реальная подтверждённая пара305. Отвергнуты другая стадия, неверные hash/имя/index, квитанция при другом точном инвентаре, повреждённые payout/profile и отсутствие канонической квитанции. Missing CLI arguments также отвергнуты.
- Отдельная временная копия корректного журнала переведена в синтетический CreditIntent с канонической сериализацией; точная сохранённая пара принята только в режиме intent. Эта копия не является игровым доказательством прерывания. Оригиналы не менялись.
- Payout200+property289+capacity9 и interruption guard23 PASS. Первый payout-запуск остановлен sandbox с `System.InvalidOperationException: Отказано в доступе по данному пути`; исходный отчёт сохранён, тот же тест прошёл вне sandbox.

Артефакты: `artifacts/checkpoint-312/build-store.log`, `saved-credit-evidence.json/log`, `arguments-negative.log`, `payout.log`, `payout-sandbox-denied.log`, `interruption-guard.json/log`. `.277` не установлен и native interruption/recovery не выполнены. Полная цель и все14 разделов остаются открытыми в прежнем объёме.
