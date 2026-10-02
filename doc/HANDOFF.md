# HANDOFF — estado em 2026-10-02, base 777a63b (+docs não commitados)
## Pronto (validado)
- Build 0 erros/0 warnings; testes 89/89. Commits C1–C10 verdes por construção.
- Worker 9 contas em fundo sem troca (negado `setfg=0` + flash + `fired=9 skipped=0`).
- Lock `LockSetForegroundWindow` com refcount + `[lock] ok win32` (falha em degradado).
- Freeze UI: scan fora da thread da UI (snapshot no pool + apply na UI); open/Activated/timer convergem; `CleanseAllShortcuts` em STA dedicada; `[scan]` verbose >50 ms.
- Settings enxutas: numpad, shift-tap, log (experimentais + forced removidos c/ código).
- Sender smoke + parser harden; publish validado (App 147 MB + Sender 64 MB); vpk pack 1.2.0 OK (delta 29 MB); Releases limpos (~270 MB).
- Incidentes fechados: `.dll` ausente (LocateExe agora só-exe + stderr no erro); App.xaml.cs stale no C3 (pego em revisão, C10 corrige, builds validam a árvore exata).
- Clicks R2 sem foco; legado prime-falso executa sem trocar; Helper 2/2 bg aqui.
- Descobertas lei nº 5: latch do último input; timeout volátil (`~1 ms`↔`INT_MAX`, registro intacto); fronteira de processo = mecanismo; 3 variantes Helper; sync one-shot; mouse-move não transfere; mini inocente + B4 default-ON é confound.
## Pronto (código, pendente de jogo)
- Nada pendente de código. Falta: validação final no jogo no build com settings limpas + release v1.2.0 (NOVA release, nunca editar a antiga; repack após estes commits — artefatos 1.2.0 atuais são pré-fix).
## Próximo passo
- `git push origin main` (3 commits) → republish → repack limpo → tag v1.2.0 → GitHub release (4 anexos + texto pronto).
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Daemon estacionado (vs spawn) se latência doer; hold adaptativo; B4 default-ON em revisão.
## Perguntas abertas
- Quem reescreve o timeout em runtime (protocolo pendente, não bloqueia).
