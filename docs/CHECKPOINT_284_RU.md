# Проверка284 — часы эффекта и независимое решение Astra

21.10.2026. **Это диагностика, не исправление тумана.** QA .264/DLL/bundle не менялись. По запросу Ostrix Astra с effort `ultra` независимо прочитала лабораторию v11, текущий renderer и доказательства. [Решение и две проверки](FOG_RENDER_DECISION_RU.md): изолированный локальный туман контакта вместо объёмной отрисовки всего дома; кандидат ещё не реализован. [English](CHECKPOINT_284.md).

В существующий внешний `ValheimQA/render-probe/CadenceProbe.cs` добавлено ограниченное CPU-наблюдение `HouseStatus`: не чаще4Гц, до272срезов/32768символов, до дополнительного controlled render. Полные typed owner records, отсутствие fault/error и границы queue/pending/ticks обязательны;16negative controls выполняются в игре. Второй вариант добавляет `Time.timeAsDouble` и `Time.unscaledTimeAsDouble`. Никаких GPU readback/PNG, новых контроллеров или изменений часов игры/эффекта.

| Проверка | Результат | Граница |
| --- | --- | --- |
| Внешний observer | Две сборки0warning/0error, существующие Python tests7/7; native guard16negative, analyzer4negative | `verify` skill отсутствует; отдельный read-only review |
| Native, первый прогон | `ValheimQA/artifacts/20261021-165815`,15/15,24,6с; rest23,08/23,01fps, contact12,79fps | Только real/effect clocks, без game clock |
| Native, второй прогон | `ValheimQA/artifacts/20261021-170841`,15/15,24,6с; rest22,26/22,15fps, contact12,44fps | Controlled3440×1440/FOV55 с дополнительным Camera.Render; не штатная камера/HUD или GPU timer |
| Часы второго прохода |54среза:15,8824с real,14,0514с game,14,0609с effect; game отстал от real на1,8310с, effect от game на−0,0095с | Отдельного накопительного clock-лага эффекта в этом окне нет; не доказана причина владельческих20с |
| CPU очередь | Max captured-confirmed14ticks,queued10,pending7; max read16,515мс | CPU confirmation не равен GPU publication или видимому кадру |
| Очистка обоих запусков |97projectSHA/23inventory/67Wood,8cfg values,4141ordinary-save membership/size/mtime прежние; ошибки BepInEx0, logout/menu/Quit/restore3/PIDgone | Содержимое ordinary-save не хешировалось; QA save chunks штатно менялись |

В игре `maximumDeltaTime=.2`, `timeScale=1`. Часы эффекта в измеренном окне идут почти вместе с игровыми; изменение часов вслепую не решает измеренную низкую скорость. GPU/видимую задержку по этим числам не заявляем. Hold выполнялся в центре дома, а не постоянно внутри стены; следующий visual gate требует удержания именно в материале.

Сохранены исходные неудачи инструментария: `Tool 'player/inventory' not found` исправлено на подтверждённое `player/get_inventory`. Первый аудит ошибочно хешировал также сменяемые QA save chunks и остановился на отсутствующем `_main.206.chunks`; исправлена область проверки на cfg плюс отдельные97project-хешей и обычные сохранения. Никакие файлы мира не восстанавливались/удалялись, проверки ресурсов не ослаблялись. Запросы несуществующих `WORK_STATUS.md`/`prepare.json` не служили доказательствами; фактические файлы прочитаны по их именам.

Доказательства: `artifacts/checkpoint-284/clock-summary.json`, `clock-summary-game-clock.json`, исходные cadence JSON, два полных native test report, `audit.json`, `audit.ps1`, build/restore/BepInEx logs и снимки observer source. `analyze_clock.initial.py` проверяет первый формат; текущий `analyze_clock.py` требует оба новых clock-поля. Старый отчёт не дополнялся выдуманными полями.

Вывод: прекратить цепочку shader-микрооптимизаций; следующий этап — архитектурный кандидат из решения Astra с двумя ограниченными gate. Ручной осмотр не готов.9.2–9.7, физическая E/табличка, достройка/платная отмена и сеть открыты.
