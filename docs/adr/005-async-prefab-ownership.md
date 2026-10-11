# ADR 005 — локальные префабы, UniTask и отмена

Дата: 2026-10-11. План: W5.3–W5.6, FR-013/017/022/023/027, NFR-006.
На момент принятия package installation проверена отдельно; результаты
интеграции добавляются после настоящих тестов.

Контекст: все роли/варианты уже существуют как prefab. Начальные 6 экземпляров
сцены нужно заменить единым созданием и возрождением через 10 игровых секунд.
Смена сцены может произойти раньше физического окончания загрузки.

Выбран LoadAssetAsync + Object.Instantiate. Provider передаёт prefab с lease,
который владеет одним Addressables reference и освобождается ровно один раз.
Повторные загрузки одного ключа используют внутренний reference counting
Addressables; каждая созданная копия удерживает свою reference до OnDestroy.
InstantiateAsync/ReleaseInstance в этой модели не используются.
Весь контент в локальной группе: никакого remote catalog/CDN.

Factory получает provider от GameLifetimeScope и владеет временем жизни
созданных юнитов. Scope dispose отменяет запросы и уничтожает свои экземпляры.
После завершения загрузки проверяется token **до Instantiate**. Если отменённый
запрос физически завершился позднее, его lease освобождается без создания юнита.
Ошибка ключа освобождает failed handle; UI сообщает ошибку и даёт Retry/Exit.

Provider сначала выполняет LoadResourceLocationsAsync и освобождает lookup
handle. Пустой список даёт понятную собственную ошибку без вызова LoadAssetAsync
с неверным ключом. Счётчики adapter учитывают lookup и prefab references.
Это появилось после первого W5.4 FAIL: штатный InvalidKeyException выдавал Error
и отложенную ошибку форматирования stack trace. Native crash Editor после прогона
зафиксирован отдельно; его причина не объявляется установленной.

Отмена не обещает физического прекращения AssetBundle IO. Provider ожидает
финальный handle без автоматического release от UniTask, затем передаёт или
освобождает его. Отмена до запроса не создаёт operation. Scene lifetime и
caller token объединяются в Factory. Таймер respawn живёт в game ticks;
UniTask применяется для загрузки/ожидания, а не для подмены игрового clock.

Unity Awaitable тоже позволяет асинхронные операции и отмену. UniTask выбран
для прямого typed await Addressables, controllable completion sources в
тестах и единых cancellation API этого проекта. Awaitable остаётся штатным
вариантом Unity; обязательной обёртки вокруг синхронного кода здесь нет.

Источники: [Addressables handles](https://docs.unity3d.com/Packages/com.unity.addressables@2.11/manual/AddressableAssetsAsyncOperationHandle.html),
[UniTask](https://github.com/Cysharp/UniTask),
[Unity Awaitable](https://docs.unity3d.com/6000.0/Documentation/Manual/async-await-support.html).
API проверяется по установленным пакетам, не только по документации.

Обязательные проверки: настоящий prefab spawn/death/release; delayed provider
после cancel/dispose; missing key; отсутствие двойного respawn; freeze в pause;
safe NavMesh spawn; 10 Main restarts с нулевыми остатками handles/юнитов.
Content build/Player проверяются отдельно: Editor AssetDatabase не доказывает APK.
