# Практическая карта изучения SectorLab

Начните с Main и README: сыграйте партию, посмотрите HUD, смерть и возрождение.
Затем читайте конкретные коммиты через `git show <SHA>`, а код — в указанном файле.
Ни один SHA ниже не является запланированным: все эти коммиты уже существуют.
Новые упражнения делайте в отдельных ветках/копиях; исходную историю не переписывайте.

| Тема / реальный коммит | Код от `Assets/_Project/` | Проверка | Упражнение на 10–20 строк и ожидаемый результат |
| --- | --- | --- | --- |
| Игровое время, `f74d44c` | `Scripts/Battle/MatchClock.cs`, `MatchState.cs` | `Tests/EditMode/MatchStateTests.cs` | Добавьте тест двух шагов с Pause посередине: время и счёт изменяются только до/после паузы. |
| Одновременный исход, `f74d44c` | `Scripts/Battle/MatchRules.cs` | `Tests/EditMode/MatchRulesTests.cs` | Проверьте ровно 45:45 и оба счёта выше порога в одном тике: Draw, один terminal outcome. |
| Захват и contest, `3a2a9c7` | `Scripts/Objectives/RelayState.cs` | `Tests/EditMode/RelayStateTests.cs` | Проверьте 2 allies + 1 enemy на нейтральном узле: прогресс неподвижен, численного ускорения нет. |
| Дробное лечение, `359778b` | `Scripts/Objectives/RelayPoint.cs`, `Scripts/Units/Health.cs` | `Tests/EditMode/HealingTests.cs` | Разбейте секунду лечения на несколько тиков: итог тот же, Max не превышается, мёртвый не оживает. |
| Composition root, `0badd72`, `f1bf936` | `Scripts/Battle/GameLifetimeScope.cs` | `Tests/PlayMode/MatchScopeTests.cs` | Уберите регистрацию MatchClock в отдельной копии: тест обнаружит отсутствующую зависимость вместо скрытого fallback. |
| Владение ресурсом, `4c9ef8f` | `Scripts/Infrastructure/UnitFactory.cs`, `IUnitPrefabProvider.cs`, `UnitPrefabOwnership.cs` | `Tests/PlayMode/UnitFactoryTests.cs` | Дополните fake provider счётчиком release: смерть и Dispose не должны освободить один lease дважды. |
| Отмена поздней загрузки, `1395333`, `ca8ff08` | `Scripts/Infrastructure/AddressableUnitPrefabProvider.cs`, `Scripts/Units/UnitLifecycle.cs` | `Tests/PlayMode/UnitFactoryTests.cs`, `MainLifecycleTests.cs` | Завершите delayed load после cancellation: ни одного нового unit, reference освобождён после окончания load. |
| Пауза возрождения, `89968e1` | `Scripts/Units/RespawnTimer.cs`, `UnitLifecycle.cs` | `Tests/EditMode/RespawnTimerTests.cs`, `Tests/PlayMode/MainLifecycleTests.cs` | Убейте Scout, продвиньте 9 с, Pause, затем Resume и 1 с: одна новая генерация с полным HP. |
| Жесты и UI, `119cd44`, `c623e8c` | `Scripts/Player/UnitCommander.cs`, `TacticalCamera.cs` | `Tests/PlayMode/UnitCommanderTests.cs`, `TacticalCameraTests.cs` | Добавьте cancel касания после drag: камера может сдвинуться, приказ бойцу не появляется. |
| Сцена и интерфейс, `cabf1ee`, `e3510d8` | `Scripts/Presentation/BattleHud.cs`, `SafeAreaLayout.cs` | `Tests/PlayMode/BattleHudTests.cs` | Подайте асимметричный safeArea: кнопки и камера остаются внутри, тап по HUD не проходит в мир. |
| Событие и время жизни звука, `b2edadc` | `Scripts/Presentation/MatchAudio.cs`, `Scripts/Units/UnitAttack.cs` | `Tests/PlayMode/MatchAudioTests.cs`, `UnitAttackTests.cs` | На Pause дождитесь готового первого выстрела: ни damage, ни Fired; Resume даёт удар, Restart не оставляет clips. |

Каждое упражнение начинается с существующего проходящего теста. Изменяйте один
сценарий, сначала сформулируйте наблюдаемый результат, затем проверьте его.
Не считайте ошибку отсутствующего телефона успешной проверкой Android.

На ревью полезно объяснить: почему State не MonoBehaviour; почему AI думает раз
в игровую секунду; чем request cancellation отличается от физического окончания
Addressables IO; кто освобождает lease после смерти; почему clone не должен
зависеть от Library; какие проверки действительно сделал человек.

Здесь отражена автономная реализация с AI. Освоение и самостоятельные исправления
владелец подтверждает собственными упражнениями, а не заявлением о коммерческом опыте.
