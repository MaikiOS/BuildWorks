# v11: опубликованное состояние в mesh и объёмном шейдере

2026-10-17 · [English](FOG_PUBLISHED_RENDER_213.md) · [Живой список](LIVE_CHECKLIST_RU.md)

**Проверен отдельный consumer-путь Unity, не игровой дом.** `BUILDWORKS_FOG_PUBLISHED` читает один64-byte GPU record для clock/body/actor/active; mesh vertex/fragment берут опубликованный surface atlas. Static shape и dynamic fields имеют отдельные размеры/offset. Owner/generation сравниваются как точные64-bit значения; sequence с CPU не сравнивается. Перед чтением проверяются реальные размеры/stride буферов, region и slot. Неверная привязка сохраняет свою статическую форму и исключает чужие поля.

| Проверка | Фактический результат |
| --- | --- |
| Unity6000.0.61f1 / D3D11 / Linear | Candidate bundle3 assets/8 kernels,88727B; shader errors0 |
| Mesh-only / volume-only / combined | 87 captures,62 comparisons,2 владельца полного96×80×80 domain |
| Dense oracle против GPU publication | Максимум среднего RGB9.8727858e-8; максимум локального канала0.00048828125 |
| Устаревшие CPU clock/body/actor/active | Разница0: подтверждение CPU удерживается до camera.Render |
| Metadata-only / отказ packing | Изменение capsule/fractional clock без шага; отказ сохраняет прежний кадр |
| Owner low/high, generation low/high, slot; region/короткие buffers | 26 отрицательных случаев совпадают со static fallback; сосед сохраняется |
| Отрицательный keyword / generation | Разница0.0034708318 /0.0021120982; контроль чувствителен |
| Установленный bundle против candidate без нового keyword | 16 captures:mesh/single/pair/atlas, idle/contact; попиксельная разница0 |
| CPU | Normal/QA/Editor C#0 warnings/0 errors;129 групп geometry;363 повреждённых квитанции/manifest отклонены |
| Независимое чтение | Correctness PASSED, Ponytail Ship; повторного GPU-запуска ревьюером не было |

Камера фиксирована: perspective49°,384×216,position(4,2.3,-5),target(0,1,1.1),roll0,Deferred/HDR/depth;фон(.15,.2,.3),без отдельного tone mapping и без луча. Две принадлежащие проекту процедурные стены(.2,2,2) имеют origins(0,0,0)/(0,0,2.5),цвет(.64,.31,.12). Исходные снимки, `comparison-sheet.png`, `verification.json` и `sources.json` локальны в `artifacts/fog-published-render/`; камеры/поля не подгонялись под результат. Это техническая проверка эквивалентности; внешний вид дома Ostrix ещё не принимал.

При расширении проверки обнаружены surface-analysis ошибка `Unexpected identifier StructuredBuffer` и legacy delta0.018276. Unity успешно завершала сборщик, несмотря на shader error. Ошибка устранена: StructuredBuffer скрыт только от surface-analysis parser; реальные GPU passes читают его. Теперь проверяются ShaderUtil messages и журнал, а manifest связывает7 исходных shaders/compute с bundle. Неверные source/bundle hash и manifest отклоняются до GPU-прогона. Неудачный промежуточный вариант не устанавливался.

Команды: `scripts/Build-FogGhostAssets.ps1 -CandidateOnly`, затем `scripts/Test-FogSimulation.ps1 -PublishedOnly`. Candidate не копируется в рабочий resource. SHA candidate `A9CD597CACB133047E9C6C7EE29B55FA9308E3D6677F7DAB753AB4F0514848CC`. Установленная QA .213 и её bundle неизменны; Valheim/миры/инвентарь не затронуты.

Следующий законченный шаг: runtime arena и split atlas в существующих Channels/Simulation/Scratch/Atlas, затем admission реальной геометрии и пространственный ledger вместо YZ-only. Полный domain, бюджет всех живых состояний и безопасный отказ обязательны. Производственный scheduler/render lifecycle, дом28, FPS, многопользовательский проход и постоянная табличка пока не проверены. Новый consumer ограничен текущим accumulated-v11 с одним actor/trail[0]; прежние варианты остаются для legacy путей. Другие графические API этим D3D11-прогоном не доказаны.

Независимое чтение также обнаружило пропуск input/state при копировании C# сборщиком assets. Оба файла добавлены и сверяются по SHA; CPU-тест выполняет реальный цикл копирования в пустую папку. Оставшиеся от прежних запусков RuntimeSources не требуются.

### Runtime: явное заимствование полей

`BlueprintFogSimulation.StagedState` заменяет reflection-подмену временных полей и ledger в fixed-pool probe. Расчёт проверяет размеры/формат/UAV/Trilinear/noMip/ClampU/V/W и отсутствие alias до GPU-записей. Заимствованные поля не выделяются повторно и не освобождаются расчётом; стадия не может разрешить recovery по общему ledger. Committed velocity/ledger остаются у владельца пула. Обычный constructor/Dispose сохранены. `BlueprintFogScratch(includeReadFields:true)` единолично владеет всеми9 полями рабочего набора44236800B; прежние callers получают6 полей как раньше. Probe не освобождает read-поля отдельно.

Проверено:11 неверных наборов ресурсов отклонены без изменения sentinel; valid Dispose сохраняет borrowed ресурсы. GPU physics664/498/28,239commit/2отказа/242metadata;Frame236 кадра54/232/174/20/54;render87/62/26+legacy16/delta0; прежние Scratch360/36 и Recovery24 прошли на одном source SHA. Normal/QA/Editor0/0,129 групп,371 отрицательная CPU-проверка, независимые PASSED/Ship. Квитанции и fingerprints локальны в `artifacts/fog-borrow-213/`.

Это рабочий API расчёта, используемый проверкой, **не готовый игровой allocator/scheduler**. Сам pool пока остаётся Editor-стендом. Полная память дома, source admission, пространственный ledger и FPS ещё открыты; installed .213/bundle неизменны.
