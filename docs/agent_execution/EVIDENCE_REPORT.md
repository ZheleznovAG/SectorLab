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
| Android device | `adb devices -l` || Текущая ADB-проверка |
| Человек / баланс / performance / clean checkout | Не выполнялись | NOT_RUN | Нет |

Первые CLI/ADB-проверки в песочнице были недоступны.

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
промежуточному коммиту по отдельности. Совокупный код зафиксирован в
`ec1a94494dcb6a08f857d26de11618ebbb0391ac`: при завершении аудита SHA-256 всех
20 C# файлов совпали с проверенным входом, Unity/Packages/ProjectSettings
не содержат незакоммиченных изменений. Результат APK — incremental build,
не проверка clean checkout; фактический размер APK берётся с диска,
а не из суммарного размера всех промежуточных файлов BuildReport.

## Acceptance gates

| Gate | Статус | Что подтверждено / что остаётся |
| --- | --- | --- |
| G0 | PASS | Аудит, backup, тесты, APK выполнены; исходные fixes сохранены в `9853006`, `970827a`, `ec1a944`; контракт/аудит — `d15a009`, завершение учёта — текущий docs-коммит |
| G1 | PASS | MatchState/Clock, 29 EditMode + 43 PlayMode, Main pause/result/restart и осмотренный экран Victory 45:0 |
| G2 | PASS | A/B/C capture+score+heal; 46 EditMode + 61 PlayMode; 18/18 NavMesh, Main Victory 45:0 и repair/contest |
| G3 | PASS | AI 53/53 EditMode, 67/67 PlayMode, четыре полные партии Main и наблюдаемый выигрыш/проигрыш |
| G4 | TODO | VContainer отсутствует |
| G5 | TODO | Addressables/UniTask/respawn/async cleanup отсутствуют |
| G6 | TODO | Нет жестов камеры, UI blocking и нового HUD |
| G7 | TODO ||
| G8 | TODO | Есть APK baseline; финальный content+APK и Android performance впереди |
| G9 | TODO | Итоговых v1.0 документов/лицензий/учебной карты пока нет |

Не приравнивать обнаружение устройства к Device verified, сборку baseline к
APK built v1.0, а автоматический PlayMode-прогон к Playtest verified.

## W1 — модель и интеграция

Правила/clock: `f74d44c`, `99da84a`; интеграцию смотреть через
`git log -1 -- Assets/_Project/Scripts/Battle/BattleController.cs`. Полные входные хеши и реальные UTC запуска — в JSON W1.3:
[EditMode 29/29](evidence/tests-W1.3-editmode.json),
[PlayMode 43/43](evidence/tests-W1.3-playmode.json). Старые четыре death-outcome случая удалены как противоречащие v1.0,
пять новых PlayMode случаев проверяют отсутствие death-outcome, pause и finish.
[Main observations](evidence/W1-main-observations.txt) получены из живой сцены
через CLI: pause, технический score outcome, Restart. [Game View](evidence/W1-main-victory.png)
проверен визуально после исправления обрезанного счёта и повторной регрессии. Обычные очки от узлов ещё не подключены. APK W0 содержит baseline, не W1.

## W2 — реальные узлы, команды и эффект

Правила: `3a2a9c7`; сцена: `3d2ab70`; команды: `a578f6f`; счёт: `b8e5cd7`;
ремонт — `git log -1 -- Assets/_Project/Scripts/Units/Health.cs`.
Итоговые [46/46 EditMode](evidence/tests-W2.5-editmode.json) и
[61/61 PlayMode](evidence/tests-W2.5-playmode.json) с UTC/хешами входов.
Ранние FAIL-логи W2.1/W2.4 сохранены, сравнение дробных величин исправлено на
допуск 1e-6. Main проверялась через CLI; техническое Warp использовалось только
в проверочном сценарии, игровые команды по tap идут обычным NavMesh-путём.

- [18/18 подходов и захват](evidence/W2-scene-check.txt), [Game View](evidence/W2-owned.png).
- [45:0 от A/B](evidence/W2-score-check.txt), [результат](evidence/W2-score.png).
- [C: +18 HP за 3 с, без лечения на pause/contest](evidence/W2-heal-check.txt), [спорный C](evidence/W2-heal-contested.png).

Компиляция прошла, groundTruth consoleErrors=0 после финальной проверки.
AI и respawn ещё отсутствуют; это не полный плейтест и не финальная Android-сборка.

## W3 — соперник и полный игровой цикл

Правила: `359778b`; runtime/сцена: `116bb28`; сценарии/прибытие группы —
`git log -1 -- Assets/_Project/Tests/PlayMode/MainMatchTests.cs`.
Проверки: [53/53 EditMode](evidence/tests-W3.1-editmode.json),
[67/67 PlayMode](evidence/tests-W3.3-playmode.json),
[реальный AI в Main](evidence/W3-main-ai.txt), [Game View](evidence/W3-main-ai.png).
Четыре партии Main с обычными move/attack и ускорением timeScale=4 завершились:
[CSV](evidence/W3-main-matches.csv). При удержании A и обходе Scout на B
получена Victory 45:8,2. Остальные три плана дали Defeat. Начальная серия до
исправления группового прибытия сохранена отдельно; её результаты не скрываются.
Это автоматические игровые сценарии, не Playtest verified. После respawn/карты
баланс нужно повторить; конечный Android build и человеческая проверка впереди.
