# Доказательства SectorLab v1.0

## Текущее продолжение после аудита, 12–13.10.2026

Игровой код зафиксирован в `4a06ae3`: исправлены роли независимо от имён ассетов,
приказ через союзника на узле, компактный HUD/доход/эффекты, назначения ролей,
разные SFX и подписи событий. Коммиты `8cb198a`, `ee23f2b`, `909c48a`,
`468e6db`, `dc16223`, `4a06ae3` — настоящие локальные изменения, push не выполнялся.

**63/63 EditMode, 124/124 PlayMode PASS**, failed/skipped/inconclusive=0:
[EditMode](../evidence/tests-A5-full-editmode.json),
[PlayMode](../evidence/tests-A5-full-playmode.json). Все 296 input hashes совпадают
с [сборкой APK](../evidence/android-build-A5-feedback.json).
[Обычная Main](../evidence/A5-editor-smoke.json): Ready, 6 units, consoleErrors=0;
[обратная связь](../evidence/A5-feedback-16x9.png) просмотрена в отдельном capture fixture.

APK `Builds/Android/SectorLab-A5-feedback.apk`: **32 311 981 bytes**,
SHA-256 `8257C9B1EC109272A3027CD6146BE9790657D085651B856983225797E3A8BE83`.
0 build errors, 1 Pipeline warning. [Установленный APK](../evidence/A6-phone-install.json)
выгружен с физического Android / Android 11 и имеет тот же hash. На момент этого evidence процесс/Activity есть, системный keyguard закрывает
видимую игру. G7/G8 IN_PROGRESS; **Release ready не заявляется**. Все ранние неудачные проверки
сохранены и объяснены в [статусе](JOURNAL.md).

## Предыдущий checkpoint до аудита

Текущее проверенное игровое состояние: **`0ae98c8`**, включая исправление
SelectionRing. Повторная полная регрессия: **63/63 EditMode, 112/112 PlayMode**,
failed/skipped/inconclusive = 0. [EditMode](../evidence/tests-W8-selection-final-editmode.json),
[PlayMode](../evidence/tests-W8-selection-final-playmode.json): sourceHead `0ae98c8`,
реальные UTC и SHA-256 входов сохранены. Чистый public clone без project Library:
[63/63 EditMode](../evidence/clean-clone-W8-editmode.xml),
[112/112 headless PlayMode](../evidence/clean-clone-W8-full-retry.json) после
исправления ожиданий тестов в `acbd9bf`.

APK `Builds/Android/SectorLab-W8-selection.apk`: **1.0.0**, 32 302 469 bytes,
SHA-256 `654CB514B441F9B8C18B61C93AEED701771D6DE5A3F8E7CCB6ED0FD1081237DA`.
[Сборка](../evidence/android-build-W8-selection.json): Succeeded, 0 errors,
1 Pipeline warning, 40,752 с; локальные AA bundles/catalog включены в APK. APK установлен на Android / Android 11.

Возобновление после прерванной сессии: **63/63 EditMode, 112/112 PlayMode PASS**
на неизменных Unity-входах стартового `5362099`;
[EditMode](../evidence/tests-W8-emulator-resume-editmode.json),
[PlayMode](../evidence/tests-W8-emulator-resume-playmode.json).
[Новый обычный Editor smoke](../evidence/W8-emulator-resume-editor.json): естественный
Defeat 0:45, consoleErrors=0, [снимок](../evidence/W8-emulator-resume-editor.png).

**Android Emulator API34: v1.0 launch FAIL**, ARM native bridge отвергает SEVL
в libunity.so. Первоначально запущенная игра оказалась baseline 0.1.0, что
подтверждено хешем выгруженного APK. Повторный baseline также падает. G7/G8 остаются открыты.

Старые прогоны выполнены до privacy rewrite; соответствие коммитов и неизменность
кода подтверждены [операцией очистки](../evidence/privacy-history-published.json)
и [картой SHA](../evidence/privacy-commit-map.txt). Исторические SHA ниже и sourceHead
старых JSON оставлены как provenance; это не вымышленные повторные запуски.

## Исторический аудит baseline 2026-10-11

Это результаты текущего прототипа R4/R5 и исходных исправлений; gates новой
v1.0 ими не закрываются. Среда: Windows, Unity Editor 6000.6.4f1, Android target,
CLI через локальный Pipeline 0.8.0-exp.1. Исходный HEAD:
`1c8754d8cca20bb342c6770c0985b4f14be27fa3` + шесть изменённых C# ниже.

