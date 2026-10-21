# SectorLab v1.0 — фактический статус реализации

Последнее обновление: 2026-10-21; исходный аудит: 2026-10-11.
Рабочий каталог: `[LOCAL_USER_HOME]\Projects\SectorLab`.

## Сводка

Подробный журнал по шагам — ниже, в хронологии от новых записей к старым.

| | |
|---|---|
| Последний проверенный код | аудит 21.10 (бой на тике, команды, id): EditMode 68/68, PlayMode 138/138 PASS. APK V-ring собран до этих изменений |
| Последний APK |На телефоне — A5 `8257C9B1…A8BE83` |
| Gates | G0–G6, G9 — PASS; G7, G8 — IN_PROGRESS ([GATES.json](GATES.json)) |
| Публикация | README переработан под портфолио (15.10). Push и GitHub Release выполняет владелец: auto mode агента запрещает push |

1. Установка APK V-ring и партия: выбор бойца на узле, длительность боёв, толкучка, рамка «кто побеждает», сигналы захвата (G7).
2. Видимый геймплей и 600-секундный `Tools/measure-android.ps1` на разблокированном телефоне (G8). Последняя попытка — `BLOCKED_SCREEN`, фокус был у keyguard.
3.

**Известные ограничения:** запуск в эмуляторе — FAIL (SIGILL/SEVL, [ANDROID_EMULATOR.md](ANDROID_EMULATOR.md)).
Для `Tools/check-repository.ps1` нужен `rg` в PATH; 15.10 его проверки выполнялись вручную:
whitespace, отсутствие дублей GUID и `.meta`, JSON пакетов — PASS, privacy — PASS.

**Следующий шаг:** установить APK V-ring и повторить партию.
После этого решить, нужна ли v1.1 с механическими различиями ролей (например, особая способность Scout).

## Аудит, рефакторинг и подготовка к сети (21.10.2026)

[Отчёт аудита](../audit/2026-10-21-code-review.md): 12 находок, 3 высокой важности —
все про сетевую модель. [ADR-013](../adr/013-network-readiness.md): авторитетный хост,
не lockstep.

- `41b2a0c` refactor: удалён отключённый `ForceCounterView` и ежекадровый подсчёт живых
  в `BattleController`; общий `RelayText`; итог «Победа/Поражение/Ничья» по-русски;
  порядок ИИ без `ToString()` ролей; кэш `Image` кнопок HUD.
- `775828d` fix: кулдаун и урон `UnitAttack` — в `Simulate(seconds)` от тика матча,
  наведение и преследование остались покадровыми. Новый тест: 60 FPS и рваные кадры
  за 2 игровые секунды дают одинаковый урон. Три теста, ждавших выстрел от кадров,
  переведены на тик. [Регрессия](evidence/tests-R1-full-playmode-playmode.json) 132/132,
  [EditMode](evidence/tests-R1-full-editmode-editmode.json) 68/68.
- feat: приказы игрока — данные `UnitCommand`, проверка в `MatchCommands.Execute`
  (матч идёт, юнит жив и свой, цель допустима); `Unit.Id` = слот ростера, сохраняется
  при респауне; `UnitCommander.LocalTeam`. [Команды 6/6](evidence/tests-R2-commands-playmode.json),
  [командир 27/27](evidence/tests-R2-UnitCommanderTests-playmode.json),
  [жизненный цикл 6/6](evidence/tests-R2-MainLifecycleTests-playmode.json);
  полная регрессия: [EditMode 68/68](evidence/tests-R2-full-editmode-editmode.json),
  [PlayMode 138/138](evidence/tests-R2-full-playmode-playmode.json) PASS.
- R3 не установлен: auto mode запретил агенту подключать сторонние git/NuGet-пакеты.
  План применения (реактивное состояние матча для HUD/звука) — в ADR-013.

## Регрессия и APK V-ring (16.10.2026)

На `d91f4b9`: [EditMode 68/68](evidence/tests-V-full-editmode-editmode.json),
[PlayMode 132/132](evidence/tests-V-full-playmode-playmode.json) PASS.
[Тактики](evidence/V-full-playmode-sustained-plans.csv): 4 победы / 1 поражение
(прошлые прогоны на том же балансе: 3/2 и 1/4 — разброс большой, вывод о балансе за человеком).
[Android-сборка](evidence/android-build-V-ring.json) Succeeded, 0 errors:
`Builds/Android/SectorLab-V-ring.apk`, 32 314 701 bytes, SHA-256
`C27B72F394A3582D7FD552D67811B5F4178D841B7B7DA5703FA7DC5F561C7428`.
`preloadedAssets` после сборки снова возвращён к `[]`. Установка: `adb devices` пуст.

## CI1 — GitHub Actions + GameCI (16.10.2026, не запускался)

