# Сторонние зависимости SectorLab

Исходная лицензия проекта не меняется. Unity Engine и штатные Unity-пакеты
используются по условиям Unity; точные версии находятся в `Packages/packages-lock.json`.

| Пакет | Версия / источник | Лицензия | Проверка |
| --- | --- | --- | --- |
| VContainer | [1.19.0](https://github.com/hadashiA/VContainer/releases/tag/1.19.0), commit `5401e5a7ebc4980a2b82141ffc26391a6547edd7` | MIT, Copyright 2020 hadashiA; [текст](licenses/VContainer-MIT.txt) | Unity 6000.6.4f1: compilation + 53/53 EditMode, W4.1 |
| Addressables | [2.11.2](https://docs.unity3d.com/Packages/com.unity.addressables@2.11/manual/index.html), официальный Unity registry | Unity Companion License, Copyright 2020 Unity Technologies ApS; [notice](licenses/Addressables-LICENSE.md) | Local Units: реальные prefab loads, leases и встроенный APK catalog/bundle, W5–W8 |
| UniTask | [2.5.11](https://github.com/Cysharp/UniTask/releases/tag/2.5.11), commit `2e993ff18f28c931602a07292df0b0804eebef99` | MIT, Copyright 2019 Yoshifumi Kawai / Cysharp, Inc.; [текст](licenses/UniTask-MIT.txt) | Spawn/respawn ждут физическую загрузку; cancellation/error/late completion проверены |
| AI Navigation | 2.0.16, Unity registry | Unity Companion License; [установленный текст](licenses/com.unity.ai.navigation-LICENSE.md) | Существующий NavMeshSurface и сохранённая baked Main сетка |
| Input System | 1.20.1, Unity registry | Unity Companion License; [установленный текст](licenses/com.unity.inputsystem-LICENSE.md) | Mouse/Touchscreen, virtual touch tests |
| Universal Render Pipeline | 17.6.0, штатный пакет Unity 6000.6 | Unity Companion License; [установленный текст](licenses/com.unity.render-pipelines.universal-LICENSE.md) | Прежние Mobile/PC renderer и материалы |
| uGUI | 2.6.0, штатный пакет Unity | [Установленный текст](licenses/com.unity.ugui-LICENSE.md) | HUD, обучение, результат, UI input blocking |
| Test Framework | 1.8.0, штатный пакет Unity | [Установленный текст](licenses/com.unity.test-framework-LICENSE.md) | Только тесты/Editor; не игровой framework |
| Profiling Core | 1.0.3, Unity registry | [Установленный текст](licenses/com.unity.profiling.core-LICENSE.md) | Зависимость Addressables/диагностики |
| Unity Pipeline | 0.8.0-exp.1, Unity registry | Unity Terms of Service; [установленный текст](licenses/com.unity.pipeline-LICENSE.md) | Editor automation/CLI; не подсистема игрового матча |
| Gitleaks | [8.30.1](https://github.com/gitleaks/gitleaks/releases/tag/v8.30.1) | MIT, Copyright 2019 Zachary Rice; [текст](licenses/Gitleaks-MIT.txt) | Внешний локальный инструмент аудита/pre-commit, не включён в APK или Git как binary |

UPM использует официальный Git URL с фиксированным tag. Фактически разрешённый
commit проверяется в lock; обновление версии требует отдельной проверки.
VContainer используется в GameLifetimeScope, UniTask и Addressables — в настоящей
цепочке UnitLifecycle → UnitFactory → prefab provider, а не в отдельном sample.
Прочие штатные Editor-пакеты/транзитивные зависимости перечислены в lock;
их оригинальные лицензии остаются в соответствующих UPM-пакетах.

Геометрия арены/роботов, кольца, линии выстрелов и UI используют Unity primitives
и сохранённые материалы проекта. Четыре коротких SFX MatchAudio синтезируются
оригинальным кодом; сторонняя музыка, изображения, модели и шрифты не загружались.
LegacyRuntime UI font предоставлен Unity. Скриншоты evidence получены из
настоящего Main / собственного APK, а не из генератора изображений.

Лицензия основного кода отдельно не назначалась: корневого LICENSE в исходном
проекте нет. Этот файл перечисляет происхождение зависимостей и не выдаёт
пользовательский код за лицензированный MIT автоматически.