| Проверка | Команда / способ | Результат | Лог |
| --- | --- | --- | --- |
| Compilation | `unity command --no-banner console_status` и `editor_status` | PASS: ready, compilationFailed=false, consoleErrors=0 | Фактические CLI-ответы в сессии |
| EditMode | `unity command --json --no-banner run_tests --mode EditMode --async_tests true`, затем `test_status` | PASS: 9 passed, 0 failed/skipped/inconclusive; 0,34 с | [Полный ответ](../evidence/tests-editmode-1c8754d-dirty-20261011.json) |
| PlayMode | `unity command --json --no-banner run_tests --mode PlayMode --filter SectorLab.Tests --async_tests true`, затем `test_status` | PASS: 38 passed, 0 failed/skipped/inconclusive; 8,38 с | [Полный ответ](../evidence/tests-playmode-1c8754d-dirty-20261011.json) |
| Android | `build --target Android --outputPath Builds/Android/SectorLab-baseline-1c8754d-20261011.apk --confirm true`, затем `build_status` | PASS: Succeeded, 0 errors, 1 warning | [Краткий BuildReport](../evidence/android-build-1c8754d-dirty-20261011.json) |
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
| G4 | PASS | VContainer scene scope, настоящая injection, missing registration/reload tests — W4 |
| G5 | PASS | UnitFactory/UniTask/Addressables, late cancel/error/respawn/10 restarts и локальный контент APK — W5 |
| G6 | PASS | Touch/camera/UI blocking/HUD/safe area/tutorial; пользователь подтвердил жесты и Insets API проверен на телефоне — W6 |
| G7 | IN_PROGRESS ||
| G8 | BLOCKED ||
| G9 | PASS | README/8 ADR/notices/11 упражнений; links/JSON/reachable SHA/Unity inputs проверены; ограничения G7/G8 явно перечислены |

Не приравнивать обнаружение устройства к Device verified, сборку baseline к
APK built v1.0, а автоматический PlayMode-прогон к Playtest verified.

## W1 — модель и интеграция

Правила/clock: `f74d44c`, `99da84a`; интеграцию смотреть через
`git log -1 -- Assets/_Project/Scripts/Battle/BattleController.cs`. Полные входные хеши и реальные UTC запуска — в JSON W1.3:
[EditMode 29/29](../evidence/tests-W1.3-editmode.json),
[PlayMode 43/43](../evidence/tests-W1.3-playmode.json). Старые четыре death-outcome случая удалены как противоречащие v1.0,
пять новых PlayMode случаев проверяют отсутствие death-outcome, pause и finish.
[Main observations](../evidence/W1-main-observations.txt) получены из живой сцены
через CLI: pause, технический score outcome, Restart. [Game View](../evidence/W1-main-victory.png)
проверен визуально после исправления обрезанного счёта и повторной регрессии. Обычные очки от узлов ещё не подключены. APK W0 содержит baseline, не W1.

## W2 — реальные узлы, команды и эффект

Правила: `3a2a9c7`; сцена: `3d2ab70`; команды: `a578f6f`; счёт: `b8e5cd7`;
ремонт — `git log -1 -- Assets/_Project/Scripts/Units/Health.cs`.
Итоговые [46/46 EditMode](../evidence/tests-W2.5-editmode.json) и
[61/61 PlayMode](../evidence/tests-W2.5-playmode.json) с UTC/хешами входов.
Ранние FAIL-логи W2.1/W2.4 сохранены, сравнение дробных величин исправлено на
допуск 1e-6. Main проверялась через CLI; техническое Warp использовалось только
в проверочном сценарии, игровые команды по tap идут обычным NavMesh-путём.

- [18/18 подходов и захват](../evidence/W2-scene-check.txt), [Game View](../evidence/W2-owned.png).
- [45:0 от A/B](../evidence/W2-score-check.txt), [результат](../evidence/W2-score.png).
- [C: +18 HP за 3 с, без лечения на pause/contest](../evidence/W2-heal-check.txt), [спорный C](../evidence/W2-heal-contested.png).

Компиляция прошла, groundTruth consoleErrors=0 после финальной проверки.
AI и respawn ещё отсутствуют; это не полный плейтест и не финальная Android-сборка.

## W3 — соперник и полный игровой цикл

