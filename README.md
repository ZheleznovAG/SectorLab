# SectorLab

Учебный прототип мобильной тактической игры на Unity: 3D, вид сверху, целевая платформа Android. Проект развивается маленькими проверяемыми шагами по [`docs/03_ROADMAP.md`](docs/03_ROADMAP.md). Основная механика пока не выбрана (см. [`docs/06_GAME_DESIGN.md`](docs/06_GAME_DESIGN.md)).

**Текущее состояние:** R0 — техническая основа. Есть сцена с тестовой ареной и настроенной камерой, игровой логики и C#-кода ещё нет.

![Арена R0](docs/images/r0-arena.png)

## Требования

- Unity **6000.6.4f1** с модулем **Android Build Support** (OpenJDK, Android SDK & NDK).
- Git и **Git LFS** (бинарные ассеты хранятся через LFS).

## Запуск

1. Клонировать: `git clone https://github.com/ZheleznovAG/SectorLab.git`, затем `git lfs pull`.
2. Открыть папку проекта через Unity Hub.
3. Переключить платформу: **File → Build Profiles → Android → Switch Platform** (активная платформа хранится в `Library/`, поэтому свежий клон открывается под Windows).
4. Открыть сцену `Assets/_Project/Scenes/Main.unity` и нажать Play.

## Сборка Android

**File → Build Profiles → Android → Build** (или из терминала через Unity CLI: `unity command build --target Android --outputPath Builds/Android/SectorLab.apk --confirm true` при открытом редакторе). Папка `Builds/` в Git не попадает.

Идентификатор пакета: `com.zheleznovag.sectorlab`. Ориентация: только горизонтальная (Landscape Left/Right).

## Что внутри

| Что | Где |
| --- | --- |
| Единственная сцена | `Assets/_Project/Scenes/Main.unity` |
| Материалы арены | `Assets/_Project/Materials/` |
| Настройки URP (Mobile/PC) | `Assets/Settings/` |
| Решения и их причины | [`docs/07_DECISIONS.md`](docs/07_DECISIONS.md) |
| Журнал шагов | [`docs/08_LEARNING_LOG.md`](docs/08_LEARNING_LOG.md) |

Сцена: камера Perspective (наклон 55°, FOV 35°), направленный свет с жёсткими тенями, арена 10×10 м (`Arena` → `Ground`, `Walls`, `Obstacles`). 1 единица Unity = 1 м.

## Ограничения и известные проблемы

- **Не проверено на реальном устройстве.** APK собирается без ошибок, но запуск на телефоне ещё не выполнялся.
- Редактор показывает уровень качества **Mobile** (как на телефоне). На ПК-сборке включается PC: мягкие тени, 4 каскада.
- Тени на Mobile жёсткие, на краях заметна «лесенка» — сознательная экономия; дальность теней 25 м подобрана под арену 10×10 и камеру `(0, 14, -11)`. При увеличении арены пересмотреть камеру и дальность теней.
- Тестов пока нет: пакет Unity Test Framework не подключён, проверять нечего.
- `com.unity.pipeline` — экспериментальный мост для Unity CLI в редакторе; в сборку не попадает (предупреждение `No RuntimePipelineConfig asset found` при сборке ожидаемо).

## Как ведётся разработка

Правила для людей и ИИ-агентов — [`AGENTS.md`](AGENTS.md), процесс — [`docs/02_WORKFLOW.md`](docs/02_WORKFLOW.md), коммиты — Conventional Commits ([`docs/05_GIT_QUALITY.md`](docs/05_GIT_QUALITY.md)).
