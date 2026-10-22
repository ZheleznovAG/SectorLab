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
| `python Tools/sl.py doctor` | Что доступно: git, LFS, хуки, email, Unity CLI и открыт ли Editor, gitleaks, adb и телефоны |
| `python Tools/sl.py test --task T` | EditMode и PlayMode в открытом Editor; `--mode EditMode\|PlayMode\|both`, `--filter <полное имя теста или фикстуры>` |
| `python Tools/sl.py check` | Пробелы (`git diff --check`), JSON пакетов, дубли GUID, `.meta` у каждого файла, ссылки в документации, приватность |
| `python Tools/sl.py privacy [--staged]` | Gitleaks по публикуемым файлам: секреты, личные пути и почта; запрещённые типы файлов; автор — noreply |
| `python Tools/sl.py build-android --task T` | APK в `Builds/Android/SectorLab-T.apk` через открытый Editor, отчёт с SHA-256 и составом Addressables |
| `python Tools/sl.py install-android [--apk P]` | Установить (по умолчанию — свежий APK), сверить SHA-256 на устройстве, запустить |
| `python Tools/sl.py measure-android --task T` | FPS по кадрам SurfaceFlinger, CPU, память запущенной игры; `--auto-restart` нажимает «Ещё бой» |

`--task` — имя доказательства: латиница, цифры, `.`, `_`, `-`. Отчёты: `docs/evidence/tests-T-<mode>.json`,
`android-build-T.json`, `T.json`/`T.csv`. В отчётах есть `sourceHead`, SHA-256 всех входов (`Assets`,
`Packages`, `ProjectSettings`) и полный ответ Unity. Личные пути заменены на `[LOCAL_USER_HOME]`.
Отчёты важных прогонов коммитятся.

## Тесты

- Полный прогон: `python Tools/sl.py test --task <имя>` — около 15–25 минут; `MainTacticsTests` (5 матчей
  тактиками игрока) — самая долгая часть.
- Одна фикстура: `--mode PlayMode --filter SectorLab.Tests.NetworkMatchTests`.
- В Editor: **Window → General → Test Runner**.
- Тесты, пишущие CSV (`MainMatchTests`, `MainRestartTests`, `MainTacticsTests`, `MainAllocationTests`), берут
  имя файла из переменной `SECTORLAB_VERIFICATION_TASK`. `sl.py` ставит её в Editor перед запуском.
- CI: `.github/workflows/tests.yml` (GameCI), запуск вручную. Нужны секреты `UNITY_LICENSE`, `UNITY_EMAIL`,
  `UNITY_PASSWORD`; ни разу не запускался.

## Сборка и телефон

1. `python Tools/sl.py build-android --task <имя>`. Сборка перезаписывает `preloadedAssets` в
   `ProjectSettings/ProjectSettings.asset` (Input System); `sl.py` предупредит. Если в diff только это —
   `git checkout -- ProjectSettings/ProjectSettings.asset`. Служебные изменения в `Mobile_RPAsset`,
   `UniversalRenderPipelineGlobalSettings`, `GraphicsSettings` — нормальны.
2. Телефон: режим разработчика, отладка по USB, подтвердить ключ ПК. `python Tools/sl.py doctor` покажет устройство.
3. `python Tools/sl.py install-android` — ставит самый свежий APK и сверяет хеш установленного `base.apk`.
4. Сеть: телефоны и ПК в одной Wi-Fi; брандмауэр ПК должен пропускать UDP 7777 для Unity/игры.

Пакет `com.zheleznovag.sectorlab`, подпись — debug-ключ Unity. Эмулятор Android ARM64-сборку не запускает (SIGILL).

**Windows-плеер** (например, второй участник сетевой игры на том же ПК): перед сборкой выполнить меню
**SectorLab → Build local unit content** (или `AddressableUnitSetup.BuildContent`), иначе в плеере не будет
бандлов юнитов. Затем запустить `SectorLab.exe -sectorlab-join 127.0.0.1`.

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
- R3.Unity (UPM) не компилируется без ядра R3 из NuGet. Ядро и его зависимости лежат в `Assets/Packages`
  (в git), список — `Assets/packages.config`.
- Windows-плеер падал на SSAO в URP; фича убрана из `PC_Renderer` (на Android не влияет).
- `FindFirstObjectByType` и `FindObjectsSortMode` устарели в Unity 6.6: использовать `FindAnyObjectByType`.
- Ожидание в тестах — по игровому времени (`State.Clock.ElapsedSeconds`), не по реальному: на занятой
  машине кадров меньше.