`.github/workflows/tests.yml`: матрица EditMode/PlayMode в `game-ci/unity-test-runner@v4`
для Unity 6000.6.4f1, кэш `Library`, результаты — артефакт и check run. Пока только
`workflow_dispatch`: без секретов `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
каждый push давал бы красную проверку. Владельцу: добавить секреты (Settings → Secrets
→ Actions), запустить вручную, при успехе добавить триггеры `push`/`pull_request` и бейдж
в README. Наличие образа `unityci/editor` для 6000.6.4f1 и прохождение PlayMode в
headless Linux не проверены.

## V1 — узлы читаются краем глаза (16.10.2026)

Продолжение B2 («краем глаза не распознаёшь»). На скриншоте B2 подписи узлов на поле
(«A +1 очк/с / СПОР · эффект остановлен») были мелкими, наползали на бойцов и дублировали
карточки HUD. Теперь на поле только крупная буква и короткий статус (`СПОР`, `42%`,
`ремонт`), детали остались в карточках. Вокруг диска `RelayPoint` рисует кольцо захвата
(`LineRenderer`, материал M_Marker): длина — доля прогресса, цвет — сторона,
которая захватывает (синий/красный), жёлтый — спор; у нейтрального узла без прогресса
кольца нет. [RelayPoint 12/12](evidence/tests-V1-relay-playmode.json) (2 новых теста кольца),
[HUD 9/9](evidence/tests-V1-hud-playmode.json) PASS.
[Game View](evidence/V1-relay-ring-capturing.png) просмотрен: C — синее кольцо 80%,
A — полное красное, B — жёлтый спор.

## P1 — замер GC-аллокаций (16.10.2026)

Проверка пункта roadmap «Profiler при измеримой необходимости» перед подключением
zero-alloc-библиотек из радара StackSignal ([ADR-012](../adr/012-measured-allocations.md)). Медиана добавки партии к Editor-фону — 0 Б/кадр, среднее ~550 Б/кадр (редкие смены
подписей). ZString/пулы не подключаются. Новый `MainAllocationTests` (бюджет 512 Б/кадр
и контроль чувствительности с подброшенными 2 КБ/кадр):
[2/2 PASS](evidence/tests-P1-gc-budget2-playmode.json), [кадры](evidence/P1-gc-budget2-gc-frames.csv). Атрибуция по типам компонентов была разовой диагностикой, в коде не оставлена.

## Отзыв владельца после партии на APK A5 (15.10.2026)

Перед партией установленный APK сверен: SHA-256 извлечённого `base.apk` = `8257C9B1…A8BE83`
(сборка A5), после `4a06ae3` входы Assets/Packages/ProjectSettings не менялись. Владелец сыграл полную партию на Android:

1. Приказ занятому узлу работает, но бойца, стоящего на узле, больше не выбрать тапом по нему — **B1**.
2. Назначения ролей прочитаны и поняты. 3. Состояние узлов видно по надписям.
4. Выстрелы и звук поражения отчётливо слышны (сигналы захвата отдельно не отмечены).
5. Результат и «Ещё бой» работают. 6. Mute сохраняется после перезапуска.
7. Раздражает: бойцы толкаются и мешают пройти — **B4**; бойцы гибнут слишком быстро — **B3**
   (по данным Scout/Ranger под огнём двоих живут ≈1,5 с); не понятно, выигрываешь ли:
   глаза на поле, текст HUD краем глаза не считывается — **B2**.

G7: пункты 2, 3, 5, 6 подтверждены человеком; 1 и 7 требуют правок.

**B1 / FR-004/006.** Правило A2 «узел важнее союзника» распространялось и на прямое
попадание лучом в тело союзника, поэтому при любом выбранном бойце союзника на узле
нельзя было выбрать. Теперь прямое попадание в бойца сохраняет обычный смысл
(союзник — выбрать, враг — атаковать), а тап по поверхности узла рядом с союзником,
включая допуск под палец, остаётся приказом выбранному. Подсказка обучения обновлена.
Тесты A2 переведены на тап по поверхности узла; добавлены прямой выбор на узле
(синтетическая арена и сохранённая Main). Регрессия старого кода для новых тестов
отдельно не запускалась: старое условие `unit.Team == Team.Player` явно направляло
такой тап в приказ. [UnitCommander 27/27](evidence/tests-B1-commander-playmode-playmode.json),
[HUD 8/8](evidence/tests-B1-hud-playmode-playmode.json),
[EditMode 63/63](evidence/tests-B1-full-editmode-editmode.json),
[полная PlayMode 126/126](evidence/tests-B1-full-playmode-playmode.json) PASS.
Проверка пальцем — на следующем APK.

**B3 / баланс.** Урон снижен ~в 2,5 раза (Tank 5, Scout 6, Ranger 9), HP прежние —
[ADR-011](../adr/011-longer-fights.md). [Тактики](evidence/B3-tactics-sustained-plans.csv):
3 победы / 2 поражения вместо 0/5; [MainTactics 1/1](evidence/tests-B3-tactics-playmode.json),
[MainMatch 1/1](evidence/tests-B3-matches-playmode.json) PASS. Полная регрессия после B3
ещё не запускалась.

**B4 / толкучка.** У всех агентов был одинаковый `avoidancePriority` 50, и стоящий
союзник перекрывал узкий проход. Новый тест (коридор 1,6 м, стоящий боец посередине)
[на старом коде FAIL](evidence/tests-B4-passage-before-playmode.json): боец с приказом
застрял в 1,8 м от цели. Теперь `UnitMovement` ставит 30 на время приказа и 70 в покое:
идущий не уступает стоящему, а стоящего отталкивает. Правило одинаково для обеих команд.
[UnitMovement 8/8](evidence/tests-B4-movement-playmode.json) PASS.

**B2 / «выигрываю или нет».** Новое чистое правило `MatchRules.Forecast`: при текущем
доходе побеждает тот, кто раньше дойдёт до 45, иначе — у кого больше очков к 180 с.
`BattleController.CurrentRates` заменил дублирующий подсчёт дохода в HUD.
HUD рисует рамку толщиной 10 по краю поля в цвете прогнозного победителя и
скрывает её, если никто не впереди. Рамка лежит под панелями и не ловит тапы.
`MatchAudio` при смене прогноза, устойчивой 1 игровую секунду, показывает
«Сейчас вы побеждаете» / «Сейчас побеждает враг: отбейте узел» с сигналом захвата
или предупреждения. [Прогноз EditMode 5/5](evidence/tests-B2-forecast-editmode.json),
[HUD 9/9](evidence/tests-B2-hud-playmode.json) (захват A → синяя рамка и подпись;
спор A + вражеский B → красная), [звук 4/4](evidence/tests-B2-audio-playmode.json) PASS.
[Game View в живой партии](evidence/B2-lead-frame-16x9.png) просмотрен: красная рамка при 0:9.

**Итог B1–B4.** На `5e50114`: [EditMode 68/68](evidence/tests-B-full-editmode-editmode.json),
[полная PlayMode 128/128](evidence/tests-B-full-playmode-playmode.json) PASS.
[Android-сборка](evidence/android-build-B-feedback.json) Succeeded, 0 errors, 1 warning:
`Builds/Android/SectorLab-B-feedback.apk`, 32 314 013 bytes, SHA-256
`849B4F0708550133FB4407F9294F1F3A3FC804EFEA5A054F4BBB5166869D9896`. Сборка снова добавила Input System в `preloadedAssets`; прежний пустой список восстановлен. Осталось: установка, повторная партия владельца (проверить B1–B4 пальцем).

## Продолжение после внешнего аудита 12.10.2026

Пользователь предоставил аудит снимка `dccfe6c` и поручил продолжить разработку. На старте `main` и рабочее дерево чисты; архив не разворачивается поверх проекта. Подключён физический Android / Android 11. Установленный APK выгружен
и сверён: SHA-256 совпадает с `SectorLab-W8-selection.apk` (`654CB514…1237DA`). Приложение запущено; получен ручной отзыв об управлении, ролях и звуке (ниже).

A0 / FR-024, NFR-003/004: первый запуск нового regression из оставленного Play Mode
оказался некорректным. Перекомпиляция сбросила несериализованное runtime state;
TestRunner не смог сохранить сцену из Play Mode и завершился ошибкой orchestration. Прогон отменён; [console evidence](evidence/A0-test-harness-in-play-failure.json)
сохранён до очистки консоли. Это не результат gameplay regression. `Tools/verify-unity.ps1` теперь останавливает Play Mode и ожидает завершения перехода
перед Refresh/run_tests. Проверка этого пути: запуск сохранённой Main, затем
[полный EditMode](evidence/tests-A0-from-play-editmode.json): **63/63 PASS**. Изменение не сохраняет сцену и не меняет игровые данные.

A0 зафиксирован в `8cb198a`. A1 / FR-003/013/027: роль теперь задаётся в
`UnitStats.role`, существующие три профиля мигрированы явно. Lifecycle проверяет
роль и team, HUD и AI не используют имя ассета как идентификатор.
GUID/характеристики/NavMesh/состав roster сохранены. Решение —
[ADR-009](../adr/009-explicit-unit-role.md).
На старом коде [rename regression](evidence/tests-A1-role-before-retry-playmode.json)
завершилась ожидаемым FAIL. После исправления
[MainLifecycle 6/6](evidence/tests-A1-role-focused-playmode.json) PASS, включая
respawn шести переименованных профилей и отказ/cleanup при неправильной role.
[EditMode 63/63](evidence/tests-A1-role-editmode.json) PASS.
[Полная PlayMode регрессия](evidence/tests-A1-role-playmode.json): **114/114 PASS**.
Два DI exceptions в консоли совпали с ожидаемыми `LogAssert.Expect` в
`MatchScopeTests.UnwiredBattle…/UnwiredEnemy…`; оба теста PASS. Они не означают
отсутствия GameLifetimeScope в сохранённой Main. Штатный запуск проверяется отдельно
после очистки этих уже подтверждённых ожидаемых записей.
[Обычная сохранённая Main](evidence/A1-role-editor-smoke.json): Ready, естественный
бой и начисление очков, compilationFailed=false, consoleErrors=0.

Ручной отзыв пользователя с физического телефона на исходном APK:
звук не позволяет понять событие; тап по узлу с союзником выбирает этого союзника
вместо отправки выбранного бойца; назначения ролей и эффекты узлов не ощущаются. Итоговый счёт и mute не подтверждены. Приоритет следующих малых правок:
приказ занятому узлу → читаемые роли/эффекты и компактный HUD → различимые SFX.

A1 зафиксирован в `ee23f2b`. A2 / FR-004/006/007: сообщение пользователя о
переключении на союзника у узла воспроизведено
[на старом вводе](evidence/tests-A2-occupied-before-playmode.json): вместо выбранного
бойца Selected становится стоящим в зоне союзником. Теперь прямое попадание во врага означает атаку; при выбранном бойце проверяется
узел лучом без Units, который всё ещё блокируется стеной/препятствием. Узел получает
приоритет перед союзником и finger tolerance. Без выбора союзник выбирается обычным
тапом, карточки позволяют переключить роль в любой зоне. Подсказка обновлена.
[UnitCommander 26/26](evidence/tests-A2-occupied-playmode.json) и
[сохранённая Main/HUD 5/5](evidence/tests-A2-main-input-playmode.json) PASS;
проверены реальные NavMesh, perspective ray, UI/card selection и virtual touch. Игровые числа, сцена и NavMesh не менялись. Проверка пальцем на новом APK ожидается.

A2 зафиксирован в `909c48a`. A3 / FR-015/028: верхние панели уменьшены с 225 до
156 canvas units, camera viewport рассчитывается из общих размеров HUD.
При 1280×720 высота поля выросла с 432 до 478 px. Счёт показывает текущий доход
каждой стороны; карточки узлов — реальные значения из RelayConfig, захват,
начисление очков, ремонт/ожидание бойца, остановку эффекта при споре или паузе.
Сцена изменена точечно: старые object IDs/ссылки сохранены, NavMesh не менялся.
[HUD 7/7](evidence/tests-A3-header-playmode.json) PASS, включая захват/спор/паузу,
реальное восстановление HP на C и отсутствие перекрытия поля панелями.
Просмотрены настоящие Game View [16:9](evidence/A3-header-16x9.png),
[20:9](evidence/A3-header-20x9.png), [4:3](evidence/A3-header-4x3.png);
[размеры и bounds](evidence/A3-header-layout.json) сохранены. Кнопки читаемы,
не обрезаны. Смена render size требует нескольких кадров для перестройки UI;
промежуточные кадры с неготовым Canvas не используются как evidence.
Это Editor-проверка; читаемость/ощущение эффектов на новом APK ещё открыты.

A3 зафиксирован в `468e6db`. A4 / FR-003/015: карточки показывают назначения
Tank/Scout/Ranger (защита/манёвр/дальний бой). Выбор раскрывает тактический совет,
HP, урон и интервал удара, дальность и скорость из действующего профиля.
Scout не захватывает быстрее остальных: подсказка описывает скорость перемещения,
а не несуществующую способность. Механики/баланс/префабы не менялись.
Мировые подписи узлов показывают русский статус и бонус из RelayConfig.
`RelayPoint` кэширует видимое состояние: одинаковые ticks больше не форматируют
строку и не переустанавливают color property block. Configure сбрасывает кэш.
[HUD 7/7](evidence/tests-A4-role-hud-playmode.json) и
[RelayPoint 10/10](evidence/tests-A4-relay-playmode.json) PASS.
Первая попытка объединить классы через `|` дала
[0 найденных тестов](evidence/tests-A4-roles-playmode.json); это ошибка фильтра,
не PASS и не gameplay failure. Затем оба класса проверены отдельно.

A4 зафиксирован в `dc16223`. A5 / FR-018/015: три разных процедурных звука
оружия, собственный capture и предупреждение о вражеском capture/потере
контроля/новом споре. Непрерывный спор не повторяет сигнал каждый tick. Короткая подпись называет событие и узел; mute не отключает визуальную информацию. Notice не перехватывает тапы. Все семь clips принадлежат сцене и освобождаются;
mute/restart и существующие подписки сохранены. Решение —
[ADR-010](../adr/010-mobile-feedback.md).
[Первые 2/4](evidence/tests-A5-audio-playmode.json) и
[повторные 3/4](evidence/tests-A5-audio-wired-playmode.json) сохранены:
сначала scene helper выполнился до Refresh новой сборки и не создал notice;
также тест отводил бойца в центр C, невольно создавая ещё один настоящий capture. После подключения notice к сохранённой сцене и отвода в исходные spawn points
[audio 4/4](evidence/tests-A5-audio-final-playmode.json) PASS. Короткий clip проверяется PCM/событием, без хрупкого ожидания isPlaying через два
кадра; полноценный playback проверяет исходный result test.
[Полная PlayMode регрессия](evidence/tests-A5-full-playmode.json): **124/124 PASS**,
failed/skipped/inconclusive=0; включает новые UX/audio случаи, настоящие партии,
10 restarts, cancellation/failure/cleanup.
[EditMode](evidence/tests-A5-full-editmode.json): **63/63 PASS**.
[Обычная Main](evidence/A5-editor-smoke.json): Ready, 6 units, consoleErrors=0.
[Снимок обратной связи](evidence/A5-feedback-16x9.png) просмотрен: подпись своего
захвата A, +1 очк/с, назначение/характеристики выбранного Tank и видимый маркер. Для этого отдельного visual fixture настоящий Tank размещён на A и пройдены
ticks capture; это не естественная победа или человеческая партия.

A5 зафиксирован в `4a06ae3`. A6 / FR-020/024, NFR-008/009: свежая
[Android-сборка](evidence/android-build-A5-feedback.json) Succeeded, 0 errors,
1 Pipeline warning, 136,141 с. APK `Builds/Android/SectorLab-A5-feedback.apk`,
32 311 981 bytes, SHA-256
`8257C9B1EC109272A3027CD6146BE9790657D085651B856983225797E3A8BE83`. Все 296 входных hash полных PlayMode tests совпали со входами сборки. После сборки восстановлен прежний пустой `preloadedAssets`; ProjectSettings не
изменены. [Установка на Android/Android 11](evidence/A6-phone-install.json) с `-r`
и повторным извлечением APK: hash совпал, данные приложения сохранены, процесс жив. Скриншоты системного экрана не делались. В measurement helper учтены `ResumedActivity` без `m` и полный `dumpsys window`;
одной resumed Activity недостаточно для разрешения taps/capture. Исправлен также
Windows PowerShell stderr успешного adb pull. Это изменения проверки, не игры. Прежняя настройка удержания экрана восстановлена в `0` и прочитана
обратно; это зафиксировано в install evidence. Первый временный launch helper неверно разделил package/activity в массиве аргументов;
исправлен, установки/хеши не потеряны. G7/G8 IN_PROGRESS: новый APK установлен, но игровые/слуховые и device workload
проверки нельзя подменять фактом установки. Обычные wakeup,
`wm dismiss-keyguard`, закрытие шторки и повторный запуск своей Activity не сняли
блокировку. PIN/пароль не вводились.

## Где остановились до внешнего аудита

12.10.2026: игровой checkpoint `0ae98c8`. По отзыву пользователя поднят
SelectionRing над поверхностью узла; проверены bounds трёх ролей и настоящий
Game View. Исправление входит в APK `Builds/Android/SectorLab-W8-selection.apk`:
**1.0.0, 32 302 469 bytes**, SHA-256
`654CB514B441F9B8C18B61C93AEED701771D6DE5A3F8E7CCB6ED0FD1081237DA`.
[Build report](evidence/android-build-W8-selection.json): 0 ошибок,
1 предупреждение Pipeline. Input System preloadedAssets после сборки восстановлен
через PlayerSettings API; дополнительных изменений ProjectSettings нет. Полный повторный прогон именно `0ae98c8`: **63/63 EditMode и 112/112 PlayMode PASS**,
[JSON](evidence/tests-W8-selection-final-playmode.json), failed/skipped/inconclusive=0.

Публичный clone без project Library/Temp/Builds импортирован: **63/63 EditMode
и 112/112 headless PlayMode PASS**. Найденные пять тестов с ожиданием 600 кадров
переведены на игровое время в `acbd9bf`; первая неудача и повторные прогоны
сохранены, см. [ADR-008](../adr/008-game-time-in-navigation-tests.md). Две реальные серии Android приложены: первая частичная из-за выключения экрана,
вторая — 601 с, 398/398 foreground samples, в среднем 29,86 FPS, преимущественно
на экране результата. Это ограниченные измерения, **не PASS 10 минут активных партий**. Native AssetPackManager probe воспроизводится также в исходном baseline APK
без Addressables; он не назван устранённым.

ADB сейчас не видит телефон; проверенный эмулятор описан ниже. Предыдущий offline helper прервался
при чтении размеров экрана; готового offline evidence нет, PASS не выставлен. WiFi/mobile data были восстановлены в finally. При следующем подключении нужно
также вернуть `stay_on_while_plugged_in=0` после замера: временное значение 3
сохраняет экран включённым при зарядке; исходное значение записано вне Git. README, ADR, notices и 11 упражнений подготовлены. Следующее: повторный offline
test, 600 с повторных партий на новом APK, подтверждение пользователя и обновление приёмки. Пользователь поручил продолжать без телефона. Доступная W9-передача завершена:
README/checklist/gates согласованы, [audit](evidence/final-audit-W9.json) проверяет
локальные Markdown-ссылки, JSON и доступность учебных SHA. Коммит `2f1bfd6`
сохранил финальную регрессию, APK evidence, ограниченные метрики и native baseline.
[Обычный Editor Main smoke](evidence/W9-editor-smoke.json): реальные приказы,
естественный Defeat 18:45, consoleErrors=0. Два прежних console exceptions
проверены как ожидаемые отрицательные MatchScope tests, затем консоль очищена
для отдельного штатного запуска; это не скрытие runtime ошибки. Финальный SHA документации
и чистота рабочего дерева проверяются после коммита командой `git status`. Таймер 13:19:48 +07:00 выполнился с кодом 1; automatic resume FAIL, а не будущий
запуск. Wrapper исправлен и проверен только через `codex --version`; разработка
продолжается в текущей сессии. См. [AUTOMATION](AUTOMATION.md). Точный HEAD — `git rev-parse HEAD`. [Аудит приватности](PRIVACY_AUDIT.md)
очистил опубликованную main по явному разовому разрешению пользователя:
`0b2badb`, 79/79 коммитов, код/ассеты/даты/сообщения сохранены, Gitleaks всех refs
с privacy rules — 0 findings. Частный backup Git/LFS сохранён вне репозитория.
[Карта SHA](evidence/privacy-commit-map.txt) связывает исходные SHA в историческом
журнале и evidence с доступными очищенными коммитами. Результаты, даты и входные
хеши доказательств не заменяются. Другие push и публикация APK не разрешены.

## Возобновление 12.10.2026 после прерванной сессии

Стартовый HEAD текущей проверки — `5362099`, ветка `main`, remote не менялся. Обнаружены три modified и пять untracked файлов: Android-эмулятор, его build
evidence, выбор ADB-устройства и сведения о неудавшемся автоматическом запуске. Все восемь файлов скопированы в отдельный `SectorLab-resume-*` в локальном TEMP
и сверены по SHA-256; сохранён `working-tree.patch`. Новый архив не применялся,
игровые C#, сцена, prefab, manifest и lock на старте чисты. Изменения сохраняются
отдельными проверенными локальными коммитами; новая публикация не выполняется.

FR-024/FR-025: коммит `bb6b521` исправил устаревший отчёт о таймере — фактический запуск
автоматического продолжения завершился FAIL, доступен только PASS self-test
wrapper. Проверены JSON evidence, PowerShell parser, repository/privacy checks
и текущий реестр gates; G7/G8 остаются открыты.

W8.2e / FR-020, FR-024, NFR-003/004/008/009: пользователь поручил проверить
игру в эмуляторе и завершить оставшиеся доступные проверки. ADB видит только
`emulator-5554`, Android 14. Запущенная игра была **baseline 0.1.0**, а не v1:
установленный APK выгружен и сверён по SHA-256. Диагностическая Activity-сборка
и финальный APK v1.0 падают на нативном `SEVL`/SIGILL; baseline после возврата
тоже падает. Ограничение потоков не помогло. Настройки и библиотеки Unity не
менялись; корректность C# не выводится из ошибки ARM-транслятора.
[Подробности и доказательства](ANDROID_EMULATOR.md).

Повторные **63/63 EditMode и 112/112 PlayMode PASS**, включая реальные Main
matches/10 restarts/тактики; sourceHead `5362099`, входные SHA сохранены. Обычная сохранённая Main в Editor: естественный Defeat 0:45 за 33,1 игровых
секунды, 1280×720, consoleErrors=0; [отчёт](evidence/W8-emulator-resume-editor.json). Выбор ADB-устройства и коррекция окна сохранены; parser PASS, фактическое
`fits=true`. Замер FPS отклонён после вылета приложения, готовых метрик нет. Следующий milestone —
приёмка на подключённом телефоне и отзыв пользователя по полному матчу. Финальный [локальный аудит](evidence/W8-emulator-resume-local-audit.json):
592 входных SHA тестов совпадают с текущими файлами, локальные ссылки/JSON,
privacy/whitespace/LFS проверены. Исходники Unity не изменены.

## Исходный аудит W0 (11.10.2026, историческое состояние)

Исходный HEAD: `1c8754d8cca20bb342c6770c0985b4f14be27fa3` (`docs: record R5 progress`),
ветка `main`, 35 коммитов; upstream `origin/main`, remote `https://github.com/ZheleznovAG/SectorLab.git`.
Проверенный кодовый checkpoint после фиксации: `ec1a94494dcb6a08f857d26de11618ebbb0391ac`.
Текущий docs-checkpoint содержит этот статус; его точный HEAD — `git rev-parse HEAD`.
В начале сессии индекс пуст. Код дошёл до R4 и части R5: бой 3×3,
Tank/Scout/Ranger, NavMesh, выбор/движение/атака, здоровье, вспышка урона,
счётчик живых бойцов, Victory/Defeat и перезагрузка Main.

