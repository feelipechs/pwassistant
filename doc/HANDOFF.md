# HANDOFF — estado em 2026-09-28, base 6388cea
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 1 UI (`d709f79`): warning, status dots, clamp taskbar, wheel combo,
  blink loop, tray (tudo verificado isolado).
- Lote 2 fluxos: sync-por-conta no Mini + sync botão direito (`8dd8dbe`),
  refresh de membros do editor (`c31d6df`), remoção do Simultaneous com
  migração (`6388cea`).
## Pronto (código, pendente de jogo/Windows)
- Validação no jogo: Lote 1 visual, duplicate→editor, sync checkboxes,
  sync direito, presets Simultaneous migrados.
- Fase 3: observabilidade primeiro (logs jobId/HWND/cancel), depois fixes.
## Próximo passo
- Push (terminal do usuário) + testes no jogo + mandar log se falhar.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