Правила: `359778b`; runtime/сцена: `116bb28`; сценарии/прибытие группы —
`git log -1 -- Assets/_Project/Tests/PlayMode/MainMatchTests.cs`.
Проверки: [53/53 EditMode](../evidence/tests-W3.1-editmode.json),
[67/67 PlayMode](../evidence/tests-W3.3-playmode.json),
[реальный AI в Main](../evidence/W3-main-ai.txt), [Game View](../evidence/W3-main-ai.png).
Четыре партии Main с обычными move/attack и ускорением timeScale=4 завершились:
[CSV](../evidence/W3-main-matches.csv). При удержании A и обходе Scout на B
получена Victory 45:8,2. Остальные три плана дали Defeat. Начальная серия до
исправления группового прибытия сохранена отдельно; её результаты не скрываются.
Это автоматические игровые сценарии, не Playtest verified. После respawn/карты
баланс нужно повторить; конечный Android build и человеческая проверка впереди.

## W4 — службы настоящего матча

Пакет/лицензия: `b5c1db7`; root/сцена: `0badd72`; удаление fallback —
`git log -1 -- Assets/_Project/Scripts/Battle/BattleController.cs`.
[53/53 EditMode после установки](../evidence/tests-W4.1-editmode.json),
[72/72 PlayMode](../evidence/tests-W4.3-playmode.json). GameLifetimeScope регистрирует
Clock/State Scoped и существующие Battle/AI; injection реально участвует в Main.
Проверены ошибки отсутствия Clock/State, отсутствие скрытого fallback и новые
instances после dispose/reload. [Четыре партии](../evidence/W4.3-main-matches.csv).
G4 PASS. UnitFactory добавляется в W5, без sample-заглушки.

## W5 — загрузка, возрождение и владение ресурсами

Environment: Unity 6000.6.4f1, Windows, Android target; итоговый очищенный
checkpoint `7cdb6b2` (исходный `d6532d7`). Code: UnitFactory,
AddressableUnitPrefabProvider, UnitPrefabOwnership, UnitLifecycle, RespawnTimer,
GameLifetimeScope. VContainer создаёт реальные provider/factory; UniTask ждёт
реальный load и pause-safe countdown. Lease удерживается до физического окончания
IO даже при логической отмене; clone после cancellation не создаётся.

- [Чистые таймеры 63/63](../evidence/tests-W5.5a-editmode.json),
  [Main lifecycle regression](../evidence/tests-W5.6-playmode.json).
- [10 последовательных настоящих Main restarts](../evidence/W5.6-main-restarts.csv):
  новый state/clock, старые factory instances 0, 6 текущих units/handles.
- Missing key после двух successful loads: cleanup, видимый error, Retry/Exit;
  cancellation во время физической загрузки, отсутствие позднего spawn и double-release
  проверены integration tests. Ранние FAIL/исправления сохранены в evidence.
- [APK с локальными bundles](../evidence/android-build-W5-G5.json),
  [установка и бой на Android](../evidence/W5-G5-device.json).

Result G5: PASS. Пилотный APK не использован вместо финального; его native
AssetPackManager diagnostic и URP shader notices были оставлены для W8.

## W6 — сенсорное управление и читаемый интерфейс

Environment: тот же Editor; Android, Android 11/API 30. Code: UnitCommander, TacticalCamera, BattleHud, SafeAreaLayout, TutorialView,
AndroidSystemBars. Release tap отделён от drag/pinch/cancel, UI raycasts блокируют
команды. Камера имеет границы pan/zoom. HUD читает те же scoped State/Lifecycle.

- [Virtual input, tutorial и регрессия](../evidence/tests-W6.5-playmode.json).
- [Настоящие размеры и bounds](../evidence/W6.4-layout-observations.txt),
  [16:9](../evidence/W6.4-16x9.png), [20:9](../evidence/W6.4-20x9.png),
  [4:3](../evidence/W6.4-4x3.png).

Result G6: PASS. Android Insets API вызывается только на Android;
`com.unity.modules.androidjni` явно включён. Фокусный screenshot не доказывает
сохранение paused state; это ограничение осталось явно записанным в device report.

## W7 — звук, физическая симметрия и измеренные планы

Code checkpoints после очистки: `6271c1b` MatchAudio, `d7fcea4` зеркальная арена.
MatchAudio принадлежит сцене: четыре собственных процедурных clips, реальные
shot/capture/outcome events, mute сохраняется в PlayerPrefs; teardown освобождает
клипы и подписки. [110/110 PlayMode](../evidence/tests-W7.1-playmode.json),
[реальная Main observation](../evidence/W7.1-audio-observation.json),
[feedback screenshot](../evidence/W7.1-feedback.png).

