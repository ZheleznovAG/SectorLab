# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Главное: режим работы

Обязательные правила — в [`AGENTS.md`](AGENTS.md); они важнее любых умолчаний. Коротко:

- Это **учебный** проект: пользователь сам пишет код и меняет сцену, агент — наставник и ревьюер. Ничего не редактировать и не запускать изменяющих команд без явного «сделай сам/примени».
- **Одна микроитерация за ответ** в формате из `AGENTS.md` (Шаг → Зачем → Сделай вручную → Проверь → Понимание → Коммит → Стоп). Если просят обзор/план/аудит — только отвечать, без правок.
- **Не коммитить**, не делать `reset --hard`, `rebase`, force push. Предлагать сообщение коммита (Conventional Commits, см. `docs/05_GIT_QUALITY.md`) и точный список файлов для `git add` — никогда не `git add .`.
- Объяснения — на русском, английские термины расшифровывать при первом упоминании.
- Не вводить VContainer, UniTask, Addressables, DI, менеджеры, ECS без реальной задачи. Не считать «захват точек» принятой механикой — геймдизайн открыт (`docs/06_GAME_DESIGN.md`, `docs/07_DECISIONS.md`).
- Не объявлять что-либо проверенным в Unity Editor или на Android, если это реально не запускалось.

Порядок чтения документов: `README_HANDOFF.md` → `docs/00_CONTEXT_STATUS.md` → `docs/02_WORKFLOW.md` → `docs/03_ROADMAP.md` (этапы R0.0–R5 с рекомендуемыми коммитами). Документы — **план, а не доказательство готовности**: статус этапа определять по фактическому содержимому репозитория. Шаблоны микрозадачи, ADR и ревью — в `docs/templates/`.

## Технический стек (проверено по файлам)

- Unity **6000.6.4f1** (`ProjectSettings/ProjectVersion.txt`), установлен в `C:\Program Files\Unity\Hub\Editor\6000.6.4f1`.
- URP 17 с двумя наборами ассетов в `Assets/Settings/`: `PC_RPAsset`/`PC_Renderer` и `Mobile_RPAsset`/`Mobile_Renderer`. Целевая платформа — Android, 3D top-down.
- Input System (`com.unity.inputsystem`), действия по умолчанию — `Assets/InputSystem_Actions.inputactions`.
- `com.unity.pipeline` (экспериментальный) — мост между Unity CLI и открытым редактором (`unity list` / `unity command`). Не удалять как «мусор шаблона».
- Unity Test Framework 1.8.0. Тесты: `Assets/_Project/Tests/EditMode` (чистые правила: `Health`, `BattleRules`) и `Tests/PlayMode` (движение, атака, команды касанием, исход партии). PlayMode-тесты ввода используют `InputTestFixture`, поэтому в `manifest.json` есть `"testables": ["com.unity.inputsystem"]`.
- Код — одна сборка `SectorLab` (`Assets/_Project/Scripts/SectorLab.asmdef`): `Units/` (`Unit` — точка входа юнита: команда, `Health`, выделение, реестр `Unit.All`; `UnitMovement` — прямолинейный `MoveTowards`; `UnitAttack` — сближение, удар по кулдауну, автоцель для стоящих юнитов, линия выстрела; `UnitStats` — ScriptableObject с числами роли, `Unit` применяет его в `Awake`; `HealthBar`), `Player/UnitCommander` (луч от `Pointer` — мышь и касание одним кодом: свой юнит → выбрать, враг → атаковать, слой `Ground` → идти), `Battle/` (`BattleRules` — чистая функция исхода, `BattleController`, `BattleResultView` на legacy `UI.Text`). Слои: 8 `Ground`, 9 `Units`.
- Ассеты — в `Assets/_Project/` (`Scenes/`, `Materials/`, `Prefabs/` — база `Unit` → вариант `Enemy`; роли `Tank`/`Scout`/`Ranger` — варианты `Unit`, `EnemyTank`/… — варианты `Enemy`; `Data/Units/` — ассеты `UnitStats`); единственная сцена — `Assets/_Project/Scenes/Main.unity` (она же единственная в Build Settings). В сцене: камера, свет, `Arena`, `Units` (по Tank/Scout/Ranger с каждой стороны), `UnitCommander`, `DestinationMarker`, `Battle`, `HUD`, `EventSystem`. Фактический статус этапов — `docs/00_CONTEXT_STATUS.md`. Шаблонные `SampleScene`/`TutorialInfo` удалены. `*.csproj`/`SectorLab.slnx` генерируются Unity и не коммитятся.
- Активная платформа — Android через Build Profile `Assets/Settings/Build Profiles/Android.asset` (ничего не переопределяет). Активная платформа хранится в `Library/`, поэтому свежий клон откроется под Windows, пока профиль не активирован.
- `Assets/Settings/SampleSceneProfile.asset` не удалять, несмотря на имя: на него ссылаются `PC_RPAsset` и `Mobile_RPAsset`.

## Команды

Сцену и настройки меняет пользователь в Unity Editor. Из терминала — **Unity CLI** `unity` (1.0.0-beta, в PATH: `%LOCALAPPDATA%\Unity\bin\unity.exe`; справка — `unity <команда> --help`). `doctor`/`open`/`run`/`test`/`build` взяты из `--help` и с этим проектом ещё не прогонялись; `list`/`command` проверены.

