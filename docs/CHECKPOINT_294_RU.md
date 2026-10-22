# 294 — настоящая depth-текстура и закрытие старых возвратов

22.10.2026. Установленная QA .267, существующие изолированные мир/персонаж, non-batch Direct3D11, штатная камера3440×1440, target0. **31/31**, optional0; новых ресурсов/построек/terrain edits нет. Локальный shader292 включён только на время наблюдения, затем снят.

В реальном render callback увидели `Deferred Depth`:3440×1440, свойство `filterMode=Point`, texelSize=(1/3440,1/1440,3440,1440). Первое чтение одно, тот же cameraId908136; postRender7→137, productionFrames145. Это наблюдение свойства Unity Texture, а не прямое чтение аппаратного sampler state. Наблюдатель не вызывал дополнительный Camera.Render, GPU readback или снимок.

Первый валидатор ошибочно требовал depthFrame строго позже hostFrame RPC. Callback законно выполняется позднее в том же кадре: проверка исправлена на >= при обязательных начальных reads0/seenfalse и продвижении postRender. Проверки игры не ослаблялись.

Startup-финализатор290 закрыл два уже полностью полученных старых Wood-долга. В обоих cancelled/terminalBound=true, board=false, выдача0/0; все три инвентаря точны:23entries/67Wood. Из97файлов поменялись только два `.bwproject` и два `.bwpayout`. Независимый read-only обзор: framing/checksum верны; plan rev1→2, добавлена точная операция отмены, остальной payload побайтно прежний; payout rev15→16 добавил Terminal, Items/Collections/receipt-данные прежние. Нет повторной выдачи.

Штатный logout/main-menu/Quit, PID48032 завершён,3DLL моста восстановлены с хешами.4141обычный save: состав/размер/mtime прежние, содержимое не хешировалось. Конфиг .267 отличается только комментарием версии;0изменений значений. QA-сохранения и контрольные файлы менялись штатно.

Предел: старые уже полученные долги, private Local. Это не новый полный платный цикл, физическая E, crash-window, remote или приёмка тумана. Повторная загрузка проверяется отдельно в [296](CHECKPOINT_296_RU.md).

Доказательства: `artifacts/checkpoint-294/{scenario.json,native-report.json,validation.json,normal-exit.json,native-exit-audit.json,config-values.json,projects-before.json,projects-before-copy/}`; `ValheimQA/artifacts/20261022-074720/`.
