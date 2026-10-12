# ADR-007: вход отряда в узел и ресурсы renderer

Дата: 2026-10-12. Требования: FR-004/007/014, NFR-003/004, W8.

Финальная регрессия после W7 выявила остановку третьего бойца при групповом
приказе к B. Пути полные, но два ранних бойца останавливались на расстоянии
1,35 от центра и создавали локальный затор для следующего NavMeshAgent.
Первый stress test отправлял все шесть без боя A→B→C. Проверка каждого отряда
из трёх от его настоящих баз тоже воспроизвела ошибку:
[шесть агентов](../agent_execution/evidence/W8-crowded-route-diagnostic.json),
[три](../agent_execution/evidence/W8-squad-route-diagnostic.json).
В Enemy squad Ranger оставался на complete path с remaining 2,05 и moving=true
через 12 с; Tank/Scout уже остановились у края. Deadline не увеличивался.

Общее RelayPoint.GetArrivalDistance теперь оставляет 0,6 м от края, вместо
0,15 м. Это соответствует радиусу самого крупного существующего агента
0,575 м. Первые бойцы проходят глубже, освобождая подход следующему.
AI и UnitCommander используют одно правило; роли, урон, здоровье, capture
radius и скорость не менялись. Новых систем формаций/навигации не введено.

Тест MainArena проверяет каждую команду из трёх независимо, от реальных баз:
18 полных путей и фактических вхождений. Между проверками Warp только возвращает
fixture в исходные базы, не заменяет проверяемое движение к узлу.
Геометрия спавна/зеркальность/одинаковые stats проверяются до fixture resets.
**Focused 1/1 PASS** после исправления
([JSON](../agent_execution/evidence/tests-W8-arrival-fix-playmode.json)).
Это не доказательство одновременного размещения шести остановленных агентов;
ошибка stress test и ошибка обычной тройки сохранены, не выдаются за PASS.
Полная регрессия после исправления: **112/112 PlayMode PASS**, без skipped,
failed/inconclusive ([JSON](../agent_execution/evidence/tests-W8-retry-playmode.json)).
Десять real Main restarts и все отрицательные сценарии ресурсов проходят.
Пять активных партий сохранены: hold-then-flank Victory 45:15,5 за 58,5 с,
split-pressure Defeat 43,1:45; три остальных также Defeat
([CSV](../agent_execution/evidence/W8-retry-sustained-plans.csv)).
Проверка имеет настоящий набор inputHashes: новый код, renderer settings и
version; sourceHead относится к commit до этих проверенных изменений.

Отдельно проверен установленный URP 17.6.0: UniversalRenderer создаёт
PostProcess при data.postProcessData != null независимо от выключенного
renderPostProcessing камеры Main. Прежний Android APK сообщал об unsupported
неиспользуемых DOF/Panini/MotionBlur shaders. В существующих Mobile/PC renderer
убрана только postProcessData reference; Main post-processing уже был выключен,
остальные renderer/features/materials сохранены. [Настоящий Game View](../agent_execution/evidence/W8-renderer.png)
1280×720 просмотрен; поле, тени, бойцы и UI отображаются. 63/63 EditMode PASS
([JSON](../agent_execution/evidence/tests-W8-final-editmode.json)).

Player bundleVersion 1.0.0 — номер локального кандидата, не утверждение о
Release ready. Unity 6000.6.4f1 не обновлялась; bundle code 1 и штатная
локальная debug-подпись сохранены. APK не публикуется.
