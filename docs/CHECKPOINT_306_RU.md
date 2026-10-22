# 306 — причины паузы в исходниках; проверка recovery подготовлена

22.10.2026. **Локальный кандидат0.19.274, НЕ установлен.** Установлена и проверена
QA0.19.273 из [305](CHECKPOINT_305_RU.md), SHA
`2A5F8CBAB7D540592BDB0F8F2712913D6FDFE772B9AAAFEEEF5AFA029155CBD4`.
Игра владельца в Default-Compat-Test не изменялась. Второй Valheim не запускался.

## Исправление причины паузы

В304 при отсутствии верстака HaveRequirements(CanBuild) возвращал false, а
BuildWorks сообщал «не хватает ресурсов». В кандидате .274 отказ объясняется
в том же порядке, что native Player.HaveRequirements: требуемая ремесленная
станция с учётом NoWorkbench → недоступное DLC → материалы. Имя станции
локализуется. Проверка noCost/HaveRequirements, разрешение установки и расход
не менялись. Причина используется и обычной серией, и ProjectPart.StopBeforeNative.

Actual-method9PASS: отсутствие/наличие станции, исходная позиция игрока и имя,
NoWorkbench, DLC и порядок причин, fallback имени без Localization. Этот тест
включён в HostContract. Normal/QA build0warnings/0errors, HostContract и
localization1129PASS; независимые correctness/Ponytail reviews PASS.
Первый localization запуск до сборки обнаружил ожидаемый stale embedded catalog;
после пересборки каталоги совпали. Изменённый текст в настоящем UI ещё не проверен.
verify skill отсутствует. Нового игрового доказательства или установки306 нет.

## Подготовка следующего recovery-сценария

В отдельном локальном ValheimQA/render-probe подготовлен scoped one-shot Harmony
prefix на приватный ConfirmCredit перед записью подтверждения. Только exact
QA project/operation/index/immutable entry с CreditIntent и правильными full
inventory/receipt/profile proof приводит к deliberate interruption. Prefix не
меняет journal/profile/source; перед throw разоружается. Disarm работает без
загруженного мира, снимает только свой Harmony ID и разоружает при ошибке cleanup.
Actual prefix/status/disarm23PASS и независимый review PASS. Native injection
и восстановление после перезапуска НЕ доказаны. Это не power-loss/kill-проверка.

Будущий тест: свой новый paid payout → E выбранного источника → triggers1,
сохранённые AfterBytes/receipt и всё ещё CreditIntent/источник → normalQuit →
новый процесс без hook → E того же источника без второй вставки → CleanupSaved
и ещё один fresh replay. Старые settled проекты не восстанавливать. Нельзя
объявлять injection успешной без фактически retained intent и triggers1.

Ostrix играет в Default-Compat-Test. Readonly psutil подтвердил соответствие
профиля PID57236. Прежние Prepare/Restore/Deploy guards сохранены; игровые действия
не запускались. По вопросу владельца исследован отдельный parallel режим:
DirectPath допускает отсутствие stopProcessName и сохранение собственного
PID+starttime; нынешний name scan может принять чужой процесс за external instance.
Нужны pinnedPID/endpoint proof, запретgames_stop/games_kill и отдельная проверка
общих Steam/Unity preferences/дисплея/нагрузки. Режим ещё не реализован/не принят.
Обычные сохранения владельца во время игры закономерно меняются; прежний аудит
unchangedordinary не является проверкой параллельного QA.

Подробная локальная подготовка: artifacts/checkpoint-306/PREPARATION_RU.md,
build/probe/requirements/host/localization reports. Список115ID/16галочек/14разделов
сохранён. Полная цель остаётся незавершённой; native306, свойства предметов,
остальные fault windows, UX и сеть открыты. Поверхность303 и EarthWorks прежние.
