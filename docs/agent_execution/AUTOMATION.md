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
