# Этап264 — реальная отрисовка выявила незакрытый сбой дома

20.10.2026. Установленная QA/игровая база **0.19.250**, DLL и shader не менялись. Изменён только локальный внешний QA-helper; эта source-only публикация содержит отчёт/документы, не helper DLL.

## Результат Valheim — FAILED

`ValheimQA/artifacts/20261020-201834/`:12setup PASS на .250; раннее закрытие render-only observer7/7 кадров, восстановленные камера/RT/cap/vSync;3отказа совместного владения камерой. Основной side2:52,072с,854host frames/854Camera.Render. До отказа полные2с окна дают11..14FPS. Потом производственный туман вышел в `Fog input queue is full; observation rejected, accepted history retained.`; только после отказа окна вернулись к36FPS. Max host gap289,32мс. В момент cleanup общий production RenderFrames493, включая7ранних; это не854успешных fog-кадра.

Первый проход не прошёл. Второй не запускался, финальные recovery-поля не проверялись. Подтверждение визуальной плавности/v11 отсутствует. Отдельный аудит правильно отклоняет cadence и подтверждает сохранность свидетельств неудачи; `evidenceAuditPassed=true` не означает `scenarioPassed=true`.

Так воспроизведён настоящий сбой пути обновление+отрисовка без PNG-записи. Какая доля задержки вызвана CPU/GPU/конкретным shader-путём, ещё не измерена.36FPS из[263](CHECKPOINT_263_RU.md) остаются только simulation cadence сRenderFrames0. FAILED343/520 в[262](CHECKPOINT_262_RU.md) не переписан в PASS.

## QA-helper и проверки

Добавлен opt-in `CadenceProbe.StartRendered(position,lookAt)` в существующую библиотеку ValheimQA. Один960×540RT, прежний Read(camera,target,null), одна отрисовка на наблюдаемый host frame, без ReadPixels/PNG/передачи кадра CPU. Старый Start() остаётся simulation-only. Взаимное владение запрещает одновременные still/recording/rendered cadence. После каждого вызова проверяется восстановление камеры; Finish возвращает cap/vSync и освобождает RT. Прежние65с/4096frames и критерий48..65с/2с windows30..40 не ослаблены.

Build helper0warnings/0errors; Python protocol/cadence7/7, включая отказ на нулевые/неполные renders и невосстановленные ресурсы. Native earlyStop/ownership и cleanup проверены. GPU execution timings и monitor presentation не измерялись. `verify` skill отсутствует; прямые проверки и независимый review выполнены.

## Изоляция

89прежних project-файлов побайтно неизменны. Отменён только собственный новый неоплаченный план2256a9f3074447808b643258c85672d3, revision2/checksum/identity проверены; инвентарь при cleanup прежний.4141обычный save-файл: состав/размер/mtime, не content hashes. Единственное дополнительное config-изменение — BepInEx-комментарий версии249→250, байтовое сравнение подтверждено; значения прежние. Time/weather pins восстановлены, normal logout/quit, bridge3restored. Grants/native construction/terrain/reset/другие профили/сеть не выполнялись.

QA BuildWorks SHA256 A5608BDC767CB49F538276AC9499FA1F4A4E5F4F6D0894AE63D3EFC12DB6113E остаётся прежним. Новая DLL/переустановка не требовалась.

## Следующее исправление и параллельная ветка

Read-only разбор обнаружил повторные обращения к одному и тому же block-key в sparse sampler `FogPublished.cginc`:8соседних ячеек внутри одного4³блока повторяют lookup. Предложено пропускать только повторные block-координаты, сохраняя первый уникальный порядок z/y/x, ошибочные/пустые страницы, выбранную page/UV и аппаратную фильтрацию. Это кандидат, **ещё не правка и не измеренное ускорение**. До native нужны CPUвыбор-page/границы/invalid-тесты и GPUbaseline/candidate parity; не уменьшать качество, не увеличивать очередь и не терять историю.

8.7→8.8: архитектурный обзор исправил потенциально неправильный сценарий. Отмена завершает видимую стройку после сохранённой выдачи drops, до их подбора; получение/очистка продолжаются без таблички. Предлагаемые rev3 returns-pending/rev4 settled пока Draft. Прежний all-collected-gate отклонён; [проект протокола](PROJECT_CANCELLATION_PROTOCOL_RU.md). Общий скан .250 уже есть, сами переходы ещё не реализованы.

Локальные доказательства: `artifacts/checkpoint-264/` native-failure.json/native-audit.log/helper-source.json/source/build/protocol logs; сценарий и отчёты в указанном QA run.14разделов/115ID/16частных галочек сохраняются; новых закрытых требований нет.
