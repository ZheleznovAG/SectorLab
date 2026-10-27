# Исправления комплексного ревью — 27.10.2026

Основание — [исходный отчёт](PROJECT_REVIEW_2026-10-27.md) и последующее разрешение владельца
выполнить весь план. Исходный отчёт и прежние evidence сохранены. Работа выполнена в текущем
рабочем дереве поверх `c6cf1228ad63aa11b10ae1ffd4cb46cf6f689ff6`; на этапе проверок коммиты, push
и публикация не выполнялись. Последующая фиксация описана ниже.
Хеши входов в новых отчётах описывают незакоммиченную реализацию; один `sourceHead` её не описывает.

## Результат

Исправлены подтверждённые runtime/tooling-дефекты R-01, R-02, R-04…R-07. Выбрана и реализована
симметричная семантика готового огня A-01. Архитектура остаётся одной небольшой Unity-игрой:
дополнительные сервисы, DI-слои и обновления зависимостей не потребовались.

**88 EditMode + 172 PlayMode PASS**, без пропусков; Python 10/10; два настоящих Windows-процесса
через UDP — **16/16 PASS**. Облачный Unity gate (R-03) подготовлен, но не завершён: нет лицензирующих
секретов и первого облачного прогона. Проверка на физическом Android и мобильные измерения
остаются внешней приёмкой, а не «закрытой по коду» задачей.

## Статус замечаний

| ID | Что изменено | Подтверждение / предел закрытия |
|---|---|---|
| R-07, P1 | Общая подготовка Android/Windows: выбор target → ожидание импорта/компиляции → сброс кеша profile properties → явный content build. Manifest содержит размеры/SHA-256 каталога, settings и бандлов из результата Addressables. Готовый Player проверяется по этим байтам и платформенным путям; каталог без бандлов отклоняется. | Windows Player собран и запущен. Android-артефакт проверяется отдельно ниже; физическая загрузка шести слотов требует телефона. Код: [unity.py](../../Tools/sectorlab/unity.py), [content.py](../../Tools/sectorlab/content.py), [AddressableUnitSetup](../../Assets/_Project/Editor/AddressableUnitSetup.cs). |
| R-01, P2 | `NetworkMenu.Relaunch` после ошибки создания канала завершает переход в свежую Offline-сцену; причина сохраняется в одноразовом сообщении. Роли, authority, ИИ и команды согласованы. | PlayMode: принудительная ошибка роли из Client, новая Offline-сцена, открытое сообщение, закрытие меню, ход времени и настоящий приказ. [3 теста меню](../evidence/tests-review-fixes-networkmenutests-playmode.json). |
| R-02, P2 | `UnitAttack.LastShotEnd` → `UnitView.ShotX/ShotZ` → snapshot → короткая линия `ClientUnitView`. Клиентский бой остаётся отключён. | Roundtrip и атомарный отказ при позднем NaN; baseline/duplicate/generation/death/new-match/expiry в PlayMode. В двух Windows-процессах наблюдается настоящий включённый LineRenderer и совпадающий конец выстрела. |
| R-03, P2 | Unity workflow настроен на push main, PR и ручной запуск; явный license preflight, фильтр только SectorLab test assemblies, отмена устаревшего запуска. Checkout не сохраняет credentials; fork PR не получает секреты. | **Частично:** actionlint PASS, официальный образ доступен. Секретов нет; публикация workflow, первый зелёный прогон, проверка красного gate и required checks ещё не выполнены. |
| R-04, P3 | `NetworkMatchTests` захватывает наличие/значение настройки обучения до создания матча и восстанавливает в `UnityTearDown`, включая ошибочный тест. | Фикстура проходит в адресном и полном наборе. Дополнительно три запуска по 4 теста: отсутствующий ключ, 0, 1; до/после совпали, первоначальное пользовательское состояние восстановлено — [JSON](../evidence/review-fixes-preferences.json). |
| R-05, P3 | `write_evidence` создаёт JSON исключительно через `x`; preflight проверяет существующие JSON/CSV до старта Unity, для `both` сразу оба режима. Build evidence/APK/каталоги также защищены от повторного имени. | Python-регрессии проверяют сохранность исходных байтов и отсутствие вызовов Unity, в том числе при занятом PlayMode CSV и `--mode both`. |
| R-06, P2 | Перед content build устанавливается публичное `ProjectConfigData.GenerateBuildLayout=true`, layout хранится в Library. Wrapper ждёт стабильного endpoint после domain reload, автоматически повторяются только чтения статуса. | Windows и Android content build проходят штатным wrapper. Дополнительно в диагностике воссозданы layout=false/seen=false: build завершился без ответа на диалог, layout стал true, seen остался false; начальные локальные флаги восстановлены. [Результат](../evidence/review-fixes-first-content-prompt.json), [manifest](../evidence/review-fixes-first-content-local-content.json). Внутренний seen-флаг не используется production-кодом. |
| A-01, компромисс P2 | Сначала готовятся все выстрелы тика, потом применяется урон. Смерть в этом же тике не отменяет готовый выстрел. Реестр больше не даёт приоритет первому стрелку. | Две последовательности регистрации: оба бойца погибают и каждый стреляет ровно один раз. Полный набор, включая пять тактических матчей, проходит. |

