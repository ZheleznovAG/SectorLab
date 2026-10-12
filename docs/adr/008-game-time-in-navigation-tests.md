# ADR-008 — время ожидания в навигационных тестах

Status: accepted. Requirements: NFR-002/004/005, FR-005. 12.10.2026.

В первом запуске публичного clone без project Library прошли 63/63 EditMode,
но PlayMode дал 107/112. Все пять FAIL относятся к прежним UnitAttackTests и
UnitMovementTests: они ждали максимум 600 кадров до прибытия/удара. Batchmode
с `-nographics` выдаёт эти кадры гораздо быстрее UI Editor; достаточные игровые
секунды не успевали пройти. Main routes/lifecycle/real matches прошли.

Ожидание этих пяти физических сценариев ограничено 10 игровыми секундами.
Assertions прибытия, фактического движения вдоль partial path, HP и LOS сохранены;
игровой код и скорости не изменены. UnitMovement fixture устанавливает timeScale=1.
Проверки, которым важен порядок нескольких кадров, сохраняют yield по кадрам.

[Первый настоящий FAIL](../agent_execution/evidence/clean-clone-W8-attempt1-playmode.xml)
сохранён. [Focused retry](../agent_execution/evidence/clean-clone-W8-focused-retry.json):
25/25 PASS в том же headless clone. [Полный повторный прогон](../agent_execution/evidence/clean-clone-W8-full-retry.json)
из `acbd9bf`: 112/112 PASS, failed/skipped/inconclusive=0.
Сборка APK не требует повторения из-за изменения только тестовых ожиданий.

Clone отличается от рабочей копии только LF/CRLF в исходных текстах; canonical
Git tree совпадает. На первом import Unity перезаписала generated m_RuntimeSettings
list в UniversalRenderPipelineGlobalSettings.asset, затем tests создали CSV.
Эти фактические изменения clone записаны в отчёте; чистота до import подтверждена,
чистота после работы Unity не выдумывается. Local UPM download cache использован,
project Library отсутствовала; полностью пустой системный package cache не заявлен.
