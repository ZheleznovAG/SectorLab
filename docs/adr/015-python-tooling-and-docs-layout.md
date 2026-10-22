# ADR-015: инструменты на Python и новая структура документации

Дата: 2026-10-22. Статус: принято.

## Контекст

- Восемь скриптов `Tools/*.ps1` работали только в Windows PowerShell 5.1 (`Get-FileHash`, `System.Drawing`,
  Win32-вызовы), pre-commit хук вызывал `powershell.exe`. Проект нельзя было проверить на macOS и Linux,
  а `check-repository.ps1` ещё и требовал ripgrep.
- Документация накопилась в три слоя: ранние учебные документы `docs/00–08`, контракт v1.0
  `docs/agent_execution/01–12` с промптами в корне, журнал `IMPLEMENTATION_STATUS.md` на 1200 строк.
  Агенту со стороны было непонятно, что из этого действует. `AGENTS.md` и `CLAUDE.md` описывали режим
  «реализовать v1.0» и запрещали сеть, которую владелец уже разрешил.

## Варианты инструментов

- **Bash + PowerShell параллельно.** Две копии логики, расходятся.
- **Node или .NET-скрипты.** Хорошо переносимы, но тянут рантайм и пакеты ради сотни строк.
- **Python 3 только со стандартной библиотекой.** Есть почти везде, `subprocess`, `json`, `zipfile`,
  `hashlib` покрывают всё. Выбран.

## Решение

- `Tools/sl.py` — одна точка входа с подкомандами `doctor`, `test`, `check`, `privacy`, `build-android`,
  `install-android`, `measure-android`; модули в `Tools/sectorlab/`. Формат доказательств сохранён:
  поля JSON те же, что у старых скриптов. Хук `.githooks/pre-commit` ищет `python3`/`python`/`py`.
- Убран Windows-only `fit-android-emulator-window.ps1`: эмулятор всё равно не запускает ARM64-сборку.
- Документация: `AGENTS.md` (вход, правила) → `docs/STATUS.md` (состояние, следующий шаг) → `MISSION`,
  `GAME_DESIGN`, `ARCHITECTURE`, `DEVELOPMENT`. Старое — в `docs/history/` без правки содержания,
  ссылки пересчитаны. Доказательства — `docs/evidence/` (было `docs/agent_execution/evidence/`).
- Журнал больше не ведётся: подробности — в коммитах, состояние — в `STATUS.md`, который каждая
  сессия обязана обновлять.
- `sl.py check` проверяет относительные ссылки в живой документации.

## Последствия

- Нужен Python 3.9+; на Windows его надо поставить (Store-заглушка `python` не подходит).
- `unity command` остаётся единственным способом гонять тесты в открытом Editor. Batch-режим (`unity test`)
  требует закрытого Editor и в проекте не проверялся.
- Старые ссылки на `docs/agent_execution/...` в сообщениях коммитов больше не открываются: файлы в
  `docs/history/v1-contract/` и `docs/evidence/`.

## Проверка

22.10.2026: `sl.py test` прогнал сетевые фикстуры и полную регрессию в Editor; `sl.py check` — PASS;
pre-commit на Python отработал на коммитах этой сессии. На macOS и Linux не запускалось.