[ADR-006](../adr/006-fair-arena-and-measured-tactics.md) описывает реальные
асимметрии старой геометрии и исправления без смены UnitStats/первичных GUID.
Обе тройки проходят к A/B/C из своих баз; новый obstacle зеркален старому,
NavMesh действительно перебейкован с прежним GUID.
[112/112 PlayMode](../evidence/tests-W7.2-final-playmode.json),
[5 sustained plans](../evidence/W7.2-tactics-sustained-plans.csv),
[осмотренная арена](../evidence/W7.2-arena.png).

Result: implementation PASS, G7 IN_PROGRESS. Пользователь сообщил «чето слышно»
и обнаружил перекрытие выделения поверхностью узла. Исправление ниже; финальный
отзыв и подтверждение полной партии ещё не получены. Скриптовые Victory/Defeat показывают
реальные приказы, бой, захват и respawn, но не заменяют человека и не обещают
постоянно выигрывающую стратегию. Запрошены две ручные партии и отзыв по звуку/UI.

## W8 — регрессия и кандидат Android

Checkpoint `c0a13b3`, code/assets соответствуют исходному `06cc168`.
[ADR-007](../adr/007-relay-entry-and-unused-renderer-resources.md): полный прогон
выявил затор тройки на B. Arrival margin изменён 0,15 → 0,6 м, чтобы первые два
бойца не перекрывали вход третьему. [Focused fix PASS](../evidence/tests-W8-arrival-fix-playmode.json)
и [112/112 финальная регрессия](../evidence/tests-W8-retry-playmode.json) включают
18 настоящих маршрутов из базы, load/cancel/error/Restart, virtual touch и Audio.
Сценарий шести simultaneous attackers на одной точке не заявляется проверенным.
В [5 повторённых планах](../evidence/W8-retry-sustained-plans.csv) фланг дал
естественную Victory 45:15,5 за 58,5 игровых секунд; остальные дали Defeat.

Main не включает post processing. В обоих RendererData удалена только ссылка
postProcessData, которая заставляла URP создавать неиспользуемые post passes.
[Game View 1280×720](../evidence/W8-renderer.png) осмотрен;
[63/63 EditMode](../evidence/tests-W8-final-editmode.json).
Вывод об исчезновении Android shader notices требует актуального device log.

Final APK и его embedded content описаны в начале отчёта. Сборка incremental;
побочные Input System preloadedAssets восстановлены через API.

В первом fresh public clone (`0b2badb`) 63/63 EditMode прошли; PlayMode дал
107/112. Пять legacy-тестов ждали 600 кадров, которых headless Editor достигал
раньше нужного времени движения. Исправление `acbd9bf` сохраняет physical/HP/LOS
assertions, ждёт до 10 игровых секунд: [focused 25/25](../evidence/clean-clone-W8-focused-retry.json),
[full 112/112](../evidence/clean-clone-W8-full-retry.json), [ADR-008](../adr/008-game-time-in-navigation-tests.md).
Clone импортирован с пустой project Library; системный UPM cache использован.
Первый import изменил derived URP global settings, tests создали CSV; рабочее
дерево clone после Unity не названо чистым, его status сохранён в отчёте.

По отзыву пользователя в `0ae98c8` изменена одна координата общего Unit.prefab:
SelectionRing Y −0,98 → −0,7. [Bounds всех ролей](../evidence/W8-selection-visibility.txt):
минимальный зазор над RelayCaptureArea 0,109 м. [Настоящий Game View](../evidence/W8-selection-visible.png)
осмотрен. Технический visual fixture ставил реальные prefabs на узлы при
замороженной сцене; это проверка видимости, а не игровой победы/захвата.
После изменения: focused 18 routes, затем полные 63/112 из `0ae98c8`,
[10 Main restarts](../evidence/W8-selection-final-main-restarts.csv),
[полные партии](../evidence/W8-selection-final-main-matches.csv),
[пять планов](../evidence/W8-selection-final-sustained-plans.csv). В повторе split-pressure
дал естественную Victory 45:31,8 за 90,8 игровых секунд; hold-then-flank —
Defeat 44,4:45 за 91,5 с. Ранее исходы этих планов отличались: реальные NavMesh
траектории не являются детерминированной гарантией победы.

Фактическая диагностика Android на предыдущем APK `6091B57B…376F`:

| Серия | Результат и границы | Evidence |
| --- | --- | --- |
| Первая | 600 с, только 132/395 foreground samples из-за выключения/блокировки экрана; частичная | [JSON](../evidence/W8-android-performance-partial.json), [samples](../evidence/W8-android-performance-partial.csv) |
| Вторая | 601,034 с, 398/398 foreground, 18 029 frames, средний 29,86 FPS; median frame 33,338 мс, P95 49,995 мс; CPU 34,94% одного ядра | [JSON](../evidence/W8-android-performance-foreground.json), [samples](../evidence/W8-android-performance-foreground.csv) |

