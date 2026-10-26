# ADR 004 — composition root сцены и lifetime служб

Дата: 2026-10-11. Задачи: W4.1–W4.3, NFR-004/FR-016.

`Main` уже содержит BattleController, EnemyTacticsController и ссылки на relay.
Нужно дать им одно состояние и одни часы, сохранив существующий цикл симуляции.

Решение: один `GameLifetimeScope` на объекте Battle. VContainer 1.19.0
инжектирует зарегистрированные компоненты до первого Update. Чистый MatchState
получает MatchClock через конструктор; сцена хранит конкретные Unity-ссылки.

| Зависимость | Lifetime / владелец | Получатели |
| --- | --- | --- |
| MatchClock | Scoped, текущая сцена | MatchState |
| MatchState | Scoped, текущая сцена | BattleController, UnitFactory; через controller AI |
| BattleController | Уже существующий компонент сцены | AI, UnitLifecycle, последующий HUD |
| EnemyTacticsController | Уже существующий компонент сцены | Подписан на Ticked только пока enabled |
| IUnitPrefabProvider / UnitFactory | Scoped, dispose контейнером | UnitLifecycle |
| UnitLifecycle | Компонент Battle | Шесть slots, death subscriptions, respawn countdown |
| UnitRoster | Unity asset, неизменяемый в матче | UnitLifecycle |
| UnitStats / RelayConfig | Unity assets, неизменяемые в матче | Существующие юниты/узлы |

Здесь Scoped выбран даже при одном root без детей: смысл — один матч, а не
постоянная служба приложения. При unload контейнер уничтожается вместе со сценой.
Никакого DontDestroyOnLoad, поиска контейнера или Resolve в Update нет.
Resolve разрешён в composition root и проверочных сценариях.

Уточнение 26.10.2026: это описание служб матча. Добавленный позже `NetworkPump`
использует `DontDestroyOnLoad` для соединения между матчами ([ADR-014](014-unity-transport-direct.md));
его lifetime относится к сетевой сессии. `Unit.All` и `Time.timeScale` пока глобальны,
поэтому наличие Scoped-регистраций не означает изоляцию нескольких матчей в процессе.

W4.2 проверяет реальные регистрации и текущую Main. W4.3 удаляет исторический
fallback `new MatchState(new MatchClock())` из Awake: пропущенная регистрация
должна обнаруживаться. Изолированные fixture явно передают состояние, не создавая
вторую игровую точку сборки. Battle/AI сообщают об отсутствии injection в Start.
До Start Battle.Update при отсутствии State не симулирует. В W5 UnitFactory
добавлен после установки Addressables/UniTask и проверки модели владения.
Старт и respawn Main используют один factory; runtime ссылки не записываются в roster asset.

Проверки: MatchScopeTests — общая identity state/clock, новый экземпляр после
dispose, ошибки при отсутствии Clock/State; MainMatchTests — настоящие перезагрузки.