После HEAD оставались незакоммиченные исправления в `UnitAttack`, `UnitMovement`,
`UnitCommander`, их трёх PlayMode-файлах и пяти исторических документах.
Дополнительно появились новые `AGENTS.md`/`CLAUDE.md`, `README_START.md`,
`AGENT_START_PROMPT.md`, комплект `docs/agent_execution/` и три ZIP.
Новых игровых механик из контракта v1.0 ещё нет. Существующие тесты исхода
проверяют уничтожение команды, а не будущие очки/таймаут/ничью.

## Источники и сохранность

- Последний ZIP: `SectorLab-for-ai-20261011-191247.zip`. HEAD совпадает с рабочим
  репозиторием. SHA-256 шести изменённых C# и пяти старых документов совпадают
  с ZIP. `AGENTS.md` и `CLAUDE.md` отличаются; новый комплект ТЗ отсутствует
  в ZIP. Архив не разворачивался поверх рабочего дерева.
- До новых правок создана полная копия всех 33 изменённых/untracked файлов,
  включая ZIP, в `[LOCAL_USER_HOME]\AppData\Local\Temp\SectorLab-baseline-20261011-211433`. Все копии сверены по SHA-256; там же `backup-manifest.csv`, `working-tree.patch`,
  исходные `HEAD.txt` и `git-status.txt`. Это локальная резервная точка,
  временный каталог не является долговременным хранилищем.
- Git LFS `3.7.1` работает. Три PNG в `docs/images/` чисты; новых LFS-объектов
  нет. Их содержимое не заменялось. Повторяющихся GUID в `Assets/**/*.meta` нет.
- ZIP исключены из Git точными шаблонами локальных экспортов в `.gitignore`;
  сами архивы сохранены. Unity-сцены, префабы и существующие GUID не изменялись.
- Рабочая ветка остаётся `main`; локальные коммиты разрешены текущим AGENTS.md. Push и публикация не выполняются. На момент завершения W0 исходные правки
  сохранены; исходный HEAD остаётся предком новых коммитов.

## Реальные проверки этого аудита

Unity Editor `6000.6.4f1` открыт для этого проекта; проверен через Pipeline.
URP `17.6.0`, Input System `1.20.1`, AI Navigation `2.0.16`, Test Framework `1.8.0`.
Manifest/lock не менялись; VContainer, UniTask и Addressables отсутствуют.

| Проверка | Фактический результат | Доказательство |
| --- | --- | --- |
| Компиляция | PASS: `compilationFailed=false`, `compiling=false`, ошибок консоли 0 | `console_status`, затем `editor_status` |
| EditMode | PASS: 9/9, Failed/Skipped/Inconclusive = 0 | [JSON](evidence/tests-editmode-1c8754d-dirty-20261011.json) |
| PlayMode | PASS: 38/38, Failed/Skipped/Inconclusive = 0 | [JSON](evidence/tests-playmode-1c8754d-dirty-20261011.json) |
| Android build | PASS: `Succeeded`, 0 ошибок, 1 предупреждение; 31,327 с | [BuildReport summary](evidence/android-build-1c8754d-dirty-20261011.json) |
| Устройство | ADB обнаружил `Android`; установка/запуск этого APK НЕ выполнялись | `adb devices -l` |
| Ручной Play Mode/баланс || Нужен отдельный плейтест |
| Clean checkout / Android performance | NOT_RUN | Следующие milestones |
| Git whitespace | PASS: `git diff --check` без ошибок | Проверяется также перед каждым коммитом |

Тесты и APK проверяют исходный `1c8754d` **плюс шесть незакоммиченных C# файлов**.
Это не тесты чистого `1c8754d` и не проверка будущей v1.0. SHA-256 проверенных
исходников фиксируются в [отчёте](EVIDENCE_REPORT.md); при фиксации код не переписывается.

APK: `Builds/Android/SectorLab-baseline-1c8754d-20261011.apk`, 29 253 015 байт.
Сборка выполнялась 2026-10-11 14:19:24–14:19:55 UTC. Это incremental build:
время файла 16:39:35 +07:00 сохранено копированием прежнего неизменного результата,
оно не является временем запуска новой проверки. Полный BuildReport сохранён
в резервном каталоге `build-status.json`; в Git входит только краткий отчёт.
Побочный `preloadedAssets` от Input System убран точечной правкой после обзора
diff; другие настройки проекта не затронуты.

## Рабочие пакеты

Идентификаторы задач берутся из `WORK_ITEMS.csv`; внутри W0 их нумерация отличается
от подпунктов в `06_DELIVERY_PLAN.md`. Например, CSV W0.4 означает выбор юнита,
а раздел плана W0.4 — регрессию и APK. Сопоставлять по смыслу, не закрывать
непроверенные действия по совпадению номера.