Вторая серия преимущественно измеряла экран результата, **не 10 минут активных
повторных партий**. PSS 237 130 → 217 591 KiB, max 237 130 KiB; это ограниченное
наблюдение, не доказательство отсутствия утечек при respawn/Restart.
Mono GC allocations и GPU time недоступны без подключённого Unity Profiler;
данные Android ART не подставляются вместо Mono. [Экран установленной игры](../evidence/W8-android-launch.png)
осмотрен перед добавлением в Git.

[Исходный baseline APK](../evidence/W8-baseline-native-probe.json), без Addressables,
также выводит один `ClassNotFoundException … AssetPackManager`, otherUnhandledErrors=0.
[Отфильтрованный diagnostic](../evidence/W8-baseline-native-probe-log.txt) показывает
тот же probe Unity; вывод о его существовании до интеграции — сравнение с baseline,
а не утверждение об устранении. Финальный native log ещё не собран. Повторный offline helper остановился при чтении размеров
экрана, WiFi/mobile data восстановлены в finally; offline PASS не записан.

`Tools/measure-android.ps1` подготовлен для 600 с own SurfaceView/CPU/PSS/RSS с
автоматическим нажатием существующей кнопки результата. Счётчик фиксирует обнаруженные restart taps;
автоматическая нагрузка не заменяет человека. Требуется сохранить raw frames,
просмотреть реальные result screenshots вне Git и проверить foreground.

## W9 — документы и сохранность истории

Русский [README](../../README.md), [8 ADR](../adr),
[фактические licenses/notices](../THIRD_PARTY_NOTICES.md),
[11 практических упражнений с существующими SHA](v1-contract/STUDY_WALKTHROUGH.md).
Root LICENSE отсутствует; новая лицензия основного проекта не назначалась.
Primitives/SFX собственные, third-party licenses скопированы из установленных
пакетов, Gitleaks — из официального release. AI-участие не скрыто.

По отдельному явному разрешению очищены опубликованные author/committer и
локальные paths; 79/79 коммитов, даты/сообщения/топология и весь код сохранены.
[Privacy audit](v1-contract/PRIVACY_AUDIT.md), [проверка публикации](../evidence/privacy-history-published.json),
[Gitleaks всей истории 0 findings](../evidence/privacy-gitleaks-clean-history.json).
Рабочее дерево не заменялось; полный проверенный Git/LFS backup находится вне Git.
Чужие clones/forks/внешние кеши операция не стирает. Дальнейшие push/APK/release
требуют отдельного разрешения. Последние evidence/docs-коммиты остаются локальными.

[Обычный Main smoke](../evidence/W9-editor-smoke.json) после полной регрессии:
load Ready, реальные Commander API orders, естественный Defeat 18:45,
consoleErrors=0 и compilationFailed=false. [Game View](../evidence/W9-editor-smoke.png)
осмотрен; он снят раньше окончания, на 18:32 / 02:24, и не выдаётся за тот же
момент telemetry. Score/HP/AI/позиции не подменялись. Два ожидаемых сообщения
`MatchState was not injected` / `Enemy tactics requires injected BattleController`
соответствуют явным LogAssert.Expect в MatchScopeTests; они были просмотрены
перед очисткой консоли для этого отдельного положительного запуска.

`2f1bfd6` сохранил полные тесты/APK/ограниченные Android измерения и baseline native
probe. [Финальный audit](../evidence/final-audit-W9.json) проверяет локальные ссылки
в Markdown, JSON, реальные учебные SHA и неизменность всех входов последних
EditMode/PlayMode/build reports. `check-repository`, privacy scan и LFS fsck PASS;
`check-gates -RequireComplete` намеренно отклоняет приёмку из-за G7/G8.
Audit содержит реальный pre-commit status, не предсказывает SHA собственного
коммита. После документационного коммита требуется проверить чистое рабочее дерево.

Пользователь поручил продолжать без телефона. Доступная реализация и Editor
проверки завершены, APK собран; прежние device observations сохранены, **финальные
Device verified / Playtest verified / Release ready не объявлены**. Следующее
подключение: вернуть настройку сна экрана, выполнить offline/native log, 600 с
активных партий, подтвердить выделение/mute и результат человеческой партии.
Локальный таймер возобновления сегодня 13:19:48 +07:00 проверен в Task Scheduler;
пока не запускался. Если все gates уже PASS, он пропускает повторную работу.
