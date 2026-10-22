# Документация SectorLab

Начни с [STATUS.md](STATUS.md) — где проект сейчас и что дальше. Правила работы — в [../AGENTS.md](../AGENTS.md).

| Документ | Вопрос, на который отвечает |
|---|---|
| [STATUS.md](STATUS.md) | Что работает, что проверено, на чём остановились, что делать дальше? |
| [MISSION.md](MISSION.md) | Зачем проект, какие цели и принципы, чего не делаем? |
| [GAME_DESIGN.md](GAME_DESIGN.md) | Как играть, какие правила и цифры? |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Как устроен код, где что лежит, с чего читать? |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Как поставить окружение, запустить тесты, собрать APK, поставить на телефон? |
| [../CHANGELOG.md](../CHANGELOG.md) | Что вошло в каждую версию APK? |
| [adr/](adr/) | Почему принято то или иное решение? |
| [audit/](audit/) | Что нашли аудиты и что из этого исправлено? |
| [evidence/](evidence/) | Чем подтверждены прогоны тестов, сборки, замеры, скриншоты? |
| [history/](history/README.md) | Как проект делался: контракт v1.0, журнал, ранние документы |
| [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) | Какие сторонние пакеты и под какими лицензиями? |
| [templates/ADR.md](templates/ADR.md) | Шаблон нового ADR |

## ADR

| № | Решение |
|---|---|
| [001](adr/001-v1-contract-and-baseline.md) | Контракт v1.0 и сохранение проверенного baseline |
| [002](adr/002-match-time-and-outcomes.md) | Игровое время, пауза и результат |
| [003](adr/003-relay-orders-and-group-arrival.md) | Узлы и прибытие нескольких бойцов |
| [004](adr/004-match-composition-root.md) | Composition root сцены и lifetime служб (VContainer) |
| [005](adr/005-async-prefab-ownership.md) | Локальные префабы, UniTask и отмена |
| [006](adr/006-fair-arena-and-measured-tactics.md) | Зеркальная арена и измеримые тактики |
| [007](adr/007-relay-entry-and-unused-renderer-resources.md) | Вход отряда в узел и ресурсы renderer |
| [008](adr/008-game-time-in-navigation-tests.md) | Игровое время в навигационных тестах |
| [009](adr/009-explicit-unit-role.md) | Роль юнита хранится в данных |
| [010](adr/010-mobile-feedback.md) | Управление занятым узлом и обратная связь |
| [011](adr/011-longer-fights.md) | Более долгие бои за счёт снижения урона |
| [012](adr/012-measured-allocations.md) | GC-аллокации измерены, пулы пока не нужны |
| [013](adr/013-network-readiness.md) | Сетевая игра — авторитетный хост |
| [014](adr/014-unity-transport-direct.md) | Unity Transport напрямую, без Netcode for GameObjects |
| [015](adr/015-python-tooling-and-docs-layout.md) | Инструменты на Python и новая структура документации |
