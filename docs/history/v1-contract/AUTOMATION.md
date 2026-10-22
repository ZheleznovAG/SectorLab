# Повторяемые проверки

Запускать из PowerShell в рабочем проекте с открытым Unity Editor и Pipeline:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/check-repository.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/check-gates.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/verify-unity.ps1 -Task W1.1 -Mode EditMode
```

`verify-unity.ps1` обновляет AssetDatabase, ожидает компиляцию и запускает
только сборку тестов SectorLab. Сохраняет реальный CLI-ответ, UTC-время,
исходный HEAD и SHA-256 Unity-входов в `evidence/tests-<Task>-<mode>.json`.
FAIL, skipped, пустой набор или таймаут останавливают скрипт с ошибкой.
Новая проверка той же задачи обновляет её лог; изменения лога видны в Git.

`check-repository.ps1` проверяет whitespace, package JSON, GUID/meta и зависимости
реестра задач. `check-gates.ps1` сверяет [GATES.json](GATES.json) с наличием
доказательств и результатами тестов/build. `-RequireComplete` требует PASS всех
G0–G9. Скрипт проверяет записи; наблюдения игры, устройства и ручного плейтеста
должны быть выполнены и записаны исполнителем, он не создаёт их автоматически.

Gates закрываются после наблюдаемого результата, подходящих тестов, обзора
staged diff и локального коммита. Перед каждым коммитом выбирать явные пути;
push, APK, ZIP, Library/Temp/Logs в эту цепочку не входят.

После внешнего изменения открытой `.unity` Unity может показать модальное
окно Reload. Pipeline-команды тогда ждут основной поток и завершаются timeout.
Проверить текст окна и сохранность локальных/несохранённых данных, перечитать
нужную сцену, затем повторить проверку. Timeout не равен результату тестов.
Ответ `editor_play` сам по себе не подтверждает вход в Play при compile error:
проверять groundTruth compilationFailed и фактический editor_status.

## Метрики Android

`Tools/measure-android.ps1 -Task <new-name> -DurationSeconds 600 -AutoRestart
-ApkPath <local-apk>` считывает actual-present timestamps только Unity SurfaceView,
снимает own process CPU и PSS/RSS. Каждый Task сохраняет JSON, samples CSV и
raw frames CSV, не перезаписывая старые измерения. `-AdbPath` позволяет указать
другую установленную SDK. Hash `ApkPath` — ссылка на файл, не независимая проверка
того, какой APK установлен.

`-AutoRestart` проверяет три разделённых жёлтых пикселя кнопки результата в
скриншоте своей foreground activity и нажимает её. Счётчик означает detected
restart taps, не подтверждённые автоматически lifecycle assertions. Result
screenshots остаются вне Git для просмотра; чужой foreground не снимается. Это реальная автоматическая нагрузка, но не human playtest. Mono GC allocations
и GPU time недоступны без Unity Profiler; Android ART не заменяет Mono.

Предыдущие серии получены отдельным локальным
сборщиком и не выдаются за запуск этого скрипта.

## Возобновление после лимита

По поручению пользователя установлена локальная Windows Task Scheduler задача
`SectorLab-ResumeAfterLimit`: **12.10.2026 13:19:48 +07:00**, через минуту после
reset primary window 13:18:48. Время reset повторно прочитано через локальный
Codex app-server 12.10.2026 04:10:37 UTC. Фактический запуск произошёл
12.10.2026 13:19:48 +07:00 и завершился с кодом **1**; automatic resume **FAIL**. Первый JSONL пуст, итогового сообщения и completed marker нет. Причина первого
сбоя по имеющимся журналам не установлена. Задача сейчас Ready, следующего запуска нет;
разработка продолжена по сообщениям пользователя в текущей сессии.

Локальный wrapper сохранён в backup и исправлен: stdin передаётся в UTF-8,
stdout/stderr процесса записываются раздельно без PowerShell native pipeline
и изменения кодировки неинтерактивной Console; ошибки wrapper также записываются.
Безопасный `-SelfTest` запускает только `codex --version`: **PASS**, exitCode=0,
CLI 0.162.1. Это проверка запуска и записи потоков, не успешный автоматический
прогон разработки. [Фактические сведения](evidence/resume-after-limit-check.json).
Новый фоновый агент одновременно с текущей работой не запущен; прошедший таймер
не переназначен на произвольное время.

Таймер требует включённого ПК и входа пользователя. Скрипт, конфигурация с thread ID
и журнал находятся вне репозитория в локальном каталоге приложения. Wrapper предназначен для возобновления текущей ветки работы; если все G0–G9 уже PASS,
запуск пропускает. Полное выполнение после исправления ещё не проверено. Уже выполненный privacy force-with-lease повторять не требуется; новые push не разрешены.
