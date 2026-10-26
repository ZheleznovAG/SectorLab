# Архитектура

Одна сцена `Assets/_Project/Scenes/Main.unity`, одна сборка кода `SectorLab` (`Scripts/SectorLab.asmdef`),
две тестовые сборки. Около 4,5 тыс. строк игры в 51 файле и 5,2 тыс. строк тестов.

## Слои

```mermaid
flowchart TB
    subgraph Rules["Правила — чистый C#, EditMode-тесты"]
        MatchRules & MatchState & MatchClock & RelayState & RespawnTimer & EnemyDecisionRules & TeamPerspective
    end
    subgraph Sim["Симуляция — тик 0,1 с"]
        BattleController --> RelayPoint
        BattleController --> UnitAttack
        UnitLifecycle --> UnitFactory --> AddressableUnitPrefabProvider
        MatchCommands
    end
    subgraph View["Представление — MatchModel (R3) и локальные компоненты"]
        BattleHud & MatchAudio & BattleResultView & TacticalCamera & TutorialView
    end
    subgraph Input["Ввод"]
        UnitCommander
        EnemyTacticsController
    end
    subgraph Net["Сеть — Network/"]
        NetworkMatchBootstrap --> HostSession & ClientSession --> UtpChannel
    end
    Sim --> Rules
    BattleController -- PublishModel --> MatchModel --> View
    UnitCommander -- UnitCommand --> MatchCommands --> Unit
    EnemyTacticsController --> Unit
    HostSession -- приказ клиента --> MatchCommands
    ClientSession -- снимок --> MatchModel
```

- **Правила** (`Battle/MatchRules`, `MatchState`, `MatchClock`, `Objectives/RelayState`, `Units/RespawnTimer`,
  `AI/EnemyDecisionRules`, `Battle/TeamPerspective`) — обычные классы без Unity. Их покрывают EditMode-тесты.
- **Симуляция.** `BattleController` копит `deltaTime` и делает фиксированные тики 0,1 с. В каждом тике
  сначала бой (`UnitAttack.Simulate`: перезарядка и урон), затем узлы (`RelayPoint.Simulate`: захват
  и ремонт), затем счёт и время (`MatchState.Tick`), затем `PublishModel()` и событие `Ticked`
  (респаун, ИИ, сеть). Наведение, поиск цели и NavMesh-движение покадровые и тоже влияют
  на исход. Фиксированный шаг таймеров не гарантирует одинаковый матч при разном FPS.
- **Модель для зрителя.** `Battle/MatchModel` — `ReactiveProperty` R3: очки, доход, время, пауза, исход,
  прогноз, вид узлов (`RelayView`). Уведомляет только при изменении. Заголовок, прогноз и карточки узлов
  HUD подписаны на неё; на клиенте её заполняют снимки хоста. Миграция представления частичная:
  карточки бойцов опрашивают локальный `UnitLifecycle` раз в 0,1 с, графика `RelayPoint` читает свой
  `RelayState`, звук итога/прогноза/выстрела зависит от локальных событий. Это вызывает расхождения
  клиента, описанные в [аудите 26.10](audit/2026-10-26-architecture-audit.md).
- **Приказы — данные.** `UnitCommander` превращает тапы в `UnitCommand` и отдаёт в `ICommandSink`.
  Локально это `MatchCommands`: проверяет, что матч идёт, юнит жив и принадлежит отправителю, а цель
  допустима, и только потом двигает или атакует. На клиенте это `ClientCommandSink`, который отправляет
  приказ хосту.
- **Перспектива.** `TeamPerspective` от `UnitCommander.LocalTeam` превращает синих/красных в «ВЫ/ВРАГ»
  для HUD, звука и экрана итога. У красного клиента «ВЫ» — красные.

## Сборка матча и ресурсы

- `Battle/GameLifetimeScope` (VContainer) — composition root сцены: регистрирует `MatchClock`, `MatchState`,
  `BattleController`, ИИ, ростер, фабрику и провайдер префабов. Зависимости внедряются один раз;
  `Resolve` в `Update` нет ([ADR-004](adr/004-match-composition-root.md)).
- `Units/UnitLifecycle` асинхронно (UniTask) спавнит ростер (`Data/UnitRoster.asset`, 6 слотов) через
  `Infrastructure/UnitFactory` → `AddressableUnitPrefabProvider`. Каждый экземпляр держит аренду
  (`UnitPrefabLease`) и освобождает её при смерти или выгрузке. Отмена при перезапуске дожидается IO
  и не даёт позднего спавна ([ADR-005](adr/005-async-prefab-ownership.md)). Ошибка загрузки показывает
  «Повторить / Выйти».
- `Unit.Id` = индекс слота ростера и сохраняется при респауне. Это адрес юнита в приказах и снимках.
  Поколения матча в протоколе пока нет. `Unit.All` и `Time.timeScale` глобальны: один scope не
  обеспечивает изоляцию нескольких одновременных матчей в одном процессе.
- Роли и узлы — ScriptableObject: `Units/UnitStats` (`Data/Units/*.asset`), `Objectives/RelayConfig`
  (`Data/Relays/*.asset`).

## Сеть ([ADR-013](adr/013-network-readiness.md), [ADR-014](adr/014-unity-transport-direct.md))

