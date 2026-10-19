# .239 — измерения пропускной способности тумана

19.10.2026 · [English](CHECKPOINT_239.md)

Измерен прежний алгоритм, не новый способ симуляции. Отдельный `TelemetryMeasurement` —20с при target36FPS, без явного polling и дополнительного post-capture Pump. Прежние48с stress-режимы и их отказы сохраняются. Фактически20,024с/32,16hostFPS,59 кадров, maxQueue45, drain1,77с;140scalar/196field сравнения точны. Очередь растёт: это не устойчивость дома.

| Стадия за измеренное окно | Вызовов | Суммарное время на вызывающем потоке |
| --- | --- | --- |
| RestoreCreate |1964|252,47мс|
| PageRelease |1964|113,96мс|
| Upload SetPixelData/Apply |1964|1441,12мс|
| CopyTexture enqueue |1964|6,51мс|
| CallbackCopy, включая new ushort[] |1972|2197,34мс|
| Solver submission |1972|255,12мс|
| Submit, включает solver и прочую постановку |1972|454,73мс|
| Capture Camera.Render |59|63,60мс|
| ReadPixels/Apply |59|2802,19мс|
| EncodeToPNG |59|390,53мс|
| Запись PNG |59|41,56мс|

Эти интервалы не являются GPU execution time. ReadPixels может включать ожидание GPU. Submit включает Solver: строки не складываются в общий процент. Асинхронные ожидания параллельных владельцев также нельзя складывать и считать CPU-временем.

1972 velocity callbacks успешно скопировали1972 новых массива по4915200байт:9,6927744ГБ выделений payload. Это не оценка всей памяти и не доказательство скорости PCIe. В окне1972 batches,14020samples/15562ticks;1964 cached pages. Средние requested→callback-end около33,5мс, callback-end→accept около4,6мс; границы охватывают доставку/copy и обработку на потоке, не только GPU. Invalid/stale/thread mismatch/unobserved равны0. Три GC.CollectionCount delta равны1007 каждое; **не суммировать их** и не считать временем или числом полных пауз. Managed memory до/после85,45/166,14МБ — моментальные значения, не peak.

Observer ограничен32slots×2request kinds; метки включают owner/generation/sequence. Callback не равен accepted. Cached alias не создаёт фиктивного request/accept. Неверные наблюдения не бросают исключение в механику, но делают отчёт недействительным. Данные снимка отделены от накопителей. В normal сборке8 изменённых методов и handlers, включая callbacks, совпадают с .237 по инструкциям после удаления только числовых имён compiler-generated closure;0 observer/clock calls. В QA observer по умолчанию выключен, включается только до создания owners.

Проверки: Geometry с15 CPU observer assertions и прежними574 saved-world assertions; validator6режимов/1manifest/65negative; normal/QA0ошибок/0предупреждений; host1.0.20/Steam25909357, localization1122, QA separation и независимый correctness/ponytail обзор прошли. GPU20с и точные сравнения прошли; fault/retire/reuse/сеть этим окном не проверены. Шейдеры, bundle, стоимость, история,32/32768/Spatial3D/4resident и контроль переполнений не менялись.

## Следующий кандидат по данным

Create+Release заняли0,37с/20с, поэтому перестройка GPU texture pool не выбрана. Проверить bounded reuse CPU payload arrays: capacity committed pages плюс ResidentCapacity staging pages, максимум36×4915200=176947200байт для32/4. Сейчас managed RAM жёстко не ограничен, а VelocityRamBytes может дважды считать cached alias; новый запас должен учитывать все уникальные committed/pending/free массивы и проверять лимит до allocation.

Старую committed страницу и stamp сохранять до matching receipt и velocity acceptance; затем swap и возврат прежнего массива. Нельзя перезаписать страницу до подтверждения, вернуть cached alias дважды или дать позднему callback чужой reused buffer. При ошибке удерживать неопределённую аренду до GPU drain/teardown. Нужны CPU ordering/alias/late callback/fault/bounds tests, затем exact GPU reuse и устойчивый48с прогон; только потом native-дом. Это технический план, не реализованное исправление. Землю, ресурсный возврат и права не затрагивает.

Доказательства: `artifacts/checkpoint-239/` и `artifacts/fog-production-frames/TelemetryMeasurement-20261019-180336-89a3c1f8/`. Unity PID54820 завершён exit0, runner восстановил настройки. Игровая установка и native-проверка .239 фиксируются отдельно в живом списке. Миры/ресурсы в этом Unity-опыте не использовались. Все14 блоков цели сохранены; goal active.

**Установка .239:** guarded deploy и хеши PASS; QA DLL `EBB944D46849727A2EC7F6A4A60C2F4D4AFDB343B4EA2D3AA113A310CF0925BD`. Normal/QA/source builds, host/localization1122/separation прошли. В .239 Valheim ещё не запускался: измерение20с выполнено в Unity-стенде; это не native-дом. Игровые сохранения/ресурсы этим этапом не изменялись.