**Обновление после фиксации:** по решению владельца облачный Unity CI не подключается; GameCI
оставлен только ручным `workflow_dispatch`, push/PR выполняют быстрые проверки
([ADR-024](../adr/024-local-unity-validation.md)). Строка R-03 выше описывает состояние на момент отчёта.

## Изменения и совместимость

- [ADR-021](../adr/021-explicit-platform-content-and-artifact-validation.md): явный платформенный
  контент и проверка готового артефакта. Это ограниченный build tooling, без нового runtime-слоя.
- [ADR-022](../adr/022-simultaneous-ready-shots.md): двухфазный готовый огонь. Используется повторно
  применяемый список на шесть выстрелов, без выделения списка каждый тик. Числовой баланс сохранён.
  Это меняет исход одновременной смертельной дуэли; абсолютная независимость от FPS не обещается.
- [ADR-023](../adr/023-replicated-shot-endpoints.md): конец выстрела в состоянии слота, без отдельного
  событийного канала и истории. При потере нескольких кадров отображается последний новый выстрел,
  а не очередь всех пропущенных. Это соответствует существующему presentation-счётчику и масштабу LAN-игры.

Версия игры — **1.3.0/code 4**. Протокол **v3**, FrameSnapshot **277 байт** вместо 229;
добавлены два float на каждый из шести слотов. Старые v1/v2 кадры и control packets отклоняются;
обоим участникам нужна 1.3.0. Подпись Android остаётся прежней debug-подписью, публикация не выполнялась.

Точечно раскрыты вложенные условия в HUD, выборе ИИ и публикации жизни слотов. Уточнены устаревшие
комментарии о будущем хосте и независимости боя от FPS. Formatter, новые линтеры и массовая
перестройка файлов не вводились. Документация состояния, запуска, архитектуры, правил и changelog обновлена.

## Выполненные проверки