| Пакет | Статус сейчас | Что требуется дальше |
| --- | --- | --- |
| W0 / G0 | PASS: аудит/backup/регрессия/build и сохранение исходных правок выполнены ||
| W1 / G1 | PASS | 45 очков/180 с/Draw, часы, pause/resume, остановка боя, result/restart; 29 EditMode + 43 PlayMode и Main Game View |
| W2 / G2 | PASS | A/B/C, capture/neutralize/contest, команды, score и repair; 46 EditMode + 61 PlayMode и Main |
| W3 / G3 | PASS | AI и четыре настоящие партии Main; 53 EditMode + 67 PlayMode, проигрыш и выигрыш зафиксированы |
| W4 / G4 | PASS | VContainer 1.19.0, настоящий scope Clock/State/Battle/AI; 72/72 PlayMode, missing injection и reload |
| W5 / G5 | PASS | Addressables/UniTask, шесть юнитов, respawn/cancel/failure, 10 restarts и локальный content в APK |
| W6 / G6 | PASS | Virtual touch, true Game View 3 форматов, HUD/tutorial; телефон: swipe/pinch и исправленные Android insets |
| W7 / G7 | IN_PROGRESS | SFX/mute, зеркальная арена, 112 PlayMode PASS и пять активных тактик; остаётся человеческий плейтест |
| W8 / G8 | IN_PROGRESS ||
| W9 / G9 | PASS | README/8 ADR/notices/11 упражнений, проверка локальных ссылок/JSON/учебных SHA и неизменности Unity-входов; ограничения G7/G8 явно перечислены |

## Реестр требований (baseline и текущие изменения)

`BASELINE_PASS` означает подтверждённую часть текущего прототипа; не означает
приёмку всей v1.0. `PARTIAL` — часть кода/проверок существует; `TODO` — реализации
нет; `NOT_RUN` — соответствующая проверка ещё не выполнялась.

| ID | Статус | Фактическая граница / пакет завершения |
| --- | --- | --- |
| FR-001 | IN_PROGRESS ||
| FR-002 | PASS | Зеркальная Main, реальные равные базы, четыре препятствия; 18 маршрутов всех бойцов пройдены / W7.2 |
| FR-003 | PASS | Явный UnitStats.role; шесть слотов проходят spawn/respawn даже после переименования профилей; неверная role вызывает cleanup / A1 |
| FR-004 | PASS | Mouse/virtual touch и HUD выбирают/двигают units; выбранный боец получает приказ занять узел даже под союзником; Main и touch regression / A2 |
| FR-005 | PASS | Атака/дальность/LOS/погоня проверены текущей регрессией 112/112 |
| FR-006 | PASS | Release tap/drag/pinch/cancel/UI tests, bounded camera; пользователь подтвердил swipe/pinch на телефоне W6-G6 |
| FR-007 | PASS | A/B Score и C Repair с отдельными RelayConfig в Main; 18/18 NavMesh approaches доступны |
| FR-008 | PASS | RelayState и RelayPoint: 5/10 с, contest, пустой прогресс, смерть, пауза/finish, цвет/проценты проверены |
| FR-009 | PASS | A/B дают 1 очко/с каждый; пустое владение, contest/neutralize/pause, порог в одном тике проверены |
| FR-010 | PASS | Health.Heal и C: 6 HP/с обеим сторонам, Max, fractional buffer, смерть/уход/contest/pause/finish проверены |
| FR-011 | PASS | MatchRules/BattleController: 45 очков/180 с/Draw и реальный доход A/B; scripted Main outcomes / W1–W7 |
| FR-012 | PASS | MatchClock, pause HUD, frozen capture/heal/AI/respawn/attack, первый готовый выстрел ждёт Resume / W5–W7 |
| FR-013 | PASS | Main: все роли обеих команд, 10 игровых с, full HP, без дублей; pause, blocked base и переименованные профили / A1 |
| FR-014 | PASS | EnemyDecisionRules + EnemyTacticsController в Main; 1 решение/игровую с, реальные move/capture/attack и stable assignments проверены |
| FR-015 | PASS | A3–A5: компактный HUD, фактический доход/эффекты, назначения ролей и notice; три формата Game View и full regression |
| FR-016 | PASS | Result/Restart останавливают ввод/бой/AI, отменяют pending respawn, не создают поздних units / W3–W7 |
| FR-017 | PASS | 10 настоящих Main Restart: новые state/clock, 6 units/handles, старые factory instances 0, score/progress/countdown reset / W5.6 |
| FR-018 | PASS | A5: семь scene-owned clips; разные оружие/захват/предупреждения, mute/Restart/cleanup в full tests; слуховая проверка нового APK открыта в G7 |
| FR-019 | PASS | Три подсказки на первом запуске с Pause/Skip и повторным Help; W6.5 107 PlayMode и реальные снимки |
| FR-020 | PASS | Свежий APK 1.0.0 из 4a06ae3, SHA-256 8257C9B1…A8BE83, локальные bundles/catalog; сборка 0 errors, установленный hash совпал / A6 |
| FR-021 | PASS | VContainer 1.19.0: scene scope Clock/State/Battle/AI/factory/provider/lifecycle, missing registrations обнаруживаются / W4–W5 |
| FR-022 | PASS | UniTask используется для реальных prefab loads, pause wait и lifetime cancellation; Main late-result/restart проверен / W5.6 |
| FR-023 | PASS | Шесть локальных prefabs, реальные loads/releases; bundles/catalog внутри Android APK, бой на телефоне / G5 |
| FR-024 | PASS ||
| FR-025 | PASS | 79 реальных атомарных коммитов сохранены; разовый privacy rewrite с разрешения меняет только личные paths/author metadata, см. карту SHA |
| FR-026 | PASS | CSV настоящих Main: время, счёт, outcome, приказы, смерти и захваты обеих команд; без сервера / W7.2 |
| FR-027 | PASS | Missing key после двух успешных loads: cleanup, остановка loading, Retry запускает Main; Exit callback без дублей / W5.6 |
| FR-028 | PASS | Настоящий Game View 3 форматов; simulated safe insets и Android Insets API: HUD целиком виден на телефоне, включая возврат фокуса |
| NFR-001 | PASS | Unity 6000.6.4f1 / 12bfff696524, manifest/lock проверены; Editor не обновлялся |
| NFR-002 | PASS | Публичный clone без Library/Temp/Builds: import и 63/63 EditMode; после исправления frame waits 112/112 headless PlayMode, реальные Main/AA loads/cleanup |
| NFR-003 | PARTIAL | Editor compilation/регрессия PASS; baseline native probe подтверждён, отсутствие новых ошибок на финальном Android требует завершённого device log |
| NFR-004 | PASS | A5: 63/63 EditMode и 124/124 PlayMode, failed/skipped/inconclusive=0; все 296 input hashes совпали со сборкой |
| NFR-005 | PASS | Fixed simulation/clock/score/capture/heal/respawn проверены с дробными тиками, FPS и Pause; точные NavMesh trajectories не обещаны |
| NFR-006 | PASS | Scene-owned cancellation, late load release, missing key/error/restart/finished wait, отсутствие позднего spawn / W5–W7 |
| NFR-007 | PASS | Repository gate проверяет уникальность GUID, .meta и связи; настоящий Main/prefab loads проходят |
| NFR-008 | IN_PROGRESS ||
| NFR-009 | IN_PROGRESS | A5 повторные партии ещё не измерены; прежние 601 с ~29,86 FPS в основном на результате не закрывают это требование. Mono GC/GPU time недоступны |
| NFR-010 | PASS | Опубликованные refs/main очищены; Gitleaks всей доступной истории 0 findings, metadata noreply, privacy gate активен; частный backup исключён из Git |
| NFR-011 | PASS | Происхождение primitives/SFX и реальные licenses/notices пакетов сохранены; лицензию основного проекта агент не назначал |
| NFR-012 | PASS | Один composition root, реальные async/ownership; прежние Unit/Movement/Attack/Commander сохранены, без лишней инфраструктуры |
| NFR-013 | PASS | Русский README: правила, жесты, demo, setup/build/tests, диаграмма, ограничения и ссылки на evidence |
| NFR-014 | PASS ||

## Решения и следующий шаг

- Новый контракт `README_START.md` и `docs/agent_execution/` имеет приоритет
  над историческим ручным режимом R0–R5. Не переносить старые PASS на новые правила.
- В `02_GAME_DESIGN.md` фраза о необходимом присутствии своего бойца для очков
  соседствует с явным правилом начисления после ухода команды. Для W2 действует
  последнее конкретное правило: полный узел без врага продолжает давать очки
  пустым; contest/нейтрализация останавливают эффект. Закрепить это тестом.
- W0–W6 проверены. Наличие плана не означает
  Implementation complete, Device verified, Playtest verified или Release ready.

## Локальные коммиты этой сессии

Самоссылку на SHA текущего docs-коммита смотреть через
`git log -1 -- docs/agent_execution/IMPLEMENTATION_STATUS.md`.

| Задача | Коммит | Сохранённые изменения | Проверка |
| --- | --- | --- | --- |
| W0.1 / контракт | `d15a009` | Backup, актуальный аудит, инструкции, ТЗ, логи, исключение ZIP | Git/LFS/meta, 9+38 tests, Android build PASS |
| W0.2 / FR-005 | `9853006` | UnitAttack + UnitAttackTests: LOS Default, retry ≤1/0,5 с, восстановление после открытия пути | Сохранённая полная PlayMode-регрессия 38/38; source SHA совпадает |
| W0.3 / FR-004 | `970827a` | UnitMovement + UnitMovementTests: bool MoveTo, disabled/off-mesh/invalid stop, фактический проход partial path | Сохранённая полная PlayMode-регрессия 38/38; source SHA совпадает |
| W0.4 / FR-004/005 | `ec1a944` | UnitCommander + UnitCommanderTests: видимость near-miss, сохранение attack при отказе move, скрытый marker | Сохранённая полная PlayMode-регрессия 38/38, включая virtual touch; source SHA совпадает |
| W0.5 / FR-024/025 | `7becd8a` | Исторические docs согласованы с контрактом, реестр/ADR/evidence актуализированы | Проверены ссылки/JSON/CSV; все 20 C# совпадают с тестированным входом; diff/check и staged review |

Решение W0.2: сохранить существующее маскирование препятствий и bounded retry,
не вводить отдельную систему укрытий. Цена — задержка обновления преследования
до 0,5 с; явная смена цели сбрасывает ожидание. `.meta`/prefab не переписываются.

Решение W0.3: `MoveTo=true` означает принятие запроса, а не гарантию полного
пути. Частичный путь допустим до доступной границы; IsMoving описывает текущее
движение агента. Отказы/отключение сбрасывают IsMoving; отдельный path-status UI
остаётся задачей обратной связи W6/W7.

Решение W0.4: допуск касания проверяет видимость центра кандидата. Прямое
попадание в видимую часть коллайдера остаётся доступным. Только принятый запрос
движения отменяет прежнюю атаку и показывает marker; отклонённый сохраняет цель.

Для изучения W0: чем отличаются принятие NavMesh-запроса и достижимость точки;
почему проходящие тесты уничтожения команды не подтверждают матч за узлы;
почему timestamp APK при incremental build может предшествовать BuildReport.

## Продолжение автономной реализации

Входной HEAD новой сессии — `7becd8a`; дерево было чистым. G0 подтверждён,
остальные gates остаются открытыми. Добавлены `Tools/check-repository.ps1`,
`Tools/verify-unity.ps1`, `Tools/check-gates.ps1` и [AUTOMATION.md](AUTOMATION.md). Проверка repository прошла; gate validator показывает G0 PASS и G1–G9 TODO. Проверки не заменяют ручной плейтест и не публикуют изменения.