```powershell
unity doctor                                   # диагностика окружения CLI/Hub/редакторов
unity open                                     # открыть проект нужной версией редактора
unity run                                      # batch mode (редактор с проектом должен быть закрыт) — проверка компиляции
unity test --mode EditMode                     # тесты (не прогонялось: редактор обычно открыт — см. run_tests ниже)
unity build --profile <Имя> -o Builds/<путь>   # сборка по Build Profile из Assets/Settings/Build Profiles
unity list                                     # команды открытого редактора (через com.unity.pipeline)

# Проверка перед коммитом (docs/05_GIT_QUALITY.md)
git status --short
git diff --check
git diff --stat
```

Граница учебного режима: чтение/диагностика (`doctor`, `list`, логи) и проверки (`test`, компиляция) — допустимы. Изменение сцены или ассетов через `unity command` — **только по явной просьбе** пользователя, иначе это обходит ручную работу.

### `unity command` на практике (проверено с открытым редактором)

- Параметры команды: `unity command --query <имя> --detail full`. Вызов: `unity command --no-banner <имя> --param value`.
- Полезное: `editor_status`, `list_open_scenes`, `get_scene_hierarchy`, `console_status` (счётчики ошибок + флаг сбоя компиляции), `console --level error`, `recompile` + `recompile_status`, `move_asset`/`rename_asset` (сохраняют GUID), `add_scene_to_build`/`remove_scene_from_build`, `list_build_profiles`, `editor_play`/`editor_stop`, `capture_game_view`.
- **Git Bash переписывает аргументы, начинающиеся с `/`** (`/Ground` → `C:/Program Files/Git/Ground`). Для путей иерархии сцены задавать `MSYS_NO_PATHCONV=1`.
- Пути ассетов считаются от authoring root (`Assets`); `..` и пути вне проекта отклоняются. `capture_game_view --save_path X` пишет в `Assets/X` — временный файл потом убрать из `Assets/` вместе с `.meta`.
- Изменения настроек проекта (например, список сцен сборки) остаются в памяти редактора, пока не выполнить `menu --path "File/Save Project"`. Сцену сохранять `save_scene`.
- Каждый отклонённый вызов CLI логируется в консоль Unity как ошибка — перед выводом «0 ошибок» проверять текст (`console --level error`), при необходимости `clear_console`.
- `eval` (C# в редакторе): первый вызов в сессии может упасть по таймауту 5 с, пока прогревается компилятор, — повторить. Нужен для PlayerSettings, которых нет в `set_player_settings` (ориентация, id пакета).
- Изменения `PlayerSettings` через `eval` не попадают на диск даже после «Save Project»: пометить `EditorUtility.SetDirty` на `Resources.FindObjectsOfTypeAll<PlayerSettings>()`, затем `AssetDatabase.SaveAssets()`.
- Скриншот, удалённый из `Assets/` до импорта, даёт в консоли предупреждение `Build asset version error` — безвредно, `clear_console`.
- Сборка (`build` + опрос `build_status`) дописывает в `Mobile_RPAsset`, `UniversalRenderPipelineGlobalSettings`, `GraphicsSettings` служебные данные — это нормально, их коммитят.
- Тесты при открытом редакторе: `run_tests --mode editmode|playmode --async_tests true --filter_type assembly --filter SectorLab.Tests.EditMode|SectorLab.Tests.PlayMode`, затем опрашивать `test_status` до `"status":"completed"`. Синхронный PlayMode-запуск обрывается доменной перезагрузкой.
- `eval`/`eval_file` игнорируют `using` (писать полные имена типов), и в конце кода не должно быть своего `return` ("Unreachable code"). Play-режим проверять так: `editor_play` → `eval` с `UnitCommander.HandleTap(Camera.main.WorldToScreenPoint(...))` → `capture_game_view` → `editor_stop`.
- После Android-сборки Input System оставляет в `ProjectSettings.asset` запись `preloadedAssets` (GUID `052faaac…`, `InputSystem_Actions`) — это артефакт сборки, не коммитить: `git restore ProjectSettings/ProjectSettings.asset`.
- `delete_asset` разрешён правилом в `.claude/settings.local.json` только при прямом вызове `unity command [--no-banner] delete_asset …` (не через обёртки и `&&`).

## Git-специфика

- `.gitattributes` — шаблон Unity: бинарные ассеты (`.png` и др.) идут через **Git LFS**, Unity YAML — с `merge=unityyamlmerge` и `eol=lf`. В индексе такие файлы выглядят как LFS-указатели (~130 байт) — это нормально, а не повреждение.
- `.gitignore` — стандартный Unity-шаблон (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Build/` и т.п.). Не перезаписывать без сравнения.
- Всегда коммитить `.meta` вместе с ассетом; сцены могут менять соседние файлы настроек — смотреть фактический diff.
- Remote `origin` — GitHub `ZheleznovAG/SectorLab`. Опубликованные коммиты не `--amend`-ить (так уже расходилась история с `origin/main`); исправления — новым коммитом.
