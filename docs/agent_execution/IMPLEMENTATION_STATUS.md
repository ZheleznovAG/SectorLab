# SectorLab v1.0 — фактический статус реализации

Дата аудита: 2026-10-11. Рабочий каталог: `[LOCAL_USER_HOME]\Projects\SectorLab`.

## Где остановились

Исходный HEAD: `1c8754d8cca20bb342c6770c0985b4f14be27fa3` (`docs: record R5 progress`),
ветка `main`, 35 коммитов; upstream `origin/main`, remote `https://github.com/ZheleznovAG/SectorLab.git`.
Проверенный кодовый checkpoint после фиксации: `ec1a94494dcb6a08f857d26de11618ebbb0391ac`.
Текущий docs-checkpoint содержит этот статус; его точный HEAD — `git rev-parse HEAD`.
Последняя завершённая задача — **W6.2**, следующий этап — **W6.3**.
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
  включая ZIP, в `[LOCAL_USER_HOME]\AppData\Local\Temp\SectorLab-baseline-20261011-211433`.
  Все копии сверены по SHA-256; там же `backup-manifest.csv`, `working-tree.patch`,
  исходные `HEAD.txt` и `git-status.txt`. Это локальная резервная точка,
  временный каталог не является долговременным хранилищем.
- Git LFS `3.7.1` работает. Три PNG в `docs/images/` чисты; новых LFS-объектов
  нет. Их содержимое не заменялось. Повторяющихся GUID в `Assets/**/*.meta` нет.
- ZIP исключены из Git точными шаблонами локальных экспортов в `.gitignore`;
  сами архивы сохранены. Unity-сцены, префабы и существующие GUID не изменялись.
- Рабочая ветка остаётся `main`; локальные коммиты разрешены текущим AGENTS.md.
  Push и публикация не выполняются. На момент завершения W0 исходные правки
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
| Устройство | ADB обнаружил `Redmi_Note_8_Pro`; установка/запуск этого APK НЕ выполнялись | `adb devices -l` |
| Ручной Play Mode/баланс | NOT_RUN: автоматические тесты не заменяют игру человеком | Нужен отдельный плейтест |
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
| W0 / G0 | PASS: аудит/backup/регрессия/build и сохранение исходных правок выполнены | Следующий пакет W1; ручной плейтест и проверка APK на устройстве ещё впереди |
| W1 / G1 | PASS | 45 очков/180 с/Draw, часы, pause/resume, остановка боя, result/restart; 29 EditMode + 43 PlayMode и Main Game View |
| W2 / G2 | PASS | A/B/C, capture/neutralize/contest, команды, score и repair; 46 EditMode + 61 PlayMode и Main |
| W3 / G3 | PASS | AI и четыре настоящие партии Main; 53 EditMode + 67 PlayMode, проигрыш и выигрыш зафиксированы |
| W4 / G4 | PASS | VContainer 1.19.0, настоящий scope Clock/State/Battle/AI; 72/72 PlayMode, missing injection и reload |
| W5 / G5 | IN_PROGRESS | Addressables установлен; далее UniTask, local content, ownership, respawn/отмена/ошибки |
| W6 / G6 | TODO | Tap/drag/UI blocking, pan/pinch, HUD и safe area |
| W7 / G7 | TODO | Звук/mute, обратная связь и плейтесты |
| W8 / G8 | TODO | Финальная регрессия, локальный content+APK, Android/профилирование |
| W9 / G9 | TODO | Финальный README, ADR, лицензии, evidence и учебная карта |

## Реестр требований (baseline и текущие изменения)

`BASELINE_PASS` означает подтверждённую часть текущего прототипа; не означает
приёмку всей v1.0. `PARTIAL` — часть кода/проверок существует; `TODO` — реализации
нет; `NOT_RUN` — соответствующая проверка ещё не выполнялась.

