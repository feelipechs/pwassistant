# HANDOFF — estado em 2026-09-30, base 4e1320c
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
## Pronto (código, pendente de jogo/Windows)
- Lote A (`b9fe208` + `4e1320c`): debounce Fire/hotkey + MiniRows
  estável; logs unificados manual/hotkey/loop com jobId, linha por
  conta (pid/hwnd/alive/vis/min/fg), trace verbose, re-resolve por
  envio; Abrir/Fechar Todas da tab (adaptativo + cancel); toasts
  (overlay + balloon) no topo-esquerdo; cluster de tabs em 2 linhas.
- Release v1.1.0 para o PC do irmão (com os logs novos).
## Próximo passo
- Irmão testa v1.1.0 e devolve logs → Fase 4b (receita alternativa
  atrás de flag, só com dados). Depois: Fase 1 batch save de presets.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
