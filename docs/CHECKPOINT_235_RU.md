# .235 — достоверность снимка крепления

19.10.2026 · [English](CHECKPOINT_235.md)

**Собраны normal/QA0.19.235; установлена QA .233.** Игра пользователя и GPU не
использовались; установленная DLL не заменялась. Туман дома этим не исправлен.

В `BlueprintItemStandCapture` обе финальные проверки теперь подтверждают ссылку
на тот же ZDO. Раньше сохранение пустого или заполненного крепления проверяло
ссылку на ZNetView и ревизию старого ZDO. Перепривязка того же view к другому
объекту с такой же ревизией позволяла принять устаревший снимок.
Новая проверка соответствует существующему `BlueprintMaterialState.TryCapture`.

Регрессия компилирует настоящий capture-source с искусственным read-only host
и настоящим Geometry serializer. На прежнем коде воспроизведён отказ теста:
`BUG: empty capture accepted a replaced ZDO with the same revision`.
После двух проверок ссылки прошли11 случаев: стабильные empty/full legacy,
смена ZDO после каждого чтения metadata, invalid view, изменение revision,
сохранность исходного состояния и прежний запрет потери ArmorStand state.
Это CPU-результат, не воспроизведение события внутри Valheim.

Проверки: normal/QA0warnings/0errors; HostContract1.0.20/Steam25909357,
localization1121, compiled QA/normal separation; независимый обзор PASSED.
Первый build отказал на sandbox/NuGet Access denied, повтор с доступом к кешу
прошёл. Новый IL guard учитывает оптимизацию ReferenceEquals в branch и проверяет
обе ветки собранной DLL. Формат, оплата, inventory и права не менялись.
Локальные доказательства: `artifacts/checkpoint-235/{repro-before.json,
repro-before.log,capture-checks.json,capture-checks.log,build-normal.log,
build-qa.log,host-contract.log,localization.log,qa-separation.log}`.

Параллельный ArmorStand-аудит: отдельный native компонент; pose, ordered slots и
индивидуальные itemData packages требуют собственного адаптера. Visual cache не
доказывает пустоту; stale package при itemID0 не является активной вещью.
Просто убрать guard означало бы потерять экипировку/позу. Он **сохранён**;
полная ArmorStand, native capture/reload и сеть пока не приняты.

Следом, когда GPU свободен: подготовленный `PollingCandidate` этапа234,
потом подтверждённое исправление и настоящий дом. Прежние неудачные прогоны
не повторять без изменения причины. Все14 блоков цели сохранены; goal active.