| ID | Статус | Фактическая граница / пакет завершения |
| --- | --- | --- |
| FR-001 | PARTIAL | Сетевой службы нет; запуск APK офлайн не проверен / W8 |
| FR-002 | PARTIAL | Main с ареной сверху существует; новая партия ещё не реализована / W2–W3 |
| FR-003 | PASS | Шесть Main slots используют прежние UnitStats/prefabs; обе команды/все роли прошли spawn и respawn / W5.5 |
| FR-004 | BASELINE_PASS | Click и virtual Touchscreen выбирают/двигают юнита / W6 |
| FR-005 | BASELINE_PASS | Атака/дальность/LOS/погоня проверены текущей регрессией |
| FR-006 | TODO | Камера неподвижна, tap обрабатывается на press, UI blocking нет / W6 |
| FR-007 | PASS | A/B Score и C Repair с отдельными RelayConfig в Main; 18/18 NavMesh approaches доступны |
| FR-008 | PASS | RelayState и RelayPoint: 5/10 с, contest, пустой прогресс, смерть, пауза/finish, цвет/проценты проверены |
| FR-009 | PASS | A/B дают 1 очко/с каждый; пустое владение, contest/neutralize/pause, порог в одном тике проверены |
| FR-010 | PASS | Health.Heal и C: 6 HP/с обеим сторонам, Max, fractional buffer, смерть/уход/contest/pause/finish проверены |
| FR-011 | PASS | MatchRules и BattleController: 45 очков/180 с/Draw; начисление от узлов подключается в W2 |
| FR-012 | PARTIAL | Игровые часы/пауза реализованы и проверены; кнопка и таймер HUD — W6 |
| FR-013 | PASS | Main: все роли обеих команд, 10 игровых с, full HP, без дублей; pause и blocked base / W5.5 |
| FR-014 | PASS | EnemyDecisionRules + EnemyTacticsController в Main; 1 решение/игровую с, реальные move/capture/attack и stable assignments проверены |
| FR-015 | PARTIAL | Есть силы/HP/result, нет очков/времени/узлов/respawn / W6 |
| FR-016 | PARTIAL | Result/Restart, остановка ввода/движения/боя проверены; будущие AI/respawn должны соблюдать тот же исход / W3–W5 |
| FR-017 | PASS | 10 настоящих Main Restart: новые state/clock, 6 units/handles, старые factory instances 0, score/progress/countdown reset / W5.6 |
| FR-018 | TODO | Звук/mute отсутствуют / W7 |
| FR-019 | TODO | Обучения нет / W6 |
| FR-020 | PARTIAL | APK baseline пересобран; APK v1.0 ещё нет / W8 |
| FR-021 | PASS | VContainer 1.19.0: scope в Main, Clock/State/Battle/AI injection; missing registrations обнаруживаются; factory добавляется в W5 |
| FR-022 | PASS | UniTask используется для реальных prefab loads, pause wait и lifetime cancellation; Main late-result/restart проверен / W5.6 |
| FR-023 | PASS | Шесть локальных prefabs, реальные loads/releases; bundles/catalog внутри Android APK, бой на телефоне / G5 |
| FR-024 | PARTIAL | Реальные логи baseline сохранены; финальные gates впереди / W8–W9 |
| FR-025 | PARTIAL | 35 старых коммитов сохранены; fixes W0 раздельно зафиксированы, история v1.0 будет продолжена |
| FR-026 | TODO | Метрики баланса не реализованы; SHOULD / W7 |
| FR-027 | PASS | Missing key после двух успешных loads: cleanup, остановка loading, Retry запускает Main; Exit callback без дублей / W5.6 |
| FR-028 | NOT_RUN | Три формата будущего HUD не проверены / W6 |
| NFR-001 | BASELINE_PASS | Unity Editor соответствует 6000.6.4f1 |
| NFR-002 | NOT_RUN | Чистый checkout не запускался / W8 |
| NFR-003 | PARTIAL | Компиляция и текущие тесты проходят; полная v1.0 ещё отсутствует |
| NFR-004 | BASELINE_PASS | 9+38 тестов текущего прототипа прошли; повторять при новых механиках |
| NFR-005 | PARTIAL | MatchClock/MatchState и фиксированные тики проверены при 15/30/60/144 FPS; подключить узлы/AI/respawn |
| NFR-006 | TODO | Async lifecycle ещё предстоит / W5 |
| NFR-007 | PARTIAL | Дубликатов meta GUID не найдено; новые ссылки проверять по мере работы |
| NFR-008 | NOT_RUN | Телефон подключён; APK на нём не запускался / W8 |
| NFR-009 | NOT_RUN | FPS/память на Android не измерены; SHOULD / W8 |
| NFR-010 | PARTIAL | ZIP/кеши/build исключены; финальный аудит истории впереди / W9 |
| NFR-011 | PARTIAL | THIRD_PARTY_NOTICES.md и MIT VContainer добавлены; последующие пакеты / W5, W9 |
| NFR-012 | PARTIAL | Сохраняется существующая структура; реальные интеграции ещё впереди |
| NFR-013 | PARTIAL | Русский README baseline есть; его v1.0 разделы ещё впереди / W9 |
| NFR-014 | BASELINE_PASS | История и файлы сохранены; destructive commands/push не применялись |

## Решения и следующий шаг

- Новый контракт `README_START.md` и `docs/agent_execution/` имеет приоритет
  над историческим ручным режимом R0–R5. Не переносить старые PASS на новые правила.
