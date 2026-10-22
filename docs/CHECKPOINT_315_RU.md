# 315 — .279: возврат после сохранённого прерывания и полного перезапуска

22.10.2026. Продолжает8.6–8.8 полной цели, не закрывает остальные обязательства.
Файловая подготовка peer provenance313 включена; .278 отдельно не устанавливалась.

## Исправление

На .277 pending recovery ошибочно требовала повторную raw сериализацию live inventory.
Реальный native случай314: сохранённые9412 сотых прочности Hammer после native Load
дают float94.1199951, следующая запись —9411. Исходный canonical .fch и receipt точны.

Новый `BlueprintStoragePlayerProfile.TryReadSavedCredit` проверяет live raw pair либо
точную существующую native Load-проекцию. Дисковое ожидание всегда оригинальные
`credit.AfterInfo/AfterBytes`; полный parser проверяет inventory и customData из
canonical Local .fch. До/после чтения сохраняются thread, Local identity/profile/path,
inventory reference, source/generation/custody и loaded receipt. Журнал проверяет
canonical receipt и принимает полное настоящее evidence. Нет Save, AddItem,
подмены After-пары или допуска по прочности. Fresh insertion и settled replay прежние.

## Автоматические проверки и обзор

- Normal и QA .279:0 warnings/0 errors.
- Полные StoreTests и GeometryTests, EditorBridge, HostContract,1132 ключа локализации PASS.
- Actual profile reader/parser/Load comparator в существующем CPU host-substitute стенде:
  51 load assertions, в том числе отдельные9412→native float→9411, extra wear,
  altered canonical disk и14 мутаций свойств. Весь прежний стенд PASS.
- 17 actual recovery-branch checks PASS. Отрицательный контроль реального reader
  `InventoryMatches()=>true` обнаруживает приём extra wear; штатный стенд затем пересобран.
- Независимые correctness/Ponytail: Ship. `verify` отсутствует.

Стенд .NET сначала давал9412 при умножении float перед truncation; наблюдение в Mono
давало9411. Только inert serializer стенда удерживает промежуточную точность через
double перед int для воспроизведения этого случая. Это модель наблюдаемого поведения,
не полная эквивалентность native serializer. Failed/corrected логи сохранены; проверку
не ослабляли. Отдельный CS0165 fixture rows assignment устранён до финального PASS.

## Установка и Valheim

Только guarded Deploy-TestBuild в закрытый TerrainRamp-1.0-Test.
QA DLL SHA256 `C7708CC1152E458AB7816C0E0AD4D7691A0B4F90090F838B385248C26C470D9B`.
Geometry SHA256 `3075C034BA872C08100D4223BA5A2E08919082C075AD222A1C43E4181862C50B`.
Normal DLL `C13FFBC1A5DCFF2D4DDAC75D849A3DC8C8991A228A9BAACFE119ED289B9ED4FE`.

Cloud=false/qaSaveRoot=true, прежние BW_QA_43d3719a/world1137745799/player376043676.
Ни Default-Compat-Test, ни основной мир не использованы. Grants/reset/terrain/network нет.

1. Run181810/PID56436: прежний завершённый314 загружен с67Wood/10receipts/no sources.
   Новый собственный двухстенный проект `7e7d1a0eebaf4e4dbd440056f83d2eb7` создан обычным
   plan confirm; E/табличка → достройка4Wood67→63 → подтверждённая отмена без native drops
   → отдельная sealed выдача/сохранённый terminal. Hook306 index1 сработал ровно один раз
   после native profile save и до journal confirmation:65Wood/11receipts/Intent/sources1+1.
   Реальный verifier315 подтвердил полную saved пару с hook payload SHA512, затем ещё
   раз после normal Quit. Snapshots/хеши/отчёты в `artifacts/checkpoint-315`.
2. Run182521/PID47552: fresh process сохранил Intent65Wood/11receipts. Обычный E index1
   на .279 без compatibility helper314 подтвердил кредит, SaveAttempts0/Wood65,
   штатный cleanup2. Index0 затем подобран обычным E:67Wood/12receipts/sources0+0,
   обе строки ProfileCredited/CleanupSaved. Никакой повторной вставки index1.
3. Run183031/PID32840: ещё один полный запуск сохранил67Wood/12receipts/31stack,
   оба источника отсутствуют, Failure=null. Новых эффектов не вызывали.

Каждый процесс: normal logout→main_menu→Application.Quit; фактическое отсутствие PID
проверено вне sandbox; bridge3 восстановлен с хешами. Все76 прежних data-файлов проектов
не изменились; дополнительно прежний baseline314 из115 файлов, включая44 lock, прежний.
Пять завершённых data-файлов нового проекта не изменились при третьем запуске.
Полный прямой inventory response третьего запуска сохранён отдельно: test runner
обрезает длинный step result, поэтому его `passed` не объявляется полным snapshot.

## Границы и следующий шаг

Проверено контролируемое исключение после сохранения и нормальный выход/полные процессы.
Не проверены отключение питания, старый откатанный мир, другие реальные prefab/свойства,
полный инвентарь и освобождение ячейки, адресные native отказы preflight, сеть или весь HUD.
Ограниченный Wood-сценарий не закрывает8.7/8.8 целиком. Следующий срез — ранние отказы
и полный инвентарь, без новых grants/reset и без повторной выдачи старых проектов.
Peer transfer313 остаётся только файловой частью13.7; новые версии native26/portable15
не следует открывать старым бинарником. Ни binary release, ни saves не публикуются.

Source checkpoint ограничен указанными правками и не синхронизирует всю рабочую папку: прежние локальные различия тумана/Unity-стенда оставлены за его пределами. QA собрана из текущей рабочей папки; её embedded v11 asset совпадает с проверенной .277. Отдельная mirror-сборка проверяет компилируемость опубликованного source-среза, но не заменяет native доказательство рабочего QA-бинарника.
