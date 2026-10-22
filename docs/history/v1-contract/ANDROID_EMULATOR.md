# Android Studio Emulator — локальная проверка SectorLab

Эксперимент W8.2e по поручению пользователя, 12.10.2026. Проверяется возможность
играть без подключённого телефона. Требования NFR-008/NFR-009 относятся к
реальному Android-устройству; эмулятор не закрывает их автоматически.

**Итог возобновлённой проверки: v1.0 в этом эмуляторе — FAIL.** Запущенная
к началу сессии игра оказалась прототипом **0.1.0**: установленный APK выгружен
через ADB и совпал по SHA-256 с baseline. Его первоначальный кадр сохранён,
но повторный запуск baseline также завершился SIGILL.
[Проверенные результаты](../../evidence/W8-emulator-resume.json).

Повторная регрессия: **63/63 EditMode, 112/112 PlayMode PASS**, без
failed/skipped/inconclusive. Обычная Main в Editor завершилась естественным
Defeat **0:45** за 33,1 игровых секунды, consoleErrors=0;
[отчёт](../../evidence/W8-emulator-resume-editor.json),
[Game View](../../evidence/W8-emulator-resume-editor.png). Это автоматическая проверка,
не человеческая партия и не успешный запуск Android.

## Установленная среда

Android Studio, Android Emulator **37.1.11.0**, platform-tools **37**,
WHPX доступен. SDK находится вне репозитория: `%LOCALAPPDATA%/Android/Sdk`.
Созданы отдельные AVD Pixel 5: `SectorLab_API30`, `SectorLab_API34`,
`SectorLab_API35`; исходных пользовательских AVD не было. Системные образы:
Google APIs x86_64, API 30 revision 16, API 34 revision 14, API 35 revision 9.
Последние два AVD: 4 GiB RAM, 4 виртуальных ядра, микрофон отключён.

APK для телефона `Builds/Android/SectorLab-W8-selection.apk` содержит только
`arm64-v8a`. Эти образы предоставляют ARM64 native bridge, но успешная установка
не подтверждает совместимость всех нативных функций Unity.

## Фактические попытки

| Вариант | Наблюдение |
| --- | --- |
| Android 11/API 30, auto/Vulkan | Android загрузился, установка Success; при запуске APK native SIGSEGV, есть сообщения NDK translation о неизвестных Vulkan-функциях |
| API 30, forced GLES / host GPU | Предупреждение Unity о графике; EGL не создал требуемый контекст, native SIGTRAP |
| Отдельная x86_64-сборка Unity | Failed, 1 error/2 warnings; Unity сообщает, что архитектура x86_64 удалена. APK не создан |
| Android 14/API 34, swangle без GuestAngle | Android загрузился, APK установлен; EGL не создал ES 3.1, предупреждение/ANR |
| API 34, ANGLE через per-app settings | ANGLE APK отсутствует, `Found 0`; fallback не помог. Обе экспериментальные global settings удалены |
| Android 15/API 35, GuestAngle | Boot parameters ES 3.1; QEMU завершился до sys.boot_completed в swangle и swiftshader. Windows Application Error: 0xc0000409 / 0xc0000005. SectorLab не запускался |
| API 34, explicit GuestAngle + swangle | sys.boot_completed=1; SurfaceFlinger: ANGLE, OpenGL ES 3.1.0. Финальный ARM64 APK: SIGILL в Unity Main Thre; с forced GLES — SIGILL в Loading.Preload |

В предыдущей сессии backtrace содержал только `pc 0 <unknown>`. При возобновлении
получен конкретный native stack: `Loading.Preload`, `libunity.so + 0x586c10`.
`ndk_translation` сообщает `Undefined instruction 0xd50320bf`. Локальный NDK
`llvm-objdump` подтверждает по этому адресу инструкцию **SEVL**. Это отказ
ARM-трансляции нативного движка; C# не менялся. Связь именно с Addressables
не доказана: baseline без этой цепочки тоже падает на SEVL, в Unity Main Thre.

| Повторная попытка | Фактический результат |
| --- | --- |
| v1.0 Activity + OpenGLES3, APK `02ABBB74…5F68F` | Build Succeeded, 0 errors/1 warning; запуск FAIL, SIGILL в Loading.Preload |
| Финальный v1.0 GameActivity, APK `654CB514…1237DA`, forced GLES, single graphics thread, job-worker-count 0 | JobQueue=0 подтверждён журналом; запуск FAIL, тот же SEVL/SIGILL |
| Возврат baseline 0.1.0, forced GLES | Установка Success; повторный запуск FAIL, SEVL/SIGILL в Unity Main Thre |

[Activity build](../../evidence/android-build-W8-emulator-activity.json) — отдельный
диагностический APK, не замена телефонного кандидата. Настройки проекта
восстановлены; Unity и его нативные библиотеки не подменялись. Полная партия,
offline acceptance и замер производительности v1.0 в этой VM недоступны.

[Неудачная x86_64-сборка](../../evidence/android-build-W8-emulator-x64.json)
сохранена как FAIL, не заменяет успешную ARM64-сборку. Настройки ARM64 восстановлены.

## Окно и повторный запуск

Выход окна за экран исправлен с учётом Windows DPI 125%. Команда выбирает только
окно Android Emulator указанного AVD, уменьшает и центрирует его в рабочей
области основного монитора; проверяет фактические границы после коррекции Qt:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/fit-android-emulator-window.ps1 -AvdName SectorLab_API34
```

Проверенный способ загрузить Android 14 с ANGLE:

```powershell
$sdk = Join-Path $env:LOCALAPPDATA 'Android/Sdk'
& "$sdk/emulator/emulator.exe" -avd SectorLab_API34 -port 5554 -gpu swangle -feature GuestAngle,-BluetoothEmulation,-WiFiPacketStream -no-boot-anim -no-snapshot-load -no-snapshot-save -no-metrics -crash-report-mode disabled
```

Команда требует, чтобы порт 5554 не занимал другой AVD. Не использовать
`-wipe-data`, не перезаписывать чужие AVD. Для установки и управления этой VM
всегда указывать `adb -s emulator-5554`; реальные телефоны не затрагиваются.
Управление Wi-Fi/Bluetooth относится только к VM. Производительность
программной графики и ARM-трансляции не является FPS телефона.

`Tools/measure-android.ps1` теперь принимает `-DeviceSerial emulator-5554`,
учитывает Android 14 `topResumedActivity` и записывает `deviceKind`. Синтаксис
обоих PowerShell-скриптов проверен. Коррекция окна реально дала `fits=true`.
30-секундный замер не состоялся: приложение уже упало, foreground precondition
отклонил запуск. FPS/память этого эксперимента не заявляются.

Полные журналы, APK, AVD и SDK остаются вне Git. В отчёт включаются только
проверенные изображения собственной игры и выбранные сообщения. Эмулятор
записывает домашние пути и ADB public key; полный stdout публиковать нельзя.

## Документация поставщиков

- [Android: ускорение CPU/GPU эмулятора](https://developer.android.com/studio/run/emulator-acceleration).
- [AOSP: native bridge](https://android.googlesource.com/platform/art/+/main/libnativebridge/README.md).
- [Unity 6.5: удаление x86_64 Android](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity65).
- [Unity 6.6: минимум GLES 3.1](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity66).
- [Unity: Android requirements; эмуляторы официально не поддерживаются](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/introducing/requirements-and-compatibility).