W1.1 / FR-011: `MatchRules` — чистые правила 45 очков, timeout 180 с,
одновременное достижение и Draw. Enum дополнен Draw; старое условие смерти
временно сохраняется только до W1.3, чтобы этот отдельный коммит был рабочим.
Добавлено 12 случаев, EditMode **21/21 PASS**; [лог с хешами входов](evidence/tests-W1.1-editmode.json).
G1 пока открыт: часы, интеграция и Game View ещё не проверены.

W1.1 зафиксирован в `f74d44c`; автоматизация — `d0f0ace`.
W1.2 / FR-012, NFR-005: `MatchClock`/`MatchState` управляют паузой, дробными
очками, ограничением последнего тика по 180 с и неизменностью завершённого матча.
Проверены 15/30/60/144 FPS, pause/resume/reset, simultaneous threshold, timeout:
EditMode **33/33 PASS** ([лог](evidence/tests-W1.2-editmode.json)).
Первый запуск проверок прервался на временной недоступности Pipeline при
domain reload; чтение статуса теперь имеет ограниченный повтор, повторный запуск
завершился успешно. Это сбой автоматизации, не выдуманный FAIL/PASS игровых тестов.
Следующий шаг — подключить модель к BattleController, остановить всю симуляцию
при паузе/исходе, показать Draw/счёт и проверить интеграцию в Main.

W1.2 зафиксирован в `99da84a`. W1.3 / FR-011/012/016: `BattleController`
использует единственный `MatchState`, тики 0,1 с и игровой timeScale.
Уничтожение команд не завершает матч; pause блокирует прямой HandleTap,
исход очищает выбор/цели, останавливает движение/атаку и вызывает Finished один раз.
Restart перезагружает Main, уничтожение контроллера восстанавливает timeScale.
Удалены четыре устаревших EditMode-теста уничтожения команд; их правило
заменено интеграционными тестами продолжения матча. Добавлены пять PlayMode случаев.

Компиляция PASS, EditMode **29/29**, PlayMode **43/43**, без failed/skipped/inconclusive. Логи: [EditMode](evidence/tests-W1.3-editmode.json), [PlayMode](evidence/tests-W1.3-playmode.json). Main: pause сохраняла 1,5 с между наблюдениями; result Victory 45:0 остановил
время; Restart вернул 0:0/InProgress и 6 юнитов ([наблюдения](evidence/W1-main-observations.txt)). Первая проверка экрана обнаружила обрезанный счёт; BestFit исправлен,
PlayMode повторно прошёл 43/43, [Game View](evidence/W1-main-victory.png) осмотрен. Исход в Main вызван техническим тестовым вводом скорости очков: A/B пока отсутствуют. Это проверка интеграции через CLI, не человеческий плейтест. Решение: [ADR-002](../adr/002-match-time-and-outcomes.md). G1 закрыт; G2–G9 открыты. Android baseline не выдаётся за APK с новым кодом.

W1.3 зафиксирован в `e5cbf53`. W2.1 / FR-007/008: `RelayConfig` хранит
kind/radius/capture/score/heal, `RelayState` моделирует [-1,+1]. Эффект считается
только для части тика после полного захвата, не за потраченное на capture время.
Число союзников не ускоряет захват; contest блокирует эффект; пустой захваченный
узел работает, частичный прогресс сохраняется. Добавлены 13 EditMode случаев,
итог **42/42 PASS** ([лог](evidence/tests-W2.1-editmode.json)).
Первый тестовый прогон **38/42 PASS, 4 FAIL**: точное сравнение дробного времени
с нулём вместо допуска; исправлен тестовый tolerance 1e-6, исходный
[FAIL-лог](evidence/tests-W2.1-attempt1-editmode.json) сохранён. Начальная ошибка
доступа к private Epsilon исправлена до запуска тестов; константа теперь internal.
G2 остаётся открытым: объектов A/B/C, начисления и ремонта в Main ещё нет.

W2.1 зафиксирован в `3a2a9c7`. W2.2 / FR-007/008: `RelayPoint` выбирает живых
бойцов по горизонтальному радиусу на тике матча; trigger используется для
нажатия, а не как источник истины о присутствии. Смерть учитывается до Destroy.
Main содержит A/B/C с цветом команды, долей захвата и contested `!`.
`RelaySceneSetup` — идемпотентная Editor-миграция; запуск игры её не требует.
Старые prefab GUID, геометрия и NavMesh сохранены. Сохранение сцены нормализовало
MaxSize старого текста счётчика 40→64 при FontSize 64/BestFit off; поведение не меняется.

PlayMode **47/47 PASS** ([лог](evidence/tests-W2.2-playmode.json)).
В Main **18/18** подходов: каждая точка доступна каждому из шести бойцов;
технический сценарий Warp размещал союзников на узлах, затем обычные тики
полностью захватили A/B/C ([наблюдения](evidence/W2-scene-check.txt)).
[Частичный захват](evidence/W2-partial.png) и [полное владение](evidence/W2-owned.png)
осмотрены. Подписи увеличены и вынесены перед роботами после первого осмотра.
При этой правке исправлена ошибка имени локальной переменной в Editor-скрипте,
после чего компиляция и повторный PlayMode прогон прошли. Неудачная попытка
войти в Play при compile error не засчитана; сохранены только успешные наблюдения.
`verify-unity.ps1` ждёт несколько устойчивых статусов после Refresh.
Команды игрока к узлам, очки и фактический ремонт пока не подключены; G2 открыт.

W2.2 зафиксирован в `3d2ab70`. W2.3 / FR-004: прямое нажатие на trigger узла
выдаёт выбранному бойцу приказ к точке внутри радиуса с полным NavMesh-путём.
Невозможный подход отклонён без отмены прежней атаки. Принятый приказ очищает
атаку и показывает обычный destination marker. Добавлены PlayMode-проверки
реального перемещения/захвата по tap и сохранения цели при отказе:
**49/49 PASS** ([лог](evidence/tests-W2.3-playmode.json)).
Первый запуск автоматизации остановился по timeout: Main ожидала подтверждения
Reload после внешнего удаления пробелов в новых YAML-строках W2.2. Текст окна
проверен, сохранённая Main перечитана через кнопку Reload; затем настоящий
прогон завершился. Ни timeout, ни сообщение CLI `Entered play mode` при ошибке
компиляции не считаются доказательством запуска тестов/игры.

W2.3 зафиксирован в `a578f6f`. W2.4 / FR-009/011: `BattleController` суммирует
дробные очки A/B после захвата; Repair не начисляет очки. Публичная симуляция
также разбивается на тики 0,1 с, поэтому длинный кадр останавливается при первом
пороге, а не после всей переданной длительности. Временные параметры test score
удалены; интеграционные исходы теперь получают очки от настоящих RelayPoint.
Добавлены пять сценариев; **54/54 PlayMode PASS** ([лог](evidence/tests-W2.4-playmode.json)).
Первый прогон 53/54: старый тест точного progress 0,4 не учитывал многотиковое
сложение; tolerance исправлен, [первый FAIL-лог](evidence/tests-W2.4-attempt1-playmode.json) сохранён.
В Main технический сценарий с размещёнными на узлах бойцами завершился
**Victory 45:0** от обычного начисления за A/B, на игровом времени 28,7 с
([наблюдения](evidence/W2-score-check.txt), [экран](evidence/W2-score.png)).
Скрипт не подавал искусственные скорости очков. Это проверка интеграции,
не человеческая тактика/плейтест; HUD и визуальный фон результата дорабатываются в W6.

W2.4 зафиксирован в `b8e5cd7`. W2.5 / FR-010: `Health.Heal` возвращает
фактическое лечение, ограничивает Max без переполнения int и не воскрешает.
C хранит дробный остаток отдельно для каждого живого бойца; не копит лечение
при полном HP, очищает остаток при уходе/смерти/утрате владельца, не лечит в contest.
Остатки во время паузы не продвигаются. `DamageFlash` реагирует только на уменьшение
HP; лечение продолжает обновлять существующую HealthBar через Changed.

Итог: compile PASS/0 console errors, **46/46 EditMode**, **61/61 PlayMode**,
failed/skipped/inconclusive=0 ([EditMode](evidence/tests-W2.5-editmode.json),
[PlayMode](evidence/tests-W2.5-playmode.json)). Проверены Player/Enemy и 15/30/60/144 FPS. Main: HP после damage/capture **190**, после 3 с ремонта **208**, на паузе и
при споре остаётся **208** ([лог](evidence/W2-heal-check.txt));
[жёлтый спорный C](evidence/W2-heal-contested.png) осмотрен. Сценарий использовал
техническое размещение юнитов и обычную симуляцию, не человеческий плейтест. G2 закрыт. AI, respawn, мобильный HUD и итоговый APK ещё впереди.

W2.5 зафиксирован в `b1f63f5`. W3.1 / FR-014: `EnemyDecisionRules` выбирает
defend → capture scoring → repair при HP≤35% → attack interference.
При безопасных очковых узлах и отсутствии помех можно захватить C или
удерживать очковый узел. Стоимость назначения позволяет разделить роли;
недостижимые цели исключены, равенство разрешается порядком A/B/C.
Семь новых EditMode случаев, **53/53 PASS** ([лог](evidence/tests-W3.1-editmode.json)).
Правила не изменяют скорость/HP/урон юнитов. Подключение к тикам и проверка
боёв — следующая задача; G3 открыт.

W3.1 зафиксирован в `359778b`. W3.2 / FR-014/016: `EnemyTacticsController`
подписан на Ticked, работает раз в игровую секунду, использует существующие
UnitMovement/UnitAttack и реальные UnitStats. Порядок — скорость/имя роли/регистрация;
назначения разделены стоимостью, прежний полный путь не повторяется без причины.
При отключении контроллера подписка и ссылки очищаются. Pause/finish не дают тиков AI.
Unity 6000.6 удалил GetInstanceID: первоначальная compile error исправлена
использованием порядка регистрации; новый глобальный ID-сервис не вводился.

PlayMode **65/65 PASS** ([лог](evidence/tests-W3.2-playmode.json)).
Main с timeScale=4: на 28,2 игровой с AI выполнил 28 решений и 10 move orders,
захватил A/B/C, счёт 2:31,9 ([лог](evidence/W3-main-ai.txt)). К моменту
следующего [скриншота](evidence/W3-main-ai.png) обычный матч завершился Defeat 2:45.
Роботы действительно двигались/сражались, Warp/test score в этом сценарии не применялся.
Это ускоренная техническая партия без приказов игрока, не человеческий плейтест.

Unity при сохранении возвращает пробел после пустых YAML-полей. Для `unity-yaml`
в `.gitattributes` отключена только проверка blank-at-eol; C#/docs сохраняют
обычную whitespace-проверку. Это устраняет повторное ручное форматирование сцены
и вызванное им модальное Reload; сериализация/старые GUID сохранены. Следующий шаг — серия партий с разными игровыми планами; G3 пока открыт.

W3.2 зафиксирован в `116bb28`. W3.3 / FR-011/014/016: новый MainMatchTests
загружает настоящую сцену четырежды, проверяет 6 стартовых бойцов/нулевой счёт,
отдаёт обычные приказы и ждёт исхода при timeScale=4. Проверяет остановку боя
и полную очистку Unit.All после unload. **67/67 PlayMode PASS**
([лог](evidence/tests-W3.3-playmode.json)); чистые правила AI ранее 53/53.

