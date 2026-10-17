<!-- docs-role: design record; reviewed 2026-10-17 -->
> Design/specification record: requirements below are not blanket implementation or acceptance claims. [Current state and queue](../docs/CURRENT_STATUS.md).

## 2026-10-14 — .184 WIP: происхождение целого документа, native23/portable12

Сохраняемая история LibraryOrigin v1: наблюдаемый сервер/мир, ID/ревизия серверной записи, видимость при получении, публикатор, SHA и версия исходного формата. Это справочная история, не серверные права/подпись. TrySaveAcceptedCopy создаёт новый личный ID, сохраняет данные/историю и исходник. Clone/update/настоящий controller Save As/share/ZIP/reload/external DetachedCopy проверены. Лимит16 не обрезается молча; malformed/future/unknown/duplicate поля и атомарные отказы проверены. PlanBuild предупреждает о пропуске истории; полный файл сохраняется рядом. Прежние native22/portable11 читаются, старые сборки новый формат не прочитают.

normal/QA0/0, полный Store, Host Steam25730771, loc1057/runtime loc, correctness PASSED/Ponytail Ship и exact-source Unity588 прошли. Unity2026-10-14T13:37:16Z/39 source hashes; старые UI-доказательства .183 сохранены отдельно в artifacts/server-library-wip/previous-ui-183 и checkpoint .183. Formatter135 прежних/0 новых. Evidence: artifacts/server-library-wip/origin-*.

**Игровой серверный интерфейс/приём личной копии ещё не подключены.** UI должен строить origin из проверенных точных read-result bytes/SHA до подготовки под host prefab. Не использовать transient placementImportId. Объединение происхождения при вставке частей другого документа тоже открыто. Следом один серверный источник обоих каталогов, admin-кнопки, принятие копии; после этого установка/host+remote/reconnect/restart. .184 WIP не устанавливать до готового сценария. Установленная .183 и ручная очередь группового обмена остаются актуальны; игру, модель, ввод, миры и saves не запускали. Полная цель ACTIVE и не завершена. История ниже.

## Verified checkpoint .183 — 2026-10-14

Batch import/export is implemented, automatically checked and deployed: common/per-document/original category, document exclusion, explicit skip report and one atomic publication. Batch export is full internal BuildWorks ZIP. Server library and in-game player transfer remain designed. See docs/BATCH_EXCHANGE_183.md and docs/MANUAL_QA_183.md. Earlier unimplemented status statements below are historical.

## Implemented checkpoint 2026-10-14

.181 adds pre-import categories; .182 implements/deploys automatic full JSON share files and Share in both catalogs. The primary library stays authoritative; share is outside scanned imports and retry never repeats Save As. Batch operations, server library and in-game player transfers remain unimplemented. Game acceptance is separate. Older statements below about missing auto files are historical. See docs/SHARE_FILES_182.md and its complete Russian duplicate.
# Blueprint library lifecycle — owner decisions, 2026-10-13

Status: the broad lifecycle remains designed; QA181 implements source/common existing/new category choice before import. The current store still
merges all files under imports. Server storage/access control, batch UI and
automatic share-file saving do not exist yet.

- Catalog entries come from explicit imports/user creation or an authorized server
  library, never merely from downloaded/source/test fixture folders.
- Category is a catalog section, distinct from the storage path. Import chooses
  a category without modifying source bytes or losing original category evidence.
- Saving a personal blueprint automatically updates a portable BuildWorks ZIP/JSON
  file in a separate share folder, using a stable ID and atomic replacement.
  Keep the existing internal library as the initial source of truth. A failed
  share-copy update reports partial success and retries only that copy.
- Share opens/copies the existing portable file path; export chooses target format,
  name and folder with an explicit limitations report. No automatic publication.
- Batch import reviews all sources and reports per-document outcomes. Start batch
  export with a full native BuildWorks ZIP/manifest; preserve provenance.
- Ostrix approved two server tiers: players/admins. Only admins publish/change
  shared entries. Players may read authorized entries and save personal copies.
  Enforce listing and file reads on the server. Personal saves are not auto-published.
  Library access never grants free construction or administrative restoration.
- Player-to-player sharing is separate: choose recipient, accept/decline, fresh
  personal identity and provenance; no implicit publication or construction.
  Server validates sender/recipient access, size/rate/duplicate limits. Admin-only
  blueprints cannot bypass recipient permissions through sharing.