| Команда | Результат | Доказательство |
|---|---|---|
| `sl.py test --task review-fixes-codecs --mode EditMode` | 88/88 PASS | [JSON](../evidence/tests-review-fixes-codecs-editmode.json) |
| `sl.py test --task review-fixes-unitattacktests --mode PlayMode --filter SectorLab.Tests.UnitAttackTests` | 21/21 PASS | [JSON](../evidence/tests-review-fixes-unitattacktests-playmode.json) |
| `sl.py test --task review-fixes-networkmenutests --mode PlayMode --filter SectorLab.Tests.NetworkMenuTests` | 3/3 PASS | [JSON](../evidence/tests-review-fixes-networkmenutests-playmode.json) |
| `sl.py test --task review-fixes-clientpresentationtests --mode PlayMode --filter SectorLab.Tests.ClientPresentationTests` | 6/6 PASS | [JSON](../evidence/tests-review-fixes-clientpresentationtests-playmode.json) |
| `sl.py test --task review-fixes-networkmatchtests --mode PlayMode --filter SectorLab.Tests.NetworkMatchTests` | 4/4 PASS | [JSON](../evidence/tests-review-fixes-networkmatchtests-playmode.json) |
| `sl.py test --task review-fixes-full --mode both` | **88/88 + 172/172 PASS** | [EditMode](../evidence/tests-review-fixes-full-editmode.json), [PlayMode](../evidence/tests-review-fixes-full-playmode.json); входы обоих режимов совпадают |
| `python -B -m unittest discover -s Tools/tests -v` | **10/10 PASS** | Проверяет реальные отказы упаковки и перезаписи evidence; внешние Unity-запуски изолированы mock |
| `sl.py build-windows --task review-fixes-v130-qa --qa` | **Succeeded, 0 errors, 3 warnings**; 5 content files совпали с manifest | [Player](../evidence/windows-build-review-fixes-v130-qa.json), [manifest](../evidence/review-fixes-v130-qa-windows-local-content.json) |
| `sl.py verify-network --task review-fixes-v130 --build-task review-fixes-v130-qa` | **16/16 PASS** | [Отчёт](../evidence/network-review-fixes-v130.json), полные хеши QA-сборки; процессы завершены инструментом |
| `sl.py build-android --task v1.3.0` после Windows QA build | **Succeeded, 0 errors, 1 warning**, 5 content files совпали с manifest | [APK build](../evidence/android-build-v1.3.0.json), [Android manifest](../evidence/v1.3.0-android-local-content.json) |
| `sl.py test --task review-fixes-prefs-{absent,zero,one} --mode PlayMode --filter SectorLab.Tests.NetworkMatchTests` | 3×4/4 PASS; наличие и значение PlayerPrefs сохранены | [Сводка](../evidence/review-fixes-preferences.json), [absent](../evidence/tests-review-fixes-prefs-absent-playmode.json), [zero](../evidence/tests-review-fixes-prefs-zero-playmode.json), [one](../evidence/tests-review-fixes-prefs-one-playmode.json) |
| `sl.py check`; `actionlint` 1.7.12 по всем workflow | PASS: гигиена, GUID/meta, ссылки, Gitleaks; workflow без диагностик | [Финальная сводка](../evidence/review-fixes-final-validation.json) |

Сценарий UDP использует настоящие команды и lifecycle. Для воспроизводимости размещение, урон
и продвижение тиков управляются на хосте. Это локальная двухпроцессная приёмка; она не проверяет
радиоканал Wi-Fi, мобильные паузы ОС или удобство живой игры.

Windows build summary содержит 3 warnings; wrapper сохраняет счётчик, а не полный текст всех
BuildReport steps. Android summary содержит 1 warning: Pipeline явно отключён в обычном Player.
В локальном Editor log также есть служебные предупреждения Addressables о пустом ProfileValueReference
и об удалённых subshaders неиспользуемого Terrain. Они не объявляются отсутствующими; регрессии
загрузки юнитов в Windows по факту запуска не обнаружено.

## Android-кандидат

Локальный файл: `Builds/Android/SectorLab-v1.3.0.apk`, **34 486 756 байт**,
SHA-256 `97A47C098851794ED229E53360B253389854A10C830F52567682CE64303909AA`.
`aapt2 dump badging` подтверждает package `com.zheleznovag.sectorlab`, versionName **1.3.0**,
versionCode **4**, ABI **arm64-v8a**, target SDK **36**. APK содержит оба бандла под `assets/aa/Android/`;
байты каталога, hash, settings и обоих бандлов совпадают с явным content build.
Отдельно через публичный Editor diagnostic API `ContentCatalogData.ExtractBinaryCatalog`
разобран бинарный каталог именно из ZIP: обе его bundle-ссылки ведут в Android и разрешаются
в существующие файлы APK — [проверка артефакта](../evidence/review-fixes-android-artifact.json).
Это проверка упаковки, а не запуск на телефоне. Прежний APK 1.2.0 и его исторический build report
не изменены; уточнение о неполноте старого артефакта добавлено в changelog/status.

