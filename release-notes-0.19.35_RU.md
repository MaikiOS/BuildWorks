# Исходная альфа BuildWorks 0.19.35

[English](release-notes-0.19.35.md) | **Русский**

Эта source-only alpha исправляет найденную в 0.19.34 ошибку runtime-токенов Valheim. Dotted ID остаются читаемыми в каталогах, но на границе Valheim преобразуются в ключи только с подчёркиваниями.

- 596 English и 596 Russian строк;
- 596 уникальных Valheim-safe runtime-токенов;
- явный English fallback для missing-ответа `[key]`;
- отдельный regression-тест регистрации English/Russian;
- Release 0/0 и Unity Workbench 81/81 PASS.

Сборка установлена только в отдельный тестовый профиль владельца и требует повторного игрового smoke English/Russian. Бинарный release asset не подготовлен.