- Implement local import lifecycle first, then category/share files, batch exchange,
  and separate bounded server-library operations. Existing contents/resource/v11/
  selection/network requirements remain part of the full goal.

Russian owner specification follows. It supersedes the historical early library
proposal retained only in the owner's canonical source tree.

---
# BuildWorks — библиотека чертежей в меню молотка

## Уточнение Ostrix — 2026-10-13: личная и серверная библиотеки

Этот раздел задаёт новый целевой сценарий. Он имеет приоритет над ранним
описанием ниже; автоматические переносимые файлы, групповые операции и серверная
библиотека ещё не реализованы. В .181 добавлен выбор исходных/общей существующей
или новой категории до импорта; автоматические проверки описаны в живом списке,
установка и игровая приёмка отмечаются отдельно.

- Каталог содержит созданные игроком чертежи, явно подтверждённые импорты и
  доступную ему серверную библиотеку. Скачанная папка с исходниками и набор
  тестовых файлов сами по себе карточек не создают. Прямое сканирование нынешней
  папки `imports` следует заменить учётом подтверждённых импортов без потери
  существующих пользовательских документов при миграции.
- Категория — раздел каталога, а не произвольный путь на диске. Перед импортом
  выбираются существующая или новая категория; для группы доступна общая
  категория с отдельными переопределениями. Исходная категория и сам исходник
  сохраняются в происхождении. Путь хранения показывается отдельно, доступны
  «Открыть папку» и «Копировать путь».
- Сохраняемый собственный чертёж автоматически получает актуальный переносимый
  файл BuildWorks в отдельной папке `share`, вне сканируемой `imports`.
  Использовать существующий полный формат ZIP/JSON и стабильный ID: повторное
  сохранение обновляет свою копию атомарно, «Сохранить как» создаёт новый ID.
  На первом этапе внутренний `blueprints.json` остаётся источником истины;
  переносимый файл — его проверяемая копия для передачи, не вторая карточка.
  Сбой её обновления явно сообщает «чертёж сохранён, файл для передачи не
  обновлён» и позволяет повторить только создание файла.
- «Поделиться» открывает или копирует путь к готовому полному файлу BuildWorks.
  «Экспорт» остаётся отдельным действием: другой формат, имя, папка и обзор
  ограничений. Никакой автоматической публикации или отправки другим людям.
- Групповой импорт показывает файлы, число документов, отсутствующие детали,
  настройки содержимого и ошибки до записи. Источники не меняются. Результат
  перечисляет импортированные и пропущенные документы и выбирает явно
  обозначенную карточку. Групповой экспорт сначала создаёт полный BuildWorks
  ZIP с манифестом; сторонние форматы требуют отдельной проверки потерь.
- Ostrix подтвердил ровно два уровня серверного доступа: «Игроки» и
  «Администраторы». Личные чертежи не публикуются автоматически. Ostrix также
  подтвердил: общую библиотеку публикует и меняет только администратор; игроки
  читают доступную им часть и сохраняют личные копии.
- Сервер проверяет список, чтение файла, публикацию, обновление и удаление.
  Скрыть административную карточку только в клиентском интерфейсе недостаточно:
  её файл также нельзя получить без прав. Административные чертежи восстановления
  сервера отделены от общедоступных. Права доступа к файлу не дают бесплатной
  стройки или восстановления административного состояния.
- Ostrix отдельно подтвердил обмен личными чертежами между игроками.
  «Передать игроку» выбирает получателя; получатель видит автора, название,
  состав/зависимости и принимает или отклоняет копию. Принятие создаёт личную
  запись с новым ID и сохранённым происхождением, не публикует общий чертёж
  и не начинает строительство. Сервер проверяет полномочия отправителя,
  доступ получателя и ограничения размера/частоты/повторов. Закрытый
  административный чертёж нельзя передать игроку через обход видимости.
  Вне игры остаётся передача готового файла через «Поделиться».
- Точки подключения: `CompositeBlueprintStore.TrySaveDocument/TryUpdateDocument`,
  `BlueprintExchangeFormat`, review импорта в `BlueprintExchangeView`.
  Серверного хранилища и протокола общей библиотеки в текущем коде нет.
  Сетевой слой реализуется отдельно после проверки локального маршрута.

Порядок: чистый QA-каталог → завершение проверяемого импорта → категории и
автофайл/«Поделиться» → групповые операции → серверный каталог и проверка прав.
Содержимое, учёт ресурсов, проект с колышком/v11 и прочие требования основной
цели сохраняются; этот план не объявляет их готовыми.
