# Сторонние зависимости SectorLab

Исходная лицензия проекта не меняется. Unity Engine и штатные Unity-пакеты
используются по условиям Unity; точные версии находятся в `Packages/packages-lock.json`.

| Пакет | Версия / источник | Лицензия | Проверка |
| --- | --- | --- | --- |
| VContainer | [1.19.0](https://github.com/hadashiA/VContainer/releases/tag/1.19.0), commit `5401e5a7ebc4980a2b82141ffc26391a6547edd7` | MIT, Copyright 2020 hadashiA; [текст](licenses/VContainer-MIT.txt) | Unity 6000.6.4f1: compilation + 53/53 EditMode, W4.1 |
| Addressables | [2.11.2](https://docs.unity3d.com/Packages/com.unity.addressables@2.11/manual/index.html), официальный Unity registry | Unity Companion License, Copyright 2020 Unity Technologies ApS; [notice](licenses/Addressables-LICENSE.md) | Unity 6000.6.4f1: compilation + 53/53 EditMode, W5.1; local content — W5.3/W8 |

UPM использует официальный Git URL с фиксированным tag. Фактически разрешённый
commit проверяется в lock; обновление версии требует отдельной проверки.
VContainer используется в GameLifetimeScope. Addressables на W5.1 установлен;
локальный контент и factory подключаются в W5.3–W5.6.
