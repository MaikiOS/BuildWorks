# Изолированный канал управления QA

[English](QA_CONTROL.md) · [Текущая сборка/доказательства](CURRENT_STATUS_RU.md)

`BUILDWORKS_TEST_STAND` включает локальный ограниченный канал клавиатуры/мыши/состояния. Обычная сборка не содержит QA control и его InputSystem-зависимость. Это ввод в настоящий Valheim, не игра внутри Unity Editor. Канал использует изолированные файлы; GABS/ValAgent — отдельный явно разрешённый helper. Постоянный публичный gameplay-сервис не добавляется.

Разрешены только прежние QA-профиль/мир/персонаж. Личный token не публиковать, saves не создавать, provisioning/выдачу не сбрасывать и не повторять. Запуск: Thunderstore TerrainRamp-1.0-Test **Modded**. Сначала процесс/профиль/изоляция, затем текущие мир/фокус/позиция/инвентарь. Старый receipt или файл сессии не доказывает живой процесс.

PowerShell7 из канонического корня BuildWorks:

```powershell
$state = .\scripts\Invoke-QAControl.ps1 -Action state | ConvertFrom-Json
.\scripts\Invoke-QAControl.ps1 -Action batch `
  -SessionNonce $state.nonce -StopRevision $state.stopRevision `
  -StepsJson '[{"milliseconds":100,"lookX":20}]' -Capture
.\scripts\Invoke-QAControl.ps1 -Stop
```

Перед зависимой последовательностью читать свежее состояние. F12/stop.flag, потеря фокуса, физический ввод, смена мира/игрока или старые nonce/revision останавливают ввод и освобождают устройства/UI. Тайм-аут не разрешает повторять потенциально выполненное действие: проверить тот же живой запрос/процесс и свежий ответ. Capture добавляет путь снимка после JSON; границу JSON разбирать явно.

Локальный модельный пилот остановлен по просьбе Ostrix. Проверки используют native-наблюдение/ограниченные сценарии, а не обещают автономную игру со скоростью человека. Ввод/UI, ограниченный batch, оплаченный дом/reload и2 стены v11 имеют отдельные receipts; это не вся ручная/сетевая очередь. `Test-QAControlContract.ps1` и `Test-QASaveIsolation.ps1` — compiled/static-проверки, не запуск игры.

Перед серией — [живой список](LIVE_CHECKLIST_RU.md). Выдачу/storage WAL не повторять. Целый дом v11/табличка пока не готовы для ручной приёмки. [Исходная история](QA_CONTROL_HISTORY_206_RU.md) сохраняет старые счётчики/ошибки.
