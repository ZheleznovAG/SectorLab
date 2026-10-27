# Разработка

Всё ниже работает на Windows, macOS и Linux. Скриптов для конкретной оболочки (PowerShell, bash) в проекте нет:
инструменты — `Tools/sl.py` на Python 3.9+ без сторонних пакетов.

## Окружение

| Что | Зачем | Обязательно |
|---|---|---|
| Unity **6000.6.4f1** (через Unity Hub) + Android Build Support (SDK, NDK, OpenJDK) | Editor, тесты, APK | да |
| Git + **Git LFS** | PNG, шрифты, DLL лежат в LFS; без `git lfs pull` проект битый | да |
| Python 3.9+ | `Tools/sl.py`, pre-commit хук | да |
| Unity CLI `unity` ([unity.com](https://unity.com)) | `unity open .`, `unity command …` — тесты и сборка через открытый Editor | для тестов и сборки |
| Gitleaks **8.30.1** | Проверка приватности (pre-commit, `sl.py check`) | для коммитов |
| `adb` | Установка на телефон и замеры; берётся из Android SDK Unity автоматически | для телефона |

Первый запуск:

```sh
git clone https://github.com/ZheleznovAG/SectorLab.git && cd SectorLab
git lfs pull
git config core.hooksPath .githooks                                  # pre-commit: проверка приватности
git config user.email "<ник>@users.noreply.github.com"               # хук требует noreply
python Tools/sl.py doctor
unity open .                                                          # первый импорт пакетов — несколько минут
```

Первый импорт требует сети (Unity registry и GitHub для UPM-пакетов по git-тегам). Сама игра офлайн:
контент локальный, кроме сетевой игры по Wi-Fi. Gitleaks: официальный релиз 8.30.1 положить в `PATH` или
в `%LOCALAPPDATA%\SectorLab\Tools\gitleaks\8.30.1\gitleaks.exe` (Windows), а на macOS и Linux — в
`~/.local/share/SectorLab/Tools/gitleaks/8.30.1/gitleaks`.

## `Tools/sl.py`

| Команда | Что делает |
|---|---|
| `python Tools/sl.py doctor` | Editor, инструменты, adb и доступность телефона |
| `python Tools/sl.py test --task T` | EditMode и PlayMode в открытом Editor; `--mode EditMode\|PlayMode\|both`, `--filter <полное имя теста или фикстуры>` |
| `python Tools/sl.py check` | Пробелы (`git diff --check`), JSON пакетов, дубли GUID, `.meta` у каждого файла, ссылки в документации, приватность |
| `python Tools/sl.py privacy [--staged]` | Gitleaks по публикуемым файлам: секреты, личные пути и почта; запрещённые типы файлов; автор — noreply |
| `python Tools/sl.py build-android --task T` | APK с локальными Addressables и отчётом о сборке |
| `python Tools/sl.py build-windows --task T [--qa]` | Windows-плеер с локальными Addressables; `--qa` включает Development и команды локального Pipeline |
| `python Tools/sl.py verify-network --task T --build-task B` | Два Windows QA-плеера из сборки B: UDP, приказы, смерть/респаун, узлы, итог, новый матч, обрыв |
| `python Tools/sl.py record-demo --task T --build-task B` | Записать матч и игровой звук из Windows QA-плеера; нужен FFmpeg |
| `python Tools/sl.py install-android [--apk P]` | Установить (по умолчанию — свежий APK), сверить SHA-256 на устройстве, запустить |
| `python Tools/sl.py measure-android --task T` | FPS по кадрам SurfaceFlinger, CPU, память запущенной игры; `--auto-restart` нажимает «Ещё бой» |

`--task` — имя доказательства: латиница, цифры, `.`, `_`, `-`. Отчёты: `docs/evidence/tests-T-<mode>.json`,
`android-build-T.json`, `T.json`/`T.csv`. В отчётах есть `sourceHead`, SHA-256 всех входов (`Assets`,
`Packages`, `ProjectSettings`) и полный ответ Unity. Личные пути заменены на `[LOCAL_USER_HOME]`.
Отчёты важных прогонов коммитятся.
Повторное использование имени с существующим отчётом или CSV останавливает проверку до
запуска Unity. Для `--mode both` заранее проверяются оба режима. Старые доказательства не заменяются;
при исправлении неудачного прогона выбирай новое имя.

## Тесты

- Полный прогон: `python Tools/sl.py test --task <имя>` — около 15–25 минут; `MainTacticsTests` (5 матчей
  тактиками игрока) — самая долгая часть.
- Одна фикстура: `--mode PlayMode --filter SectorLab.Tests.NetworkMatchTests`.
- В Editor: **Window → General → Test Runner**.
- Тесты, пишущие CSV (`MainMatchTests`, `MainRestartTests`, `MainTacticsTests`, `MainAllocationTests`), берут
  имя файла из переменной `SECTORLAB_VERIFICATION_TASK`. `sl.py` ставит её в Editor перед запуском.
- CI: `.github/workflows/checks.yml` запускает тесты Python 3.9/3.14 на Windows/Linux и гигиену
  с Gitleaks на push/PR. [Первый прогон прошёл](https://github.com/ZheleznovAG/SectorLab/actions/runs/40295350670).
  `.github/workflows/tests.yml` (Unity/GameCI) настроен на push в main, PR и ручной запуск;
  запускает только сборки `SectorLab.Tests.EditMode`/`PlayMode`. Нужны секреты `UNITY_LICENSE`,
  `UNITY_EMAIL`, `UNITY_PASSWORD`; preflight явно падает при их отсутствии. Fork PR не получает
  секреты, `pull_request_target` не используется. Успешный локальный прогон не заменяет облачный;
  текущий статус — в STATUS.
- Python-регрессии tooling: `python -B -m unittest discover -s Tools/tests -v`.

## Сборка и телефон

1. `python Tools/sl.py build-android --task <имя>`. Инструмент сначала переключает платформу на
   Android, ждёт окончания импорта, явно собирает Addressables и сверяет размеры/SHA-256
   каталога, settings и всех бандлов внутри APK с manifest сборки контента. Каталог без бандлов
   или контент другой платформы означает ошибку проверки, даже если Unity вернул Succeeded.
   Сборка может изменить `preloadedAssets` в `ProjectSettings/ProjectSettings.asset` (Input System).
   Просмотри `git diff ProjectSettings` и верни только служебные изменения; сохраняй намеренные
   правки версии и настроек. Не восстанавливай весь файл поверх незакоммиченных изменений.
2. Телефон: режим разработчика, отладка по USB, подтвердить ключ ПК. `python Tools/sl.py doctor` покажет устройство.
3. `python Tools/sl.py install-android` — ставит самый свежий APK и сверяет хеш установленного `base.apk`.
4. Сеть: телефоны и ПК в одной Wi-Fi; брандмауэр ПК должен пропускать UDP 7777 для Unity/игры.

Пакет `com.zheleznovag.sectorlab`, подпись — debug-ключ Unity. Эмулятор Android ARM64-сборку не запускает (SIGILL).

**Windows-плеер:** `python Tools/sl.py build-windows --task <имя>`. Инструмент переключает платформу,
собирает локальный контент, проверяет его байты в Player и сохраняет хеши файлов в `windows-build-<имя>.json`.
Результат — `Builds/Windows/<имя>/SectorLab.exe`; запуск клиента: `-sectorlab-join 127.0.0.1`.
Для автоматической приёмки добавить `--qa`: получится Development-плеер с локальным Pipeline,
доступный через `unity command --runtime SectorLab`. Настройки Pipeline восстанавливаются после
сборки; обычная Windows-сборка и APK не требуют диагностического сервера.
Оба wrapper включают `ProjectConfigData.GenerateBuildLayout` перед первым content build:
диагностический layout остаётся в Library, модального вопроса Addressables не возникает.
Это локальная настройка Editor, которая не включает runtime Pipeline. Подробности —
[ADR-021](adr/021-explicit-platform-content-and-artifact-validation.md).

**Приёмка сети:** `python Tools/sl.py verify-network --task <имя> --build-task <имя-QA-сборки>`.
Требует Windows. Проверяет хеши всей сборки, запускает две копии в фоне, соединяет их по UDP
на `127.0.0.1:17777` и пишет `network-<имя>.json`. Сценарий использует настоящие команды и
жизненный цикл; урон, размещение и продвижение игровых тиков задаются автоматикой на хосте.
Это автоматическая проверка двух процессов на ПК. Логи и копии остаются в `Temp/network-acceptance/`.
Сценарий также наблюдает реальную клиентскую линию выстрела и сверяет её конец с положением
цели на хосте; короткая линия отслеживается при публикации модели, а не редким опросом.
Каждый прогон получает новое имя, процессы завершаются и при неудаче. Настройка обучения сохраняется.
Runtime CLI принимает каталог плеера в `--runtime-path`; для asmdef SectorLab сценарий явно
добавляет ссылки компилятору Pipeline. Долгая сборка Addressables выполняется как detached job.

**Видео для README:** `python Tools/sl.py record-demo --task <имя> --build-task <имя-QA-сборки>`.
Требует Windows и FFmpeg с libx264 в PATH. Скрипт запускает отдельную копию QA-плеера,
загружает `Tools/scenarios/RecordGameplay.cs` через Pipeline и подаёт обычные приказы
`UnitCommander` против штатного ИИ. Камера и HUD рендерятся напрямую в текстуру 1280×720,
звук берётся из Unity `AudioRenderer`; рабочий стол и системный звук не захватываются.
Кадры сохраняются с шагом 1/30 с игрового времени; это демонстрация, а не замер FPS.
Запись заканчивается через 4 с после итога или через 190 с (`--max-seconds` можно уменьшить).
MP4 и промежуточные данные — в `Temp/media/<имя>/`, отчёт — `docs/evidence/media-<имя>.json`.
Для публикации копировать итоговый MP4 в `docs/media/` и сделать GIF из выбранного отрывка
через FFmpeg `palettegen`/`paletteuse`; бинарные материалы хранятся в Git LFS.

## Unity Editor из терминала

`unity command <имя> [--параметр значение]` выполняет команду в открытом Editor (пакет `com.unity.pipeline`).
Список: `unity command`; справка: `unity command --query <имя> --detail full`. Проверенные приёмы:

- Состояние: `editor_status`, `console_status` (флаг ошибки компиляции), `console --level error`.
- `eval --code "..."`: `using` игнорируются (писать полные имена типов). Первый вызов в сессии может
  упасть по таймауту, пока прогревается компилятор, — повторить.
- **Сцену `Main.unity` не править на диске, пока она открыта**: Editor покажет модальный диалог
  «modified externally» и заблокирует все команды. Менять через Editor, `unity command` и `save_scene`.
- Git Bash превращает аргументы, начинающиеся с `/`, в пути Windows (`/Battle` → `C:/Program Files/Git/Battle`):
  для путей иерархии задавать `MSYS_NO_PATHCONV=1`.
- `capture_game_view --save_path X` пишет в `Assets/X` — убрать файл и `.meta` после.
- Изменения `PlayerSettings` через `eval` сохраняются на диск только после `EditorUtility.SetDirty`
  и `AssetDatabase.SaveAssets()`.
- NavMesh: `NavMeshSurface` на `Arena`, данные — `Scenes/Main/NavMesh-Arena.asset`. После правки арены
  перепечь (`surface.BuildNavMesh()` и пересоздать ассет) и `save_scene`. PlayMode-тесты на `TestArena`
  пекут свою сетку сами.
- Отклонённые вызовы CLI попадают в консоль Unity как ошибки: прежде чем писать «0 ошибок», прочитать текст.

## Git

- `.gitattributes`: бинарные файлы — LFS; Unity YAML — `eol=lf`, `merge=unityyamlmerge`. LFS-указатель
  в индексе (~130 байт) — норма.
- `.meta` коммитится вместе с ассетом; `move_asset`/`rename_asset` сохраняют GUID.
- `*.csproj`, `*.slnx`, `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` не коммитятся.
- Опубликованные коммиты не переписывать (`--amend`, rebase); исправлять новым коммитом.
- Правила коммитов и полномочия агентов — [AGENTS.md](../AGENTS.md).

## Подводные камни (на них уже наступали)

- **Упавший PlayMode-тест не выполняет `finally` в корутине.** Изменённые общие ассеты (например, роль
  в `Data/Units/Tank.asset`) остаются на диске и ломают десятки следующих тестов. Откат — только в `[UnityTearDown]`.
- `R3.Unit` конфликтует с `SectorLab.Units.Unit`: в файлах с `using R3;` писать `using Unit = SectorLab.Units.Unit;`.
- R3.Unity (UPM) не компилируется без ядра R3 из NuGet.
