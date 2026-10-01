# HANDOFF — estado em 2026-10-01, base 66e5c31
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
- Descoberta lei nº 5 (teste A/B do usuário + logs + RE do Helper):
  engine ignora PostMessage sem foreground sob carga; delay 1 s não
  resolveu; `fg=1` executa / `fg=0` ignora.
## Pronto (código, pendente de jogo/Windows)
- Fase 4b (`66e5c31`, com doc `architecture.md`): `FocusedInputStrategy`
  (BringToFront por conta + restore no `finally`), chave global
  `AppData.FocusedDispatch` default off + checkbox no Settings, vale
  para botão/hotkey/loop. Sem SendInput, sem injeção.
- Fix v1.1.1 (`352942c`, empacotada): debounce 150 ms, label bulk,
  morte assíncrona, single-launch, toasts INICIADO/ENCERRADO.
## Próximo passo
- Push + publish v1.2.0 (minor: modo com foco) → irmão valida 10/10
  com a chave ligada no PC fraco. Depois: Fase 1 batch save.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
