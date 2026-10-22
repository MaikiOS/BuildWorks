# 296 — закрытые возвраты пережили новый процесс без повторной выдачи

22.10.2026. QA .267, новый PID20084, batch/background Direct3D11; фактические TerrainRamp-1.0-Test/qaSaveRoot/cloud=false, прежние персонаж/мир1137745799. **16/16**, optional0, сценарий1,9с после загрузки. Нет строительства, выдачи ресурсов, сбросов или render-проверок.

Для каждого из двух старых закрытых долгов проверены cancelled/terminalBound=true, board=false, count/units0, credits2/savedReceipts2 в RAM и на диске, CleanupSaved2, pending/sources0. Счётчики текущих workers saveAttempts/destroyCalls/worldSaveRequests/profileSaveRequests0; это не постоянная трассировка всех вызовов. Независимый read-only обзор подтвердил ветку source: уже отменённый план не попадает в paid finalization, settled cleanup не создаёт worker. Три точных инвентаря23entries/67Wood; **все97project-файлов побайтно прежние**. Четыре квитанции сохранены, повторных эффектов не наблюдается. Sources0 — скан загруженных ItemDrop, не всех сохранённых world chunks.

`SelectPayout.inventoryUnchanged=false` и `exactLastInventory=false` сравнивают с историческими снимками до выдачи/последнего старого получения. Это не новый расход: отдельный exact current-run inventory и CleanupStatus.inventoryUnchanged=true проверены обязательно.

Штатный logout/main-menu/Quit/выход20084,3DLL моста восстановлены и проверены.4141обычный save-файл: состав/размер/mtime прежние, содержимое не хешировалось. Из baseline250config-файлов изменились только9QA save/control; настройки мода и97projects прежние. Предупреждения: известные отсутствующие необязательные backpack API, Local player destroyed и звук main-menu; новых ошибок сценария нет.

Это закрывает повторный startup/reload **конкретных уже полученных локальных долгов**, а не весь8.7/8.8: новый путь до получения, физическая E, точка сбоя между terminal binding и заменой плана, прочие предметы/содержимое, paid UI и сеть ещё открыты. Внешний вид/скорость тумана не проверялись.

Доказательства: `artifacts/checkpoint-296/{scenario.json,native-report.json,validation.json,projects-before.json,normal-exit.json,native-exit-audit.json}` и `ValheimQA/artifacts/20261022-081937/{mcp.jsonl,player.log,bepinex.log,restore.json}`.