Авторитетный хост, не lockstep: NavMesh и float-физика недетерминированы между устройствами.

```mermaid
sequenceDiagram
    participant C as Клиент (красные)
    participant H as Хост (синие, считает матч)
    C->>H: Command (17 байт), reliable sequenced
    H->>H: Issuer := Enemy, MatchCommands.Execute
    loop каждый тик 0,1 с и heartbeat 0,25 с, пока часы стоят
        H-->>C: MatchState (32 байта на 3 узла), unreliable sequenced
        H-->>C: Units (1 + 12·N байт), unreliable sequenced
    end
    C->>C: MatchModel ← снимок, прокси-юниты ← позиции и HP
```

- `NetworkLaunch` — статический выбор роли (Offline/Host/Client) и канала до загрузки `Main`.
  Ставят его меню «Сеть» (`NetworkMenu`), флаги командной строки (`NetworkLauncher`) или тест.
- `NetworkMatchBootstrap` на объекте `/Battle` превращает сцену в хост или клиент. Хост отключает ИИ
  красных, шлёт снимки на `Ticked`/`Finished` и повторяет их, пока часы стоят; ждёт соперника на паузе.
  Клиент вызывает `BattleController.BecomeRemoteView()` (своей симуляции нет), делает юниты прокси
  (агент, движение и атака выключены) и показывает результат хоста.
- `HostSession` / `ClientSession` работают поверх `IMessageChannel`. Его реализации: `LoopbackChannel`
  (в памяти, для тестов) и `UtpChannel` (Unity Transport, UDP, один удалённый игрок). `NetworkPump`
  (DontDestroyOnLoad) двигает пакеты раньше игровых скриптов и переживает перезапуск сцены.
- Кодеки (`UnitCommandCodec`, `MatchSnapshot`, `UnitsSnapshot`) не доверяют байтам: усечённые данные,
  неизвестные enum и не-конечные float отвергаются. `UnitsSnapshot.Read` пока может частично изменить
  буфер до обнаружения ошибки. Два типа снимков делят один sequenced pipeline; результат отправки
  не передаётся вызывающему коду. Эти ограничения и план исправлений — в аудите 26.10.

## Где что лежит

| Путь | Содержимое |
|---|---|
| `Assets/_Project/Scripts/{AI,Battle,Infrastructure,Network,Objectives,Player,Presentation,Units}` | Код игры, namespace `SectorLab.<Папка>` |
| `Assets/_Project/Tests/EditMode`, `Tests/PlayMode` | Тесты; PlayMode грузят настоящую `Main` с NavMesh |
| `Assets/_Project/Editor` | `AddressableUnitSetup` (меню сборки контента), `GameViewVerification` (размеры Game View) |
| `Assets/_Project/Data` | Ростер, роли, узлы (ScriptableObject) |
| `Assets/_Project/Prefabs`, `Materials`, `Scenes` | Префабы юнитов (Addressables, группа Local Units), материалы, сцена и NavMesh |
| `Assets/Packages`, `Assets/packages.config` | NuGet-пакеты R3 (NuGetForUnity), в git, чтобы чистый клон компилировался |
| `Packages/manifest.json` | UPM: VContainer, UniTask, R3.Unity, NuGetForUnity по git-тегам; остальное — Unity registry |
| `Tools/sl.py`, `Tools/sectorlab/` | Инструменты разработчика ([DEVELOPMENT](DEVELOPMENT.md)) |
| `.github/workflows/tests.yml` | GameCI, только ручной триггер; автоматической проверки PR в конфигурации нет |

## С чего читать код

1. `Battle/MatchRules.cs`, `Objectives/RelayState.cs` — правила матча и захвата.
2. `Battle/GameLifetimeScope.cs` → `Battle/BattleController.cs` — сборка и тик матча.
3. `Battle/UnitCommand.cs`, `Battle/MatchCommands.cs`, `Player/UnitCommander.cs` — от тапа до приказа.
4. `Battle/MatchModel.cs`, `Presentation/BattleHud.cs` — реактивная модель и подписки HUD.
5. `Units/UnitLifecycle.cs`, `Infrastructure/UnitFactory.cs`, `Infrastructure/AddressableUnitPrefabProvider.cs` — async-спавн.
6. `AI/EnemyDecisionRules.cs`, `AI/EnemyTacticsController.cs` — ИИ.
7. `Network/MatchSessions.cs` → `Network/NetworkMatchBootstrap.cs` → `Network/UtpChannel.cs` — сеть.

## Тесты

- **EditMode** (`SectorLab.Tests.EditMode`) — правила, кодеки, перспектива: быстрые, без сцены.
- **PlayMode** (`SectorLab.Tests.PlayMode`) — грузят `Main` или синтетическую арену (`TestArena`) и ведут
  её виртуальными касаниями. Здесь проверяются загрузка, отмена и ошибки ресурсов, десять перезапусков,
  пять полных матчей тактиками игрока (`MainTacticsTests`, до 15 минут), бюджет GC-аллокаций, сеть
  через loopback и реальный UDP на localhost.
- Тесты ждут по игровому времени, а не по реальному ([ADR-008](adr/008-game-time-in-navigation-tests.md)).
  Общие ассеты, изменённые тестом, откатываются в `UnityTearDown`, а не в `finally`.