Первая серия из трёх планов прошла 66/66, но все планы проиграли.
[Исходные метрики](evidence/W3-main-matches-initial.csv) и
[лог](evidence/tests-W3.3-initial-playmode.json) сохранены. Проверка группового
приказа выявила: несколько союзников мешают точному прибытию в центр узла,
из-за IsMoving автоатака не начинается. Приказ к relay теперь использует
стоп-дистанцию внутри его радиуса с запасом 0,15 м; MoveTo на землю/для атаки
сохраняет обычный малый допуск. Это одинаковое API для игрока и AI.
Добавлен тест группы: все остановились в круге и автоматически атаковали врага.

Финальная [серия](evidence/W3-main-matches.csv):

| План | Время игры | Счёт | Исход |
| --- | --- | --- | --- |
| Без приказов | 50,0 с | 6,8:45 | Defeat |
| Tank→A, Scout→B, Ranger→C | 37,2 с | 1,7:45 | Defeat |
| Все на A | 37,0 с | 1,2:45 | Defeat |
| Удержать A, на 12 с Scout→B | 58,5 с | 45:8,2 | Victory |

Наблюдение: смена цели после удержания приносит больше простого начального
разделения/скопления. Это конкретные результаты одного автоматического прогона,
не гарантированные будущие цифры и не человеческий плейтест. Детерминированы
выбор AI на одинаковом снимке и правила; NavMesh/бой зависят от кадров Unity.
После respawn и доработки карты баланс повторяется в W7. Геометрия baseline пока
асимметрична; выравнивание арены/стартов и повторная NavMesh-проверка остаются в W5/W7.
G3 закрыт. Следующее — пакет VContainer с pinned version, затем настоящий scope.

W3.3 зафиксирован в `78e3e8b`. W4.1 / FR-021, NFR-011: VContainer 1.19.0 установлен
из официального Git UPM URL; lock содержит commit
`5401e5a7ebc4980a2b82141ffc26391a6547edd7`.
[Запись пакета](evidence/package-W4.1.json), [53/53 EditMode PASS](evidence/tests-W4.1-editmode.json).
Первая попытка Refresh получила timeout во время UPM resolve (41,53 с);
тесты запущены после возвращения Editor в ready. Этот timeout не считается PASS.
Manifest/lock, лицензия MIT и notices включены в отдельный установочный коммит.
Main/игровые службы на этом шаге не менялись; G4 остаётся открытым до проверки scope.

W4.1 зафиксирован в `b5c1db7`. W4.2 / FR-021: GameLifetimeScope добавлен
к существующему Battle в Main через Editor API, без переписывания сцены.
Clock/State Scoped; Battle и AI — зарегистрированные компоненты с Inject.
[ADR 004](../adr/004-match-composition-root.md). **70/70 PlayMode PASS**
([лог](evidence/tests-W4.2-playmode.json)): общие state/clock, новые экземпляры
после dispose, ошибки недостающей регистрации и четыре реальные партии Main.
[Метрики этого прогона](evidence/W4-scope-main-matches.csv) сохранены отдельно;
G3 CSV восстановлен из его коммита после обнаружения перезаписи тестом.
В W4.3 тестовый runner получит отдельное имя evidence на каждый task.
Старый Awake fallback пока сохранён для прежних fixtures; его удаление — W4.3.

W4.2 зафиксирован в `0badd72`. W4.3 / FR-021: fallback Battle.Awake и
serialized battle у AI удалены. Без injection Start сообщает конкретную ошибку;
изолированные fixtures создают модель явно. MainMatchTests проверяет новую
identity State/Clock при каждой смене сцены. Runner передаёт task в Editor,
каждая серия пишет отдельный CSV и больше не заменяет исторический evidence.
**72/72 PlayMode PASS** ([лог](evidence/tests-W4.3-playmode.json)), включая
две ожидаемые ошибки неправильной сборки. Четыре партии снова завершились,
последняя дала Victory 45:42,5 ([CSV](evidence/W4.3-main-matches.csv)).
В предыдущем W4.2 прогоне все четыре плана проиграли; результаты сохранены,
баланс ещё не принят. G4 закрыт; factory/async ownership внедряются в W5.

W4.3 зафиксирован в `f1bf936`. W5.1 / FR-023, NFR-011: Addressables 2.11.2
из официального Unity registry; Scriptable Build Pipeline 3.0.3 и встроенные
AssetBundle/UnityWebRequest — разрешённые транзитивные зависимости. Версии
Editor/URP/Input System/Test Framework не менялись. License notice скопирован
из установленного пакета. [Метаданные/lock](evidence/package-W5.1.json),
[53/53 EditMode PASS](evidence/tests-W5.1-editmode.json). Это отдельный package
commit; local content build выполняется в W5.3 после создания entries и
повторяется для финального APK в W8. G5 ещё открыт.

W5.1 зафиксирован в `3c7475b`. W5.2 / FR-022, NFR-011: UniTask 2.5.11
из официального Git UPM URL, commit `2e993ff18f28c931602a07292df0b0804eebef99`.
MIT сохранён в notices. [Метаданные/lock](evidence/package-W5.2.json),
[53/53 EditMode PASS](evidence/tests-W5.2-editmode.json). Начальный Refresh
не прошёл из-за domain reload; тестовый прогон выполнен после ready. Игровой
async не выдаётся за проверенный на установочном шаге: это следующая W5.3/W5.4.

W5.2 зафиксирован в `97445f2`. W5.3 / FR-021/022/023, NFR-006: локальная
группа Local Units с шестью существующими prefab variants. Content построен
для Android: **Succeeded**, 44,441 с; localunits bundle 139 077 байт,
monoscripts bundle 1 849 байт, catalog/settings + SHA-256 в
[manifest](evidence/W5-local-content-manifest.json). Это content, ещё не APK. Первый RPC Configure timeout фактически завершил настройку. Content задержан
первым модальным Build Report preference; диалог проверен и локальный report
включён. Ожидание/дополнительный RPC получили timeout, они не выдаются за PASS;
успех подтверждён отдельным фактическим результатом и файлами Unity.

IUnitPrefabProvider → UnitFactory зарегистрированы Scoped. Provider ожидает
Addressables через UniTask, передаёт lease; UnitPrefabOwnership освобождает его
в OnDestroy, не раньше deferred Destroy. Factory удерживает экземпляры и
отменяет свой lifetime при dispose. [ADR 005](../adr/005-async-prefab-ownership.md).
**74/74 PlayMode PASS** ([лог](evidence/tests-W5.3-playmode.json)): настоящий
Tank загрузился по ключу, создан на NavMesh и умер с единственным release;
невозможная NavMesh позиция освободила prefab без юнита. Прежние Main партии
продолжены ([CSV](evidence/W5.3-main-matches.csv)); пока они используют статические
стартовые экземпляры, миграция всех шести — W5.5. Отмена/missing key — W5.4.
Content evidence теперь получает task/UTC-имя, не переписывает прошлые отчёты;
после правки имени повторно проверена компиляция и [53/53 EditMode](evidence/tests-W5.3-editmode.json).

W5.3 зафиксирован в `4c9ef8f`. W5.4 / FR-022/027, NFR-006: **79/79
PlayMode PASS** ([лог](evidence/tests-W5.4-playmode.json)). Проверены
caller cancellation до запроса/во время задержанной загрузки, dispose factory
во время загрузки, late lease без ghost unit/повторного release, уничтожение
реального EnemyRanger при dispose, отсутствующий ключ без зависания/остатка.
Runner теперь хеширует все неигнорируемые Assets, включая Addressables settings.
Реальный Main restart во время загрузки проверяется после миграции стартов в W5.6.

Первый прогон дал **77/79**, сохранены [XML](evidence/tests-W5.4-attempt1-playmode.xml),
[Pipeline JSON](evidence/tests-W5.4-attempt1-pipeline.json) и [партии](evidence/W5.4-attempt1-main-matches.csv).
InvalidKeyException записывается Addressables как Error, ожидался Exception;
затем возникла отложенная ошибка форматирования stack trace. После прогона
Unity аварийно завершился: Windows Event 1000, ntdll.dll, 0xc0000374
([запись](evidence/W5.4-editor-fault.json)). Причина native crash не установлена;
нельзя объявлять её исправленной по одному успешному повтору.
Editor перезапущен; recovery scene сохранена также вне проектного Temp,
SHA-256 `825B969638F0ADB10A70BEFBE1AE1CCD8194A14BC838BB7C335BA465413E6617`.
Provider теперь сначала выполняет LoadResourceLocationsAsync: пустой список
даёт обычную понятную ошибку без InvalidKeyException logger, lookup handle
освобождается в finally. Счётчики учитывают оба вида operations. Повторный
прогон без неожиданных логов прошёл; стабильность Editor проверяется далее.

W5.4 зафиксирован в `1395333`. W5.5a / FR-003/013: чистый RespawnTimer
получает только принятый MatchClock delta, хранит 10 с и не сбрасывается от
повторного death notification. UnitRoster проверяет ровно по одному Tank/Scout/Ranger
каждой команды; данные asset не хранят runtime countdown/unit references.
**63/63 EditMode PASS** ([лог](evidence/tests-W5.5a-editmode.json)): countdown,
pause/finish, 15/30/60/144 FPS, invalid delta и неверный roster.
Это первый небольшой коммит W5.5; старт/возрождение Main ещё не подключены.

W5.5a зафиксирован в `ca8ff08`. W5.5b / FR-003/013/022/023: шесть прежних
scene prefab instances заменены UnitRoster + UnitLifecycle через один scoped
UnitFactory. Исходные prefab assets/UnitStats и NavMesh не заменены. Countdown
обновляется принятым игровым delta; late respawn load на pause ожидает resume.
Безопасный spawn проверяется до и после загрузки. Занятая база откладывает
возрождение, завершение матча отменяет countdown/pending requests.

**84/84 PlayMode PASS** ([лог](evidence/tests-W5.5b-playmode.json)):
все шесть roles/team respawn через 10 с, full HP/NavMesh, повторный lethal damage,
pause, wipe команды без окончания, blocked base/defer и cancel при finish;
loaded prefab на pause не создаёт clone. Четыре полных Main партии завершились,
в этом прогоне все Defeat ([CSV](evidence/W5.5b-main-matches.csv)); баланс W7 открыт.
После unload тесты проверяют ноль Unit.All и owned handles.

Первый и второй прогоны дали 83/84, сохранены отдельно
([1](evidence/tests-W5.5b-attempt1-playmode.json), [2](evidence/tests-W5.5b-attempt2-playmode.json)).
Ранняя гипотеза о причине — общий precheck radius — не подтвердилась как причина
FAIL. Радиус всё же приведён к фактическому NavMeshAgent роли. Диагностика второго
прогона показала нормальное завершение матча по очкам раньше пятого respawn:
isolated lifecycle fixture теперь исключает relay scoring, полные Main партии
сохраняют настоящие цели/бой. Runner timeout увеличен до 600 с для расширенной
регрессии; текущий прогон завершился за штатное время без timeout.

