# Установка и проверка BuildWorks

[English](INSTALL.md) · [Текущие исходники/QA/публичный пакет](CURRENT_STATUS_RU.md)

Для публичной установки берите явно опубликованный пакет из [GitHub Releases](https://github.com/MaikiOS/BuildWorks/releases), с Valheim и BepInExPack Valheim5.4.x. Последний ZIP0.19.37 alpha — исторический; функций исходников .206 в нём нет. Совместимость проверяется для выбранного пакета/игры; доказательства host.206 не подтверждают .37 на новой игре.

Закройте игру/сервер, сохраните резервную копию своего мира/персонажа. DLL устанавливаются согласованной парой:

```text
BepInEx/plugins/Ostrix-BuildWorks/BuildWorks.dll
BepInEx/plugins/Ostrix-BuildWorks/BuildWorks.Geometry.dll
```

Не оставляйте вторую старую DLL в plugins. В `BepInEx/LogOutput.log` должна быть строка `BuildWorks <установленная-версия> loaded.`; сверяйте реальный номер. При отсутствии UI нужны полный лог, версия игры и список модов.

## Разработческая QA на компьютере Ostrix

Разрешён только `TerrainRamp-1.0-Test`. `Default/BuildWorks` — исходники; в `Default/BepInEx/plugins` BuildWorks быть не должно. Используйте `scripts/Deploy-TestBuild.ps1 -TargetProfile TerrainRamp-1.0-Test`, сначала с `-WhatIf`. Прежний изолированный стенд использует `-QaStandToken <existing-token>` после сборки с `-p:BuildWorksTestStand=true`; не создавайте/не сбрасывайте token и saves ради заполнения каталога. Скрипт проверяет хеши и отказывает при работающей игре/другом профиле. Установка сама по себе не разрешает запуск/изменение мира.

Запуск: Thunderstore → TerrainRamp-1.0-Test → **Modded**, затем только прежние разрешённые QA-мир/персонаж. Прямой запуск Steam/game запрещён. QA.206 установлена, в игре не запускалась. При работающем Valheim DLL не заменяется.

## Данные и удаление

Обычная библиотека: `BepInEx/config/BuildWorks/blueprints.json`; переносимые файлы: каталог BuildWorks, `share/<id>.buildworks.json`. QA перенаправляет данные в существующий изолированный корень. Точный путь даёт игровое «Поделиться»; профиль не угадывать. Папка share сама не импортируется и не публикуется сервером.

Перед удалением закройте игру и сохраните чертежи/share. Удаляется только `BepInEx/plugins/Ostrix-BuildWorks`; уже построенные объекты мира это не отменяет. Миры/персонажей/другие профили не удалять.
