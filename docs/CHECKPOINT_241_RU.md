# .241 — точный шейдер и native-стена; дом всё ещё ограничен очередью

19.10.2026 · [English](CHECKPOINT_241.md)

Шейдер не перечитывает metadata/clock для каждой зависимой выборки raymarch. Состояние читается для того же owner; проверки validity/field/stride/map/page до доступа к буферам сохранены. Прежний путь оставлен как GPU A/B keyword; включение только через QA/Editor метод. Ни математика v11, ни solver, число шагов, размеры полей, бюджеты и принятая история не менялись. Normal/default QA не включает эксперимент RAM reuse из .240.

## Доказательства и ограничения

- Unity:14 RGB-сравнений old/new с точным delta0; существующие dense/published, legacy и negative controls прошли. Первый вариант с немедленным return отклонён: изменил промежуточный float на2,45e-10 при одинаковых PNG; допуск не ослаблялся. В поставленный вариант он не вошёл.
- Нагрузочные48с: guarded metadata FAILED на37,011с; reuse state/clock FAILED на39,848с,35,937hostFPS, queue64.140scalar/196field не достигнуты. Это не сравнение Valheim FPS или GPU time; ускорение не доказано. Оба режима включают Editor-only RAM pool и async recorder. Повторы остановлены.
- Valheim .241/default RAM allocator: настоящий неоплаченный проект4 woodwall с табличкой. Подготовка7/7, четыре прохода59/59 за75,6с,482 кадра,467456 exact inner texels. На hold flags1/2 обозначают активный поток/смещение; после восстановления16/16 частей имеют flags0 и пустые опубликованные smoke/surface. Инвентарь идентичен, коллизии у призрака выключены. Это проверка стены, не дома, оплаты/возврата, reload или сети.
- Четыре MP4 имеют фактическую длительность по realtime; cameraRestored=true во всех кадрах. Контактный лист просмотрен: форма возвращается, вся стена не исчезает. Новый внешний вид не объявлен принятой Ostrix финальной приёмкой v11.
- Фоновая игра штатно вышла через logout/Quit; PID59004 завершён, мост восстановлен3/3.4137 основных save files совпали по размеру/mtime, их content hashes не собирались. QA-мир штатно сохранён. В конфиге поменялся только комментарий версии .237→.241; значения не менялись. Подготовка выдала2 Warning о необязательных Backpacks API; маршрут warnings/errors не выдал. Никаких ресурсов, terrain edits или catalog imports.

## Проверки и установка

Normal/QA builds0/0, host Valheim1.0.20/Steam25909357, localization1122, separation PASS.8 normal fog methods/handlers сохранили IL относительно .240,0 observer calls; QA36. CPU evidence suite1760 negative cases и production gate8 modes/1manifest/84 negatives PASS. Steady-tail gate теперь отвергает растущий диапазон очереди в последних двух5с окнах; реальные отказавшие прогоны до этого gate не дошли. Старый footprint в validator исправлен на4061136/18540048, уже подтверждённый .233/source assert; допуски не менялись. Независимый correctness/Ponytail review: блокирующих замечаний нет. `verify` отсутствует; это перечисленные проверки, не запуск отсутствующего skill.

Host check сначала отказал с `Unexpected BuildWorks artifact version: 0.19.241.0`: обновлена явная ожидаемая версия .240→.241, проверка повторена успешно. При сборе отчёта исправлены ожидание active flags на hold и разбор дробных секунд .NET в Python3.10; игровых повторов ради этого не было. Ошибки чтения несуществующего файла и literal wildcard rg/os123 не относятся к игре, команды исправлены.

QA SHA256 `4E1B7227DDDAF5E179850594928B905A1E077FEF4AED13B477D05135804D8DE9`; normal `82971C16C84BA2F1CC93DD3AA854EA13845D24F1735D7C620D8080F8AC64A537`; bundle `E734631FB7DE49C00237C1F0F4DBB28DFA491DF6DF177C7D79353BE354C4AA78`. Установлено только в TerrainRamp-1.0-Test защищённым deploy. Публичный binary release не создавался.

## Пути и следующий шаг

- `artifacts/checkpoint-241/`: точный render receipt, два failed48s receipts, bundle manifest, сборки, validators, native.json и source manifest.
- `../ValheimQA/artifacts/20261019-191840/`: SCENARIO_RU.md, два полных test reports,8 metrics,4 PNG sequences/MP4, wall-contact-sheet.png, native verification/logs/restore.
- `artifacts/fog-production-frames/AsyncCaptureCandidate-20261019-190421-e9bc3089/` и `AsyncCaptureCandidate-20261019-191307-bb06e2c2/`: raw failures/trace/own process exits.

Архитектор предложил bounded residency quantum2: считать только committed receipts, не вытеснять owner до второго пакета при наличии очереди и пропустить первый speculative velocity readback. Пустая/удалённая запись не должна удерживать слот; сначала тесты справедливости/empty/remove/reject, затем прежние exact GPU и48с. **Не реализовано.** После устойчивой нагрузки — native-дом. Оплаченная отмена/выпавший возврат, специальные объекты и сеть остаются открыты; все14 блоков цели сохранены, Goal API active.

При промежуточном обновлении живого списка неверный разбор CRLF сократил локальный файл. Полные14 разделов восстановлены из проверенного HEAD до синхронизации, затем заголовки сравнены1:1. Короткая копия не отправлялась. Git whitespace check нашёл3 CRLF-строки в новом include; нормализация только переводов строк и повторная сборка дали тот же bundle SHA, поэтому native/GPU доказательства относятся к тем же байтам.

Первый запрос схемы GABS использовал неверный аргумент: Unknown argument toolName. Allowed arguments: gameId, tool. До игровых действий прочитана правильная живая схема. Этот отказ не выполнял мутаций.
