# HANDOFF — estado em 2026-10-01, base 31fb346
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
- Descoberta lei nº 5 (teste A/B do usuário + logs + RE do Helper +
  resposta do dev + teste APPTESTE): engine ignora PostMessage sem
  foreground sob carga; delay 1 s não resolveu. Grant de foreground:
  nosso processo interativo (clique no Disparar) tem direito → chamada
  é concedida e troca visível; sender sem input é negado (flash-only).
  Chamada negada = sem efeito observável; parte operativa = mensagens
  sem `WA_INACTIVE`.
## Pronto (código, pendente de jogo/Windows)
- Fase 4b (`66e5c31`): `FocusedInputStrategy` (BringToFront por conta +
  restore), chave persistida `AppData.FocusedDispatch` default off.
- Receita limpa (`d869928` + fix `31fb346`, com doc `architecture.md`):
  envios nus, **zero chamada de foreground** (TrySetSoft removido),
  restore só no modo foco, `[fg]` só-leitura no verbose; flag de
  sessão `CleanDispatch` ("RECEITA LIMPA (TESTE)"). Suspeita nº 1 dos
  skips: nossa higiene `WA_INACTIVE` (removida do caminho limpo).
- APPTESTE republicado (pasta, `APPTESTE.exe`, zip ~66 MB): `31fb346`.
## Próximo passo
- Irmão retesta no PC fraco (nunca junto do instalado): verbose +
  RECEITA LIMPA, tecla e click; esperado zero troca de foco, main
  estável nos `[fg]`. Resultado decide o default (limpa vs foco).
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