В отдельном обычном Play Mode просмотрены [сцена](evidence/W5.5-main.png) и
[текущее состояние](evidence/W5.5-main-observation.txt): Ready, 13,1 игровых с,
три оставшихся юнита на NavMesh, три owned handles; погибшие slots waiting. Это наблюдение реального боя, не доказательство человеческого playtest. HUD ещё baseline alive counter, score/time/respawn UI — W6.

W5.5b зафиксирован в `89968e1`. W5.6 / FR-017/022/027, NFR-006:
**88/88 PlayMode PASS** ([лог](evidence/tests-W5.6-playmode.json)).
[10 настоящих Restart](evidence/W5.6-main-restarts.csv) сбросили state/clock,
scores/partial relay progress/countdowns; каждый новый матч — 6 units/6 owned
handles, у каждой старой factory — 0 instances. После unload — ноль handles/units.
Реальный Main restarted из Loading с задержанной доставкой actual prefab lease;
старый token cancelled, поздний lease освобождён без ghost unit.

MissingKeyProvider в тесте направляет третий load на несуществующий ключ
настоящего AddressableUnitPrefabProvider. Две успешные загрузки очищаются;
Failed panel активен, Retry/Exit доступны, часы остановлены. Нажатие настоящего
Retry button запускает свежий Main; отдельно проверены обе UI actions и
отсутствие повторных listeners. Exit native Application.Quit на телефоне — W8.
Failure очищает IsSpawning/countdowns. Партии сохранены [отдельно](evidence/W5.6-main-matches.csv).

Первый прогон — 87/88 ([лог](evidence/tests-W5.6-attempt1-playmode.json)):
test double пытался RegisterInstance второго UnitRoster того же implementation
type; VContainer справедливо отклонил конфликт. Тест заменяет IUnitPrefabProvider
другим implementation, сохраняя настоящую проверку missing Addressables key.
Начальный [restart CSV](evidence/W5.6-attempt1-main-restarts.csv) и
[партии](evidence/W5.6-attempt1-main-matches.csv) не заменены.

W5.6 зафиксирован в `b64f080`. G5: промежуточный [Android build](evidence/android-build-W5-G5.json)
**Succeeded**, 373,580 с, 0 build errors, 1 warning (Pipeline отключён в Player,
RuntimePipelineConfig отсутствует). APK 32 513 517 байт, SHA-256
`CBDDB4225780D0E6D406DD7A78EFCA227A8DF62FA3EC94C77946C7B327BFF57D`.
ZIP manifest подтверждает localunits/monoscripts bundles, catalog/settings
внутри APK; hashes bundles совпадают с content build. Tools/build-android.ps1
сохраняет входные hashes, фактический BuildReport и APK content hashes и
не заменяет уже существующий APK. Полный BuildReport остаётся в игнорируемом Temp.

Unity временно добавил существующий InputSystem_Actions.inputactions
GUID `052faaac586de48259a63d0c4782560b` в preloadedAssets при Player build. После Editor/test reload пакет вернул список к исходному пустому состоянию. Исходник установленного Input System (ProjectWideActionsBuildProvider /
BuildProviderHelpers.PostProcessSinglePreloadedAsset) подтверждает временную
preload/cleanup модель. Временная запись попала в d6532d7; теперь возвращена
к штатному исходному значению. Иных изменений PlayerSettings нет. APK успешно установлен через `adb install -r`, активен на Android:
[наблюдение](evidence/W5-G5-device.json), [сцена](evidence/W5-G5-device-main.png),
[второй кадр](evidence/W5-G5-device-observation2.png), [лог](evidence/W5-G5-device-log.txt). G5 PASS подтверждает цепочку DI → UniTask → локальные Addressables в Editor/Player.

Стартовый лог Player **не чистый**: Java ClassNotFoundException для
com.google.android.play.core.assetpacks.AssetPackManager, затем бой продолжился;
URP сообщает об unsupported/stripped неиспользуемых post-processing shaders. Причины и исправление проверяются в Android regression W8; NFR-003 ещё PARTIAL. Это pilot APK с версией baseline 0.1.0, не финальный v1.0. Offline и performance
не приняты. Следующий milestone — input/camera/HUD W6.

G5 зафиксирован в `d6532d7`. W6.1 / FR-006: UnitCommander обрабатывает release,
drag с порогом 3 мм, pinch и cancel. Drag не превращается в tap после возврата
к началу; после второго пальца тап подавлен до полного release. Gesture,
начатый над UI, не даёт world orders/pan/zoom. EventSystem.RaycastAll использует
свежую позицию, не результат предыдущего UI Update. Mouse wheel/UI raycast
обрабатываются только при реальном wheel input; idle mouse не вызывает raycast.
PanRequested/ZoomRequested пока ждут потребителя камеры W6.2.

**93/93 PlayMode PASS** ([лог](evidence/tests-W6.1-playmode.json)): virtual
touch/mouse release, drag, pinch, cancel, UI; прежние Main/respawn/restart
regressions также прошли ([партии](evidence/W6.1-main-matches.csv),
[restarts](evidence/W6.1-main-restarts.csv)). Первый прогон — 92/93:
UI fixture проверял GraphicRaycaster до Canvas registration/layout первого
кадра. Добавлен реальный кадр перед проверкой; [FAIL](evidence/tests-W6.1-attempt1-playmode.json)
и [предыдущие партии](evidence/W6.1-attempt1-main-matches.csv)/
[restarts](evidence/W6.1-attempt1-main-restarts.csv) сохранены.
FR-006 остаётся PARTIAL до камеры и проверки обновлённого Player на телефоне.

W6.1 зафиксирован в `119cd44`. W6.2 / FR-006: TacticalCamera на прежней Main
Camera слушает PanRequested/ZoomRequested. Ray → ground plane сохраняет точку
под пальцем; focus ограничен ±2,5 м, distance 9–22 м, угол 55° фиксирован.
Никаких контейнерных Resolve или async в камере. **99/99 PlayMode PASS**
([лог](evidence/tests-W6.2-playmode.json)): virtual drag/pinch меняют реально
подписанную камеру без selection/orders; проверены bounds, anchor under finger,
zoom и invalid factors. Прежние [партии](evidence/W6.2-main-matches.csv) и
[10 restarts](evidence/W6.2-main-restarts.csv) тоже прошли.

Начальный Refresh RPC попал в domain reload и не запустил тесты; повтор сделан
после фактического ready/успешной компиляции. В отдельном Main Play Mode
проверены [Pan/Zoom](evidence/W6.2-camera-observation.txt) и
[вид сцены](evidence/W6.2-camera.png): Ready, focus -2,41 по X, distance 14,4,
угол 55°. Это Editor API observation; физический pinch обновлённого APK ещё
проверяется после W6.3–W6.5. HUD/safe area — следующий шаг.

W6.2 зафиксирован в `c623e8c`. W6.3 / FR-015: HUD читает тот же MatchState,
RelayPoint и реальные slots, показывает счёт, время, прогресс/contest A/B/C,
HP/respawn трёх ролей и характеристики выбранного бойца. Кнопки ролей и узлов
вызывают настоящий commander; Pause/Resume управляет игровыми часами. Смерть
выбранного бойца немедленно снимает selection и marker. Камера оставляет место
для верхней/нижней панели; тап вне её viewport не отдаёт приказ.

**103/103 PlayMode PASS** ([JSON](evidence/tests-W6.3-playmode.json)), включая
реальные кнопки, HP/respawn, score/time/pause, UI blocking и смерть selection;
Main партии и 10 restart также прошли. Первоначальный прогон дал 91/103:
Hud.Awake читал Relay.State до Relay.Awake. Первое обновление перенесено в Start;
[ошибочный прогон](evidence/tests-W6.3-attempt1-playmode.json) сохранён. Отдельный Main Play Mode: [HUD](evidence/W6.3-hud.png), [наблюдение](evidence/W6.3-hud-observation.json),
Ready, pause=True, выбран Tank. PNG 1280×720 — масштабированный кадр Game View
861×471; это ещё не доказательство трёх форматов FR-028. Safe area/форматы — W6.4. ADB снова видит Android; пользователь разрешил запуск/перезапуск игры
для тестов. Новые мобильные проверки будут использовать обновлённый APK.

W6.3 зафиксирован в `cabf1ee`. W6.4 / FR-028: один SafeAreaLayout применяет
Screen.safeArea к родителю HUD/result; камера использует тот же нормализованный
прямоугольник и исключает панели. Editor-only GameViewVerification выбирает
настоящий fixed render size, затем размер проверяется через Screen.
[16:9](evidence/W6.4-16x9.png), [20:9](evidence/W6.4-20x9.png), [4:3](evidence/W6.4-4x3.png)
просмотрены; [наблюдения](evidence/W6.4-layout-observations.txt) подтверждают
1280×720, 1600×720 и 1024×768. Проверка углов всех HUD/result buttons прошла.
**104/104 PlayMode PASS** ([JSON](evidence/tests-W6.4-playmode.json)): simulated
notch/system insets также удерживают HUD, Retry/Exit и camera внутри safe area.

Первоначальные снимки сохранены с attempt1: при смене aspect частичный camera
viewport оставлял старые UI/world pixels в полях. BackdropCamera без геометрии
(cullingMask=0, depth=-100, full viewport) очищает весь кадр до world camera.
Повторные снимки проверены после исправления, артефакт устранён. Это маленькая
дополнительная камера очистки, без AudioListener/DI/async; AR/другие рендереры
не вводились. Native Android safe insets проверяются обновлённым APK после W6.5.

W6.4 зафиксирован в `e3510d8`. W6.5 / FR-019, FR-012: TutorialView подписан на
реальный Lifecycle.Ready; на первом запуске показывает три коротких подсказки
после создания юнитов и ставит матч на паузу. Next/Skip сохраняет preference,
закрытие возвращает предыдущее состояние паузы. Help позволяет повторить;
Pause button во время modal недоступна, world input отключён. Ready/Finished
подписки снимаются OnDisable; отдельного async/tutorial state service нет.

**107/107 PlayMode PASS** ([JSON](evidence/tests-W6.5-playmode.json)): first launch,
все три Next, Skip → настоящий Restart, сохранение existing pause при Help,
modal input blocking; прежние сценарии и 10 restart также PASS. Main fixtures
закрывают tutorial production action и сохраняют/восстанавливают Editor preference,
чтобы тесты не отменяли обучение человеку. [Первый](evidence/W6.5-tutorial-1.png),
[второй](evidence/W6.5-tutorial-2.png), [третий](evidence/W6.5-tutorial-3.png) экраны
просмотрены; [наблюдение](evidence/W6.5-tutorial-observation.txt): 1280×720,
showing=True, paused=True. После ручного просмотра preference также восстановлен.
G6 ещё IN_PROGRESS до обновлённого APK на устройстве; это не финальная v1.0 сборка.

W6.5 зафиксирован в `7e24327`. Обновлённый [APK W6-G6](evidence/android-build-W6-G6.json):
Succeeded, 121,093 с, 0 errors, 1 Pipeline-config warning; 32 533 149 байт,
SHA-256 B33571E23C0F01DA602E70B71036B94ED7FC0D176FBA43EDA56D9E31AB945134.
Установка -r Success; просмотрены [Main](evidence/W6-G6-device-main-first.png),
[Defeat](evidence/W6-G6-device-result.png), [Help](evidence/W6-G6-device-help.png).
Пользователь подтвердил swipe/pinch на этом APK без случайных приказов
([наблюдение](evidence/W6-G6-device.json)). Полный человеческий playtest G7 не заявляется.
Временный Input System preload после build снова возвращён к исходному [].

