# CLAUDE.md

Все инструкции для агентов — в [AGENTS.md](AGENTS.md). Текущее состояние и следующий шаг — в
[docs/STATUS.md](docs/STATUS.md). Прочитай оба файла до начала работы.

Коротко для Claude Code:

- Тесты, сборка и проверки — только через `python Tools/sl.py …` (Windows, macOS, Linux); Unity Editor
  должен быть открыт (`unity open .`), команды Editor — `unity command <name>` ([DEVELOPMENT](docs/DEVELOPMENT.md)).
- Коммиты без трейлера `Co-Authored-By`. Push в `origin main` разрешён; force, rebase и reset — нет.
- Не называй проверенными Android-на-устройстве и живой геймплей без реального подтверждения.
- В конце сессии обнови `docs/STATUS.md`.
