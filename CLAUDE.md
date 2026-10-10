# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Главное: режим работы

Обязательные правила — в [`AGENTS.md`](AGENTS.md); они важнее любых умолчаний. Коротко:

- Это **учебный** проект: пользователь сам пишет код и меняет сцену, агент — наставник и ревьюер. Ничего не редактировать и не запускать изменяющих команд без явного «сделай сам/примени».
- **Одна микроитерация за ответ** в формате из `AGENTS.md` (Шаг → Зачем → Сделай вручную → Проверь → Понимание → Коммит → Стоп). Если просят обзор/план/аудит — только отвечать, без правок.
- **Не коммитить**, не делать `reset --hard`, `rebase`, force push. Предлагать сообщение коммита (Conventional Commits, см. `docs/05_GIT_QUALITY.md`) и точный список файлов для `git add` — никогда не `git add .`.
- Объяснения — на русском, английские термины расшифровывать при первом упоминании.
- Не вводить VContainer, UniTask, Addressables, DI, менеджеры, ECS без реальной задачи.

Порядок чтения документов: `README_HANDOFF.md` → `docs/00_CONTEXT_STATUS.md` → `docs/02_WORKFLOW.md` → `docs/03_ROADMAP.md` (этапы R0.0–R5 с рекомендуемыми коммитами). Документы — **план, а не доказательство готовности**: статус этапа определять по фактическому содержимому репозитория. Шаблоны микрозадачи, ADR и ревью — в `docs/templates/`.

## Технический стек (проверено по файлам)

- Unity **6000.6.4f1** (`ProjectSettings/ProjectVersion.txt`), установлен в `C:\Program Files\Unity\Hub\Editor\6000.6.4f1`.
- URP 17 с двумя наборами ассетов в `Assets/Settings/`: `PC_RPAsset`/`PC_Renderer` и `Mobile_RPAsset`/`Mobile_Renderer`. Не удалять как «мусор шаблона».
- Пакет **Unity Test Framework не подключён** — тестов и тестовых сборок пока нет.
- Собственного C#-кода пока нет; в `Assets/` — содержимое шаблона URP (`SampleScene`, `TutorialInfo`, `Readme.asset`). `*.csproj`/`SectorLab.slnx` генерируются Unity и не коммитятся.

## Команды

Сцену и настройки меняет пользователь в Unity Editor. Из терминала — **Unity CLI** `unity` (1.0.0-beta, в PATH: `%LOCALAPPDATA%\Unity\bin\unity.exe`; справка — `unity <команда> --help`). Команды ниже взяты из `--help` и с этим проектом ещё не прогонялись.

```powershell
unity doctor                                   # диагностика окружения CLI/Hub/редакторов
unity open                                     # открыть проект нужной версией редактора
unity run                                      # batch mode (редактор с проектом должен быть закрыт) — проверка компиляции
unity test --mode EditMode                     # тесты (после подключения Unity Test Framework)
unity test --mode EditMode --filter <Имя>      # один тест / подмножество
unity build --profile <Имя> -o Builds/<путь>   # сборка по Build Profile из Assets/Settings/Build Profiles
unity list                                     # команды открытого редактора (через com.unity.pipeline)

# Проверка перед коммитом (docs/05_GIT_QUALITY.md)
git status --short
git diff --check
git diff --stat
```

Граница учебного режима: чтение/диагностика (`doctor`, `list`, логи) и проверки (`test`, компиляция) — допустимы. Изменение сцены или ассетов через `unity command` — **только по явной просьбе** пользователя, иначе это обходит ручную работу.

## Git-специфика

- `.gitattributes` — шаблон Unity: бинарные ассеты (`.png` и др.) идут через **Git LFS**, Unity YAML — с `merge=unityyamlmerge` и `eol=lf`. В индексе такие файлы выглядят как LFS-указатели (~130 байт) — это нормально, а не повреждение.
- `.gitignore` — стандартный Unity-шаблон (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Build/` и т.п.). Не перезаписывать без сравнения.
- Всегда коммитить `.meta` вместе с ассетом; сцены могут менять соседние файлы настроек — смотреть фактический diff.
- Remote `origin` — GitHub `ZheleznovAG/SectorLab`. Опубликованные коммиты не `--amend`-ить (так уже расходилась история с `origin/main`); исправления — новым коммитом.