G6 остаётся IN_PROGRESS: системная Android navigation bar видна и закрывает
правые поля HUD. Три Editor aspect проходят, но native insets требуют отдельного
исправления W6.4b до PASS. Нативный AssetPackManager probe и URP shader notices
по-прежнему присутствуют в [raw log](evidence/W6-G6-device-log.txt); gameplay/Restart работают.

W6.4b / G6: AndroidSystemBars использует существующий Unity AndroidWindowInsets API
на старте и при возврате фокуса; системные панели можно временно показать свайпом.
HUD продолжает применять настоящий Screen.safeArea. Main изменён на 13 YAML-строк,
добавлен только компонент к прежнему SafeArea; арена и существующие GUID сохранены.
Первый Player build остановлен CS1069: встроенный Android JNI module отсутствовал.
[Ошибки](evidence/W6.4b-attempt1-player-errors.json) и [FAIL build](evidence/android-build-W6-G6-insets.json)
сохранены. Через Package Manager установлен com.unity.modules.androidjni 1.0.0.

**107/107 PlayMode PASS** ([JSON](evidence/tests-W6.4b-playmode.json)),
**63/63 EditMode PASS после подключения JNI** ([JSON](evidence/tests-W6.4b-final-editmode.json)).
[Повторный APK](evidence/android-build-W6-G6-insets-retry.json): Succeeded,
99,815 с, 0 errors, 1 Pipeline-config warning; 32 542 757 байт,
SHA-256 71C87D0EDE75057098440C712F4E2AF0721B697643F70E66FB0B983DEA556C55.
Input System build preload возвращён к исходному [] и в Editor API, и на диске.

[На телефоне](evidence/W6-G6-insets-device.png) HUD виден полностью; после Home и
возврата [result](evidence/W6-G6-insets-focus-return.png) также без перекрывающей панели.
[Player log](evidence/W6-G6-insets-device-log.txt) содержит реальные Screen/safeArea.
[Наблюдение](evidence/W6-G6-insets-device.json) отделяет эти проверки от неподтверждённых:
последующие тапы сделаны уже на Defeat overlay, поэтому не засчитываются как приказ. **G6 PASS**, FR-006/028 PASS. G7–G9 ещё впереди; native probe и URP notices остаются
открытыми в W8, полная приёмка v1.0 не заявляется.

W0.6 / NFR-010: по отдельному запросу пользователя проведён полный аудит приватности.
[Отчёт](PRIVACY_AUDIT.md), [машинное evidence](evidence/privacy-audit.json): 70 коммитов,
786 Git blobs, 29 LFS objects, Gitleaks 8.30.1 — 0 секретов. Личные метаданные
автора и Windows paths найдены; текущие 57 путей в 47 файлах скрыты, JSON results
семантически сверены. Raw originals сохранены вне Git. NFR-010 остаётся PARTIAL
из-за опубликованной истории, очистка рабочего дерева не выдаётся за удаление истории.

Новые commit identity: публичный ник + noreply; локальный core.hooksPath=.githooks.
[Пять настоящих staged-проверок](evidence/privacy-gate-checks.json): блокируются
личный путь, почта, private-key header и .env; безопасный relative path проходит,
значения не печатаются. Первый fixture выявил, что default Gitleaks не блокирует
невалидный PRIVATE KEY header; добавлено явное правило и повторный прогон PASS.
**63/63 EditMode PASS** через обновлённый wrapper ([JSON](evidence/tests-PrivacyGate-editmode.json)),
личный projectPath в этом новом отчёте автоматически скрыт. Игровой код не менялся.
Проверка repository/gates PASS; G7–G9 всё ещё TODO.

Пользователь также поручил возобновление после сброса лимита. Настройка и prompt
хранятся вне Git в локальном каталоге приложения; thread ID и account usage не
публикуются. Отдельная Windows scheduled task запускает CLI resume этого чата;
это не результат игрового теста и не доказательство завершения W7–W9.

W7.1 / FR-018, FR-012: MatchAudio на существующем HUD слушает UnitAttack.Fired,
Battle.Ticked (смена владельца) и Battle.Finished. Четыре оригинальных коротких
процедурных тона создаются один раз на сцену; AudioSource и AudioClip принадлежат
сцене, подписки снимаются и clips уничтожаются при teardown. Никаких внешних
ассетов/сервисов. Mute прекращает звук, сохраняет preference и переживает Restart.
В Main добавлены только Mute button и audio component/source (316 YAML-строк);
HudSceneSetup умеет добавить audio без пересоздания остального UI. Обучение
объясняет жесты русскими словами вместо Drag/pinch.

Устранён реальный pause edge case: первое готовое оружие раньше могло стрелять
при Time.timeScale=0. UnitAttack теперь пропускает paused/dead update; тест проверяет
отсутствие damage/Fired на паузе и настоящий удар после Resume.
**110/110 PlayMode PASS**, Failed/Skipped/Inconclusive=0
([JSON](evidence/tests-W7.1-playmode.json)), прежние 4 партии и 10 restarts PASS.
Audio tests проверяют реальный выстрел/capture/Victory, AudioSource playback,
кнопку mute, настоящий Restart и отсутствие оставшихся clips/handles/units.

Отдельный Main Play Mode: [наблюдение](evidence/W7.1-audio-observation.json),
44 shot events, 3 captures, 1 natural Defeat result; lifecycle уже Closed,
поэтому ready=false честно сохранён. Mute UI callback дважды переключён, исходный
Audio preference восстановлен. [Результат](evidence/W7.1-audio.png) и
[новый матч на паузе](evidence/W7.1-feedback.png) просмотрены, true Game View 1280×720.
Это Editor/API проверка звука, не прослушивание человеком на Android; FR-018 PARTIAL
до обновлённого APK. [Четыре прежние стратегии](evidence/W7.1-main-matches.csv)
снова дали Defeat (33,5–51,3 игровых секунд), что требует проверки старой асимметрии
стартов и тактик в W7.2. G7 IN_PROGRESS; пользовательский полный playtest ещё не заявлен.

Продолжение 2026-10-12: после настройки core.hooksPath Git LFS восстановил четыре
стандартных hook (post-checkout, post-commit, post-merge, pre-push) в .githooks.
Они сохранены в Git: новый privacy pre-commit должен сосуществовать с LFS,
иначе на другом checkout можно потерять автоматическую доставку LFS objects.
Обзор всех четырёх файлов: только стандартные вызовы git lfs, приватных значений нет.
Это локальная настройка/фиксация hooks; push не выполнялся.

W7.2 / FR-002/003/014/026: исправлена реальная асимметрия старых узлов, баз
и боковых препятствий. Существующие объекты/префабы/UnitStats сохранены,
NavMesh перепечена с прежним GUID. [ADR-006](../adr/006-fair-arena-and-measured-tactics.md)
фиксирует решения и промежуточные FAIL, исходная сетка сохранена вне Git.
**112/112 PlayMode PASS** ([JSON](evidence/tests-W7.2-final-playmode.json)),
включая реальные спавны, 18 маршрутов, равные данные ролей, 10 restarts и cleanup.
Два прежних teardown теперь ждут физического завершения отменённого IO максимум
5 с и на каждом кадре требуют отсутствие позднего юнита; production ownership
не ослаблен. Первый и второй прогоны 109/112 и 111/112 сохранены отдельно.

После полной регрессии расширен только измеряющий тест тактик: **1/1 focused PASS**
([JSON](evidence/tests-W7.2-tactics-playmode.json)). Пять [реальных партий](evidence/W7.2-tactics-sustained-plans.csv)
содержат Victory ремонтной ротации 45:39 за 81,8 с, 21 приказ, 6/12 смертей;
остальные четыре дали Defeat. Это измерения, не человеческая игра и не обещание
победы одного плана. [Снимок](evidence/W7.2-arena.png) просмотрен, фактический
1280×720; [Main на паузе](evidence/W7.2-arena-observation.txt): Ready, 6 units/handles,
elapsed 0,3 с. Optional TestFilter wrapper проверен настоящим focused запуском.
W7.2 implementation PASS; G7 остаётся IN_PROGRESS до человека. Следующий пакет W8.

W8 / FR-004/007/014, NFR-003/004: full regression выявил реальный затор входа
в узел: прибывшие бойцы перекрывали третьему вход (complete path, moving=true).
Воспроизведён и для шести без боя, и для обычной тройки от баз. Диагностики/FAIL
сохранены; [ADR-007](../adr/007-relay-entry-and-unused-renderer-resources.md).
Общий arrival margin увеличен с 0,15 до 0,6 м; тот же механизм для AI/player.
**1/1 focused PASS**, затем **112/112 full PlayMode PASS**
([JSON](evidence/tests-W8-retry-playmode.json)), включая 18 маршрутов каждой
тройки от баз и десять restarts. Одновременное размещение шести неподвижных
агентов не заявляется проверенным. В [пяти партиях](evidence/W8-retry-sustained-plans.csv)
фланг дал Victory 45:15,5 за 58,5 с; split-pressure проиграл 43,1:45.

URP Mobile/PC больше не инициализируют выключенные post-processing ресурсы;
сохранены остальные shader/material/renderer settings. [Game View](evidence/W8-renderer.png)
просмотрен, 1280×720; **63/63 EditMode PASS** ([JSON](evidence/tests-W8-final-editmode.json)).
bundleVersion кандидата 1.0.0, исходная Unity/debug signing сохранены.
Устранение Android shader notices требует настоящего нового APK/device log,
не выводится из изменения settings. Сейчас далее APK и clean checkout.

W8 clean clone / NFR-002/004/005: первый публичный clone `0b2badb` без
Library/Temp/Builds и с LFS fsck импортирован, **63/63 EditMode PASS**.
Headless PlayMode дал **107/112**, пять старых movement/attack tests ждали
600 кадров вместо игровых секунд. [Первый отчёт](evidence/clean-clone-W8-attempt1.json)
и XML сохранены. Ожидания заменены на 10 игровых секунд без изменения assertions
или runtime; [focused retry 25/25 PASS](evidence/clean-clone-W8-focused-retry.json).
[ADR-008](../adr/008-game-time-in-navigation-tests.md) объясняет LF/CRLF,
generated URP settings и границы проверки. Исправление tests зафиксировано
в `acbd9bf`; [полная регрессия clone 112/112 PASS](evidence/clean-clone-W8-full-retry.json),
failed/skipped/inconclusive=0. Production code/geometry не менялись этим исправлением.

W8 / FR-004/015: пользователь слышит звук («чето слышно») и сообщил, что
выделение юнита оказывается под зоной. Подтверждён геометрический дефект:
SelectionRing общего prefab был ниже поверхности CaptureArea. Local Y поднят
с -0,98 до -0,7; `.meta`, масштабы, collider/NavMesh/боевые правила неизменны.
[Реальные bounds всех трёх ролей](evidence/W8-selection-visibility.txt): минимальный
зазор над зоной 0,109 м. [Game View](evidence/W8-selection-visible.png) просмотрен:
обычный selected Scout имеет видимый жёлтый маркер поверх зоны. Это visual fixture
с техническим размещением prefab на узлах, не демонстрация захвата/победы.
Существующий MainArena routes **1/1 PASS**
([JSON](evidence/tests-W8-selection-playmode.json)); далее APK и подтверждение Android.
