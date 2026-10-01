# HANDOFF — estado em 2026-10-01, base d869928
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
- Descoberta lei nº 5 (teste A/B do usuário + logs + RE do Helper +
  resposta do dev): engine ignora PostMessage sem foreground sob carga;
  delay 1 s não resolveu; `fg=1` executa / `fg=0` ignora.
## Pronto (código, pendente de jogo/Windows)
- Fase 4b (`66e5c31`): `FocusedInputStrategy` (BringToFront por conta +
  restore), chave persistida `AppData.FocusedDispatch` default off.
- Receita limpa (`d869928`, com doc `architecture.md`): `TrySetSoft`
  (SetForeground real, sem forçar), envios nus sem prime/higiene,
  restore só se moveu o foco, `[fg] setfg=1/0` no verbose; flag de
  sessão `CleanDispatch` ("RECEITA LIMPA (TESTE)"). Suspeita nº 1 dos
  skips: nossa higiene `WA_INACTIVE`.
- APPTESTE publicado (pasta, exe renomeado, ~66 MB zipado): `d869928`.
## Próximo passo
- Irmão testa no PC fraco (nunca junto do instalado): verbose +
  RECEITA LIMPA, preset de tecla e de click; taskbar piscar = bloqueio
  normal. Resultado decide o default (limpa vs legada vs foco).
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
