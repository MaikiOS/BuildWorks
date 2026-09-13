# Установка BuildWorks 0.19.33 alpha

## Перед установкой

1. Закройте Valheim и сервер Valheim.
2. Сделайте резервную копию мира и персонажа.
3. Установите BepInExPack Valheim 5.4.x.

## Ручная установка

Распакуйте архив так, чтобы файлы находились здесь:

```text
Valheim/
└─ BepInEx/
   └─ plugins/
      └─ Ostrix-BuildWorks/
         ├─ BuildWorks.dll
         └─ BuildWorks.Geometry.dll
```

Не устанавливайте DLL Geometry отдельно и не оставляйте рядом старую копию `BuildWorks.dll`.

## Проверка загрузки

После запуска в `BepInEx/LogOutput.log` должна появиться строка:

```text
BuildWorks 0.19.33 loaded.
```

Если интерфейс не появился, приложите полный `LogOutput.log`, версию Valheim и список установленных модов.

## Удаление

Закройте игру и удалите только каталог:

```text
BepInEx/plugins/Ostrix-BuildWorks/
```

Сохранённые чертежи не следует удалять до отдельного резервного копирования.