- В `02_GAME_DESIGN.md` фраза о необходимом присутствии своего бойца для очков
  соседствует с явным правилом начисления после ухода команды. Для W2 действует
  последнее конкретное правило: полный узел без врага продолжает давать очки
  пустым; contest/нейтрализация останавливают эффект. Закрепить это тестом.
- W0–W2 проверены. Следующая задача — **W3.1**: детерминированный AI за узлы,
  затем реальные полные партии; далее composition root и асинхронный respawn.
- Применять `09_ACCEPTANCE.md`: G3–G9 не пройдены. Наличие плана не означает
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
`Tools/verify-unity.ps1`, `Tools/check-gates.ps1` и [AUTOMATION.md](AUTOMATION.md).
Проверка repository прошла; gate validator показывает G0 PASS и G1–G9 TODO.
Проверки не заменяют ручной плейтест и не публикуют изменения.

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

Компиляция PASS, EditMode **29/29**, PlayMode **43/43**, без failed/skipped/inconclusive.
Логи: [EditMode](evidence/tests-W1.3-editmode.json), [PlayMode](evidence/tests-W1.3-playmode.json).
Main: pause сохраняла 1,5 с между наблюдениями; result Victory 45:0 остановил
время; Restart вернул 0:0/InProgress и 6 юнитов ([наблюдения](evidence/W1-main-observations.txt)).
Первая проверка экрана обнаружила обрезанный счёт; BestFit исправлен,
PlayMode повторно прошёл 43/43, [Game View](evidence/W1-main-victory.png) осмотрен.
Исход в Main вызван техническим тестовым вводом скорости очков: A/B пока отсутствуют.
Это проверка интеграции через CLI, не человеческий плейтест. Решение: [ADR-002](../adr/002-match-time-and-outcomes.md).
G1 закрыт; G2–G9 открыты. Android baseline не выдаётся за APK с новым кодом.

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
[PlayMode](evidence/tests-W2.5-playmode.json)). Проверены Player/Enemy и 15/30/60/144 FPS.
Main: HP после damage/capture **190**, после 3 с ремонта **208**, на паузе и
при споре остаётся **208** ([лог](evidence/W2-heal-check.txt));
[жёлтый спорный C](evidence/W2-heal-contested.png) осмотрен. Сценарий использовал
техническое размещение юнитов и обычную симуляцию, не человеческий плейтест.
G2 закрыт. AI, respawn, мобильный HUD и итоговый APK ещё впереди.

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
и вызванное им модальное Reload; сериализация/старые GUID сохранены.
Следующий шаг — серия партий с разными игровыми планами; G3 пока открыт.

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
[manifest](evidence/W5-local-content-manifest.json). Это content, ещё не APK.
Первый RPC Configure timeout фактически завершил настройку. Content задержан
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
три оставшихся юнита на NavMesh, три owned handles; погибшие slots waiting.
Это наблюдение реального боя, не доказательство человеческого playtest.
HUD ещё baseline alive counter, score/time/respawn UI — W6. Android v1.0 content
в APK ещё не подтверждён, G5 остаётся IN_PROGRESS до отдельной проверки Player.

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
GUID `052faaac586de48259a63d0c4782560b` в preloadedAssets при Player build.
После Editor/test reload пакет вернул список к исходному пустому состоянию.
Исходник установленного Input System (ProjectWideActionsBuildProvider /
BuildProviderHelpers.PostProcessSinglePreloadedAsset) подтверждает временную
preload/cleanup модель. Временная запись попала в d6532d7; теперь возвращена
к штатному исходному значению. Иных изменений PlayerSettings нет.
APK успешно установлен через `adb install -r`, активен на Redmi Note 8 Pro:
[наблюдение](evidence/W5-G5-device.json), [сцена](evidence/W5-G5-device-main.png),
[второй кадр](evidence/W5-G5-device-observation2.png), [лог](evidence/W5-G5-device-log.txt).
G5 PASS подтверждает цепочку DI → UniTask → локальные Addressables в Editor/Player.

Стартовый лог Player **не чистый**: Java ClassNotFoundException для
com.google.android.play.core.assetpacks.AssetPackManager, затем бой продолжился;
URP сообщает об unsupported/stripped неиспользуемых post-processing shaders.
Причины и исправление проверяются в Android regression W8; NFR-003 ещё PARTIAL.
Это pilot APK с версией baseline 0.1.0, не финальный v1.0. Offline и performance
не приняты. Пользователь подключил телефон; прежний BLOCKED_DEVICE снят для
дальнейших доступных проверок. Следующий milestone — input/camera/HUD W6.

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
