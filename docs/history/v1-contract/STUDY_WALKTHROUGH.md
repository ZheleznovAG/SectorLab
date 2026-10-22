# Практическая карта изучения SectorLab

Начните с Main и README: сыграйте партию, посмотрите HUD, смерть и возрождение.
Затем читайте конкретные коммиты через `git show <SHA>`, а код — в указанном файле.
Ни один SHA ниже не является запланированным: все эти коммиты уже существуют.
Новые упражнения делайте в отдельных ветках/копиях; исходную историю не переписывайте.

SHA ниже относятся к опубликованной истории после разрешённой очистки приватных
метаданных. [Карта соответствия](../../evidence/privacy-commit-map.txt) сохраняет связь
с исходными sourceHead в тестовых отчётах; код и результаты проверок не менялись.

| Тема / реальный коммит | Код от `Assets/_Project/` | Проверка | Упражнение на 10–20 строк и ожидаемый результат |
| --- | --- | --- | --- |
| Игровое время, `1204612` | `Scripts/Battle/MatchClock.cs`, `MatchState.cs` | `Tests/EditMode/MatchStateTests.cs` | Добавьте тест двух шагов с Pause посередине: время и счёт изменяются только до/после паузы. |
| Одновременный исход, `1204612` | `Scripts/Battle/MatchRules.cs` | `Tests/EditMode/MatchRulesTests.cs` | Проверьте ровно 45:45 и оба счёта выше порога в одном тике: Draw, один terminal outcome. |
| Захват и contest, `43a68d3` | `Scripts/Objectives/RelayState.cs` | `Tests/EditMode/RelayStateTests.cs` | Проверьте 2 allies + 1 enemy на нейтральном узле: прогресс неподвижен, численного ускорения нет. |
| Дробное лечение, `b27bb11` | `Scripts/Objectives/RelayPoint.cs`, `Scripts/Units/Health.cs` | `Tests/EditMode/HealingTests.cs` | Разбейте секунду лечения на несколько тиков: итог тот же, Max не превышается, мёртвый не оживает. |
| Composition root, `ef3f402`, `5cc9211` | `Scripts/Battle/GameLifetimeScope.cs` | `Tests/PlayMode/MatchScopeTests.cs` | Уберите регистрацию MatchClock в отдельной копии: тест обнаружит отсутствующую зависимость вместо скрытого fallback. |
| Владение ресурсом, `a2b266d` | `Scripts/Infrastructure/UnitFactory.cs`, `IUnitPrefabProvider.cs`, `UnitPrefabOwnership.cs` | `Tests/PlayMode/UnitFactoryTests.cs` | Дополните fake provider счётчиком release: смерть и Dispose не должны освободить один lease дважды. |
| Отмена поздней загрузки, `b7dcb46`, `e35a350` | `Scripts/Infrastructure/AddressableUnitPrefabProvider.cs`, `Scripts/Units/UnitLifecycle.cs` | `Tests/PlayMode/UnitFactoryTests.cs`, `MainLifecycleTests.cs` | Завершите delayed load после cancellation: ни одного нового unit, reference освобождён после окончания load. |
| Пауза возрождения, `d170cb7` | `Scripts/Units/RespawnTimer.cs`, `UnitLifecycle.cs` | `Tests/EditMode/RespawnTimerTests.cs`, `Tests/PlayMode/MainLifecycleTests.cs` | Убейте Scout, продвиньте 9 с, Pause, затем Resume и 1 с: одна новая генерация с полным HP. |
| Жесты и UI, `82a98be`, `2cd9ecb` | `Scripts/Player/UnitCommander.cs`, `TacticalCamera.cs` | `Tests/PlayMode/UnitCommanderTests.cs`, `TacticalCameraTests.cs` | Добавьте cancel касания после drag: камера может сдвинуться, приказ бойцу не появляется. |
| Сцена и интерфейс, `101791b`, `e411962` | `Scripts/Presentation/BattleHud.cs`, `SafeAreaLayout.cs` | `Tests/PlayMode/BattleHudTests.cs` | Подайте асимметричный safeArea: кнопки и камера остаются внутри, тап по HUD не проходит в мир. |
| Событие и время жизни звука, `6271c1b` | `Scripts/Presentation/MatchAudio.cs`, `Scripts/Units/UnitAttack.cs` | `Tests/PlayMode/MatchAudioTests.cs`, `UnitAttackTests.cs` | На Pause дождитесь готового первого выстрела: ни damage, ни Fired; Resume даёт удар, Restart не оставляет clips. |

Каждое упражнение начинается с существующего проходящего теста. Изменяйте один
сценарий, сначала сформулируйте наблюдаемый результат, затем проверьте его.
Не считайте ошибку отсутствующего телефона успешной проверкой Android.

На ревью полезно объяснить: почему State не MonoBehaviour; почему AI думает раз
в игровую секунду; чем request cancellation отличается от физического окончания
Addressables IO; кто освобождает lease после смерти; почему clone не должен
зависеть от Library; какие проверки действительно сделал человек.

Здесь отражена автономная реализация с AI. Освоение и самостоятельные исправления
владелец подтверждает собственными упражнениями, а не заявлением о коммерческом опыте.