После сборок закрыт только Editor, запущенный этой сессией. Возвращены служебная пересериализация
`PC_RPAsset` и изменения `preloadedAssets`, сохранены намеренные **1.3.0/code 4**. Итоговые хеши
**всех Assets/Packages/ProjectSettings побайтно совпали с входами полного EditMode/PlayMode-прогона**.
Поэтому результаты тестов относятся к оставленной реализации, а не к случайной промежуточной версии.
Проверка первого content build воссоздавала именно условие диалога в существующем Library;
полный импорт нового чистого клона после правок отдельно не повторялся.

## Оставшаяся внешняя работа

1. Физический Android: установка нового APK, старт со всеми шестью слотами, несколько партий
   и рестартов, фон/возврат, жесты, notch/панели ОС, звук и реальный LAN. Измерить frame time и память,
   не подменять их скоростью Editor или записью демонстрации. Без устройства эта часть плана не выполнена.
2. Unity CI: владелец настраивает доступный способ лицензирования через GitHub secrets/runner;
   нельзя извлекать пароль из локального аккаунта или публиковать лицензию в исходниках.
   После отправки workflow получить зелёные Edit/Play checks и проверить
   отрицательный сценарий gate, после чего настроить обязательные checks ветки. До этого R-03 частично открыт.
3. Гипотезы H-01… из исходного ревью остаются гипотезами. DNS, дополнительные кэши и перестройка
   инфраструктуры не менялись без измерений. Необходимость обновить стек по факту новой версии не установлена.

Кандидат 1.3.0 не выпущен как релиз. Первое дальнейшее
действие — **установка проверенного APK на доступный физический Android**, без новой игровой функциональности.

## Последующая фиксация по просьбе владельца

27.10.2026 владелец подтвердил коммиты и обычную отправку в `origin/main`. Созданы тематические коммиты:

- `d16f7c4` — исходное ревью и доказательства базы;
- `55780e9` — платформенный контент, проверка упаковки и сохранность evidence;
- `8c1b23e` — восстановление Offline после ошибки сетевой роли;
- `9702037` — подготовка готовых выстрелов до применения урона;
- `6dde330` — авторитетные линии выстрелов и протокол v3;
- `17601e9` — сохранение настройки обучения в сетевой фикстуре;
- `1ffb124` — читаемые условия HUD/ИИ и актуальный комментарий команд;
- `30288dd` — Unity CI на push/PR.

Версия 1.3.0, итоговая документация и машинные доказательства фиксируются следующим отдельным коммитом.
Перед фиксацией исправлены только конечные пробелы в трёх новых ADR; игровые исходники и зависимости
после успешных тестов не менялись. Полный повтор Unity не запускался: хеши входов совпадают с
успешным полным прогоном. Хуки приватности выполняются штатно, история не переписывается.
Старые `sourceHead`, даты, хеши сборок и результаты проверок не заменяются новыми значениями.
APK/ZIP/кеши и локальные лицензии в коммиты не включены.

Коммит `fc0b9f0` с итоговыми материалами отправлен в `origin/main`. Его
[Repository checks](https://github.com/ZheleznovAG/SectorLab/actions/runs/40492565876) прошли:
гигиена и Python 3.9/3.14 на Windows/Linux, пять jobs. Облачный
[Unity run](https://github.com/ZheleznovAG/SectorLab/actions/runs/40492565898) остановился на license preflight:
нет `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`. Тесты в облаке не запускались.
Этот запуск также выявил вторичную ошибку upload-artifact с пустым output path после skipped tests;
следующая небольшая CI-правка пропускает upload для skipped tests и использует явный каталог результатов.
Она не меняет игровые входы и не скрывает отсутствие лицензии.

## Официальные источники проверки workflow

Проверено **27.10.2026**: [inputs GameCI test runner v4](https://github.com/game-ci/unity-test-runner/blob/v4/action.yml),
[нормализация testMode и передача customParameters](https://github.com/game-ci/unity-test-runner/blob/v4/src/test-args.ts),
[Docker Hub API образа Unity 6000.6.4f1](https://hub.docker.com/v2/repositories/unityci/editor/tags/ubuntu-6000.6.4f1-base-3),
[actionlint 1.7.12](https://github.com/rhysd/actionlint/releases/tag/v1.7.12).
Наличие образа и успешный линтер не означают подтверждённого облачного запуска проекта.
