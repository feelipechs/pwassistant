# HANDOFF — estado em 2026-10-02, base 3fdb8e4
## Pronto (validado)
- Build 0 erros/0 warnings; testes 89/89 (79 antigos + 10 novos).
- Sender smoke: `--help`→0, arg ruim→3, pid 0→exit 2 + ERROR + RESULT;
  smoke pegou 1 bug real (help com stdin vazio) + endureceu o parser
  (exit 0 sem RESULT = falha). Copy AfterBuild leva o Sender pra junto
  do App (verificado no tree de build).
- Fix pós-falha 13:44 (tudo skip `sender exit=0x8000809A`): faltava o
  `.dll` ao lado do `.exe` (apphost framework-dependent morre sem ele;
  meu smoke anterior rodou da pasta errada). Copy agora leva exe+dll;
  runner verifica os dois (erro nomeando o ausente) e surfamos o stderr
  no erro (a causa passa a se auto-diagnosticar). Re-smoke verdadeiro
  (da pasta do App): exit 2 correto.
- Lock `LockSetForegroundWindow` (build verde, 89/89, sem teste de jogo):
  trava no BeginBatch só-worker + destrava no finally + refcount +
  `[lock] ok win32`. Em validação pela matriz (controle/flood).
- Freeze UI corrigido (build verde, 89/89): scan de processos/hwnd fora
  da thread da UI (snapshot no pool + apply na UI, guarda reentrância);
  open/Activated/timer convergem na pipeline async; `CleanseAllShortcuts`
  em thread STA dedicada no startup; linha `[scan]` verbose se >50 ms.
- Clicks: sonho realizado — SEGUIR/ACEITAR PT executam sem perder
  foco (R2 validada, sem chamada no caminho de click).
- Legado (prime falso) executa teclas em bg sem trocar (repetido).
- Helper 2/2 em fundo sem trocar, nesta máquina, modo negação.
- Descobertas lei nº 5: latch do último input (7× `setfg=1` sem input
  + timeout `INT_MAX`); timeout volátil (`~1 ms` ↔ `INT_MAX`, registro
  200000 intacto); fronteira de processo = mecanismo (daemons
  estacionados + IPC stdin, `BINARY_HASHES`, sem FFI); 3 variantes
  mapeadas (background silenciosa, play-preset/v2 forçadas com CTRL);
  sync de foco one-shot no startup; mouse-move não transfere grant.
## Pronto (código, pendente de build/teste/jogo)
- Worker completo (não commitado): `PwAssistant.Sender` (stdin JSON,
  SFW-sozinho + envios nus + exit 0/1/2/3) + contrato Core
  (`SenderContract`, `WorkerBatch`, DIMs `SleepAsync`/`FlushAsync`) +
  `FocusedInputStrategy` (buffer por conta, flush por AccountAction,
  adaptativo por timeout, restore só no forçado) + micro-after-build
  copy + linha de publish no `release.md` + `architecture.md`.
  FALTA: validação no jogo (dev `dotnet run`, tudo desligado, BUFF foco
  no main: esperado `[mode] worker|legacy-anomaly`, 2/2 sem troca).
## Próximo passo
- Build + testes + smoke; depois validação no jogo (BUFF foco no main:
  esperado `[mode] worker|legacy-anomaly`, 2/2 sem troca) + carga
  sintética 1 client; só então release v1.2.0.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; hold adaptativo (≥2×pumpMs) e daemon estacionado
  (vs spawn por disparo) como evoluções se latência exigir.
## Perguntas abertas
- Quem reescreve o timeout em runtime (protocolo antes/depois do
  launcher/Helper, pendente); commits do lote (aguardando ordem).
