# 289 — отмена до подбора: проверен слой сохранения

21.10.2026. Локальная **0.19.266**, не установлена. В QA остаётся .265. Ostrix играет в своём профиле: Valheim/Unity/GPU не запускались, игровой ввод/DLL/сохранения не менялись. [English](CHECKPOINT_289.md).

## Что изменено

`ProjectStore.CancelPaid` закрывает оплаченный план после точных RemovedSaved/IssuedSaved, текущей construction-only absence и профиля плательщика. Получение возврата не является условием. Под существующей блокировкой сначала сохраняется optional TerminalBinding к неизменной выдаче, затем прежний plan rev2. Старые plan rev1/rev2 и payout без binding сохраняют прежний формат/байты. Поля Collections/cleanup не входят в отпечаток выдачи.

Если второй commit отказывает, план остаётся активным с frozen binding: новая выдача/получение не разрешаются, повтор может завершить тот же переход. После terminal read разрешены только точные прежние removal/issuance; новые removal/issuance запрещены, получение и очистка продолжаются без нового плана. Новые операции не допускают world generation ниже самой поздней известной issuance/terminal/cleanup. Исторический cleanup проверяется относительно своей issuance; точный replay не переписывается.

## Проверки

- Targeted `--construction-payout`: **172 assertions PASS**. Плохие revision/op/player/world/marker/parts/generation/profile не меняют файлы; immutable fingerprint чувствителен к исходным данным, независим от Collections/revision. Проверен отказ второго commit через FileShare и retry; закрытие до первого pickup; неизменность snapshot/TRS/part IDs; повтор; rollback/missing payout; запрет повторной выдачи; credit/cleanup после terminal.
- Найденный независимым review blocker устранён: старый cleanup9→10 не инвалидируется close11; old9 replay неизменен, NEW9/10 refused, NEW11 allowed. Свидетельства в тесте подставные типизированные, не native доказательство.
- Полный StoreTests PASS; normal/QA .266 **0 warnings/0 errors**. HostContract текущей игры и localization **1122 keys /1008 referenced** PASS. Helper RenderProbe CPU build0/0. `verify` skill отсутствует: выполнены прямое чтение diff, реальные CPU-проверки и два независимых read-only review (correctness и Ponytail) SHIP.
- Первая QA-сборка отказала `MSB3491: Access to the path is denied` в intermediate obj; тот же вызов с разрешением записи прошёл0/0. Исправлена ошибка компиляции `IEnumerable<uint>.DefaultIfEmpty(0)` → `DefaultIfEmpty(0u)`. Первый sandbox StoreTests отказал при временном fixture-save; разрешённый CPU-повтор прошёл. Это не сбои игры.

## Туман — подготовка без запуска

Локальный candidate shader/helper умеет diagnostic fallback-mask: exact existing fallback predicate выдаёт magenta/green, сохраняя low pass/depth taps, но пропуская дорогой повторный raymarch. Обычный режим сохраняет прежний путь. Подготовлены GPU assertions, но shader/bundle ещё не собраны и mask не запущена; измеренной доли fallback нет. Эти исследовательские shader/tool/helper-изменения не публикуются в source-only Git как принятый renderer. Установленный bundle не менялся.

Ostrix подтвердил дождь во время288, nativePNG его показывают. Погода не закреплена; не выводить причинный вклад дождя или чистое ускорение относительно другого запуска. Следующий GPU/native контроль только после освобождения игры, с записанными одинаковыми условиями; fallback пока гипотеза.

## Открыто

Native async caller свежего scan/profile/current generation, retry при старте, удаление fog/таблички и кнопка paid cancel ещё не подключены. Пункт8.8 частичный, полный цикл/ноль стоимости/права/сеть и художественная приёмка9.2–9.7 не завершены. Согласованный откат всех файлов без внешнего монотонного якоря не обнаруживается. Восстановление трёх DLL моста и ordinary-save metadata audit288 ещё отложены до закрытия игры; не ослаблять guard.

Артефакты: `artifacts/checkpoint-289/{verification.json,store-tests.log,build-normal.log,build-qa.log,host-contract.log,localization.log,REVIEW_RU.md}`. Все115 ID и галочки живого списка сохранены;8.8 уточнён без закрытия.
