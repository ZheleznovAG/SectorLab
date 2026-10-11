# Доказательства — аудит baseline 2026-10-11

Это результаты текущего прототипа R4/R5 и исходных исправлений; gates новой
v1.0 ими не закрываются. Среда: Windows, Unity Editor 6000.6.4f1, Android target,
CLI через локальный Pipeline 0.8.0-exp.1. Исходный HEAD:
`1c8754d8cca20bb342c6770c0985b4f14be27fa3` + шесть изменённых C# ниже.

| Проверка | Команда / способ | Результат | Лог |
| --- | --- | --- | --- |
| Compilation | `unity command --no-banner console_status` и `editor_status` | PASS: ready, compilationFailed=false, consoleErrors=0 | Фактические CLI-ответы в сессии |
| EditMode | `unity command --json --no-banner run_tests --mode EditMode --async_tests true`, затем `test_status` | PASS: 9 passed, 0 failed/skipped/inconclusive; 0,34 с | [Полный ответ](evidence/tests-editmode-1c8754d-dirty-20261011.json) |
| PlayMode | `unity command --json --no-banner run_tests --mode PlayMode --filter SectorLab.Tests --async_tests true`, затем `test_status` | PASS: 38 passed, 0 failed/skipped/inconclusive; 8,38 с | [Полный ответ](evidence/tests-playmode-1c8754d-dirty-20261011.json) |
| Android | `build --target Android --outputPath Builds/Android/SectorLab-baseline-1c8754d-20261011.apk --confirm true`, затем `build_status` | PASS: Succeeded, 0 errors, 1 warning | [Краткий BuildReport](evidence/android-build-1c8754d-dirty-20261011.json) |
| Git / LFS / meta | `git diff --check`, `git lfs version/status/ls-files`, поиск GUID в Assets meta | PASS: whitespace clean, LFS 3.7.1, PNG clean, duplicate GUID=0 | Аудит; повторная проверка перед коммитами |
| Backup / ZIP | Копирование 33 файлов с SHA-256; сравнение с последним ZIP без извлечения | PASS: исходные код/тесты/docs совпадают; инструкции/ТЗ новее ZIP | Локальные `backup-manifest.csv`, `working-tree.patch` |
| Android device | `adb devices -l` | Подключён Redmi Note 8 Pro; gameplay NOT_RUN | Текущая ADB-проверка |
| Человек / баланс / performance / clean checkout | Не выполнялись | NOT_RUN | Нет |

Первые CLI/ADB-проверки в песочнице были недоступны. После разрешения на доступ
к локальному редактору/ADB соответствующие команды повторены успешно; это не
ошибка проекта и не повод записывать выдуманный `BLOCKED_DEVICE`.

## Проверенный входной код

SHA-256 файлов на диске во время запуска тестов/сборки (LF). Источники при
фиксации не переписываются. Git может нормализовать окончания строк; при
сравнении checkout учитывать `.gitattributes` и `core.autocrlf`.

| Файл | SHA-256 |
| --- | --- |
| `Assets/_Project/Scripts/Player/UnitCommander.cs` | `71DAF77731C1C21B394FE0B703D6E59F4C7768FBE2A032F83BF837E1A39EF66B` |
| `Assets/_Project/Scripts/Units/UnitAttack.cs` | `1CE52FD35C0ACFD7AF28D9A9631605491E984A92D9849496E505C8FBE7E1B33E` |
| `Assets/_Project/Scripts/Units/UnitMovement.cs` | `74FE45073482518F7FA381BFA9FF61BFABB3F9C5048D4CE709C870A6DA3333AB` |
| `Assets/_Project/Tests/PlayMode/UnitAttackTests.cs` | `3B60AB7680D5B1C2E38744A11AD06522078ADC96D6727CD0F54F5CB4FD4928D6` |
| `Assets/_Project/Tests/PlayMode/UnitCommanderTests.cs` | `0583E7B5EB98135DEF45625DF6AFB2D80FA8F981641323CD319F3BA1BD7B2631` |
| `Assets/_Project/Tests/PlayMode/UnitMovementTests.cs` | `508EC295EF0FC0B022C01AA072ED8830930C607489C9F4D8BF2B6D2A5020925D` |

Полные результаты относятся к совокупности этих изменений, не к каждому
промежуточному коммиту по отдельности. Результат APK — incremental build,
не проверка clean checkout; фактический размер APK берётся с диска,
а не из суммарного размера всех промежуточных файлов BuildReport.

## Acceptance gates

| Gate | Статус | Что подтверждено / что остаётся |
| --- | --- | --- |
| G0 | IN_PROGRESS | Аудит, backup, тесты, APK выполнены; фиксация правок/документов продолжается |
| G1 | TODO | Нет нового матча по очкам/таймауту/паузе/Draw |
| G2 | TODO | Нет A/B/C и capture/score/heal |
| G3 | TODO | Нет AI за узлы и новой полной партии |
| G4 | TODO | VContainer отсутствует |
| G5 | TODO | Addressables/UniTask/respawn/async cleanup отсутствуют |
| G6 | TODO | Нет жестов камеры, UI blocking и нового HUD |
| G7 | TODO | Звук/понятность новой игры/живой плейтест ещё не проверены |
| G8 | TODO | Есть APK baseline; финальный content+APK и Android performance впереди |
| G9 | TODO | Итоговых v1.0 документов/лицензий/учебной карты пока нет |

Не приравнивать обнаружение устройства к Device verified, сборку baseline к
APK built v1.0, а автоматический PlayMode-прогон к Playtest verified.
