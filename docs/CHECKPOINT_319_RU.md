# 319 — свободная ячейка, повтор возврата и fresh replay

**Игровой срез319,23.10.2026:** на установленной **QA.279** завершён существующий возврат316. Полный инвентарь снова безопасно отказал с HUD-сообщением; native объединение двух собственных одинаковых Wood2 освободило ячейку без изменения остальных предметов/квитанций; повторный настоящий E вернул оставшееся:67Wood/32стопки/14customData, оба источника отсутствуют, cleanup сохранён. Новый процесс сохранил результат:10/10, все133 project-файла прежние,32 полных bridge inventory records совпали. Это не сравнение всех native свойств после Load.8 отчётов/39шагов PASS без optional failures. Оба QA штатно закрыты, мост восстановлен после каждого; отложенный185641 также восстановлен. DLL не менялись; .280 не установлен,318 остаётся непроверенным. [Доказательства319](CHECKPOINT_319_RU.md).

Runs: ValheimQA/artifacts/20261023-084136 (PID46068),20261023-084848 (PID42696).
Identity: BW_QA_43d3719a, world1137745799/player376043676, cloud=false/qaSaveRoot=true.
Project:5fb62f0d5c9a4400bbdf9113951844bf; no new issuance, grants, resets or terrain edits.
The first start exceeded20s bridge wait; games_status then confirmed the same live process/connection.
No restart was inferred from the observation timeout. Missing historical prepare.json/games.json
lookups were not authority; deployment.json and live runtime supplied actual state.

Offline compiled317 verifier --saved-credit-evidence checked index0 credited with the exact native
saved inventory/canonical receipt before normal logout. Profile SHA256:
30332E17B4B9B36551BDD0ACDBADC3AFDC338C857D4754AFCFBFE495CF8077F2.
Only own .bwpayout changed during final collection; other132 files unchanged. Fresh replay retained
all133 hashes. Full bridge records were compared from untruncated mcp.jsonl; test-report inventory
strings can be truncated and are not used as complete snapshots. Saved native pair and bridge
record equality are distinct evidence. Player HUD/input acceptance, other real properties/faults,
old-world, secondary collapse and host+remote remain open. All14 goal sections remain required.

Installed QA0.19.279 SHA256:
C7708CC1152E458AB7816C0E0AD4D7691A0B4F90090F838B385248C26C470D9B.
No production/source318 change or new binary build in319. Native helper was the already reviewed
316 assembly pinned279. Source318 has only partial protocol/budget work, not a working peer exchange.
Evidence: artifacts/checkpoint-319/audit.json,8 full reports, saved-final-credit.log;
full MCP logs and shutdown/BepInEx logs in both run folders. Old185641 and both new bridge restore
receipts verified3/3. Game/client processes were exited normally before ownership transfer.
