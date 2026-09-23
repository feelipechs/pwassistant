# HANDOFF — estado em 2026-09-22, base e4f8524 + trabalho não commitado

## Pronto (validado)

- M1–M6, B1/B1b/B2/B2d/B3/B4/B4-config, B5, taskbar, loop, fechar-X,
  apelido/classe, copiar + auto-clear, mini 1-clique, destaque, modo por
  preset, Fire⇄Parar, Play⇄Stop, gravação livre, Save/descarta.
- Build 0 erros/warnings; testes 71/71; lei nº 2 ok; lei nº 3
  preservada (sem cache de HWND).
- Lote 1 validado pelo usuário (scroll, shift, Mini, editor, play/stop,
  hotkeys, loop, captura sem clique).

## Pronto (código, pendente de jogo/Windows)

- Batch U8 + paridade DnD + moldura + bandeja + fluxo card→Mini +
  semântica do loop + Mini compacto + U13 lote 1 + U14 lote 2
  (foco-hierarquia, perf, descarte, multi-select) — ver `doc/04`
  (U10–U14); **não commitar antes da validação**.

## Próximo passo

- Lote 3 (sheets) após validação do Lote 2 com contas reais in-game
  (lista de testes manuais entregue ao usuário).

## Backlog pós-validação (ponteiro p/ `doc/07-backlog.md`)

- Ver arquivo; B4-config carimbado.

## Perguntas abertas

- Ghost do grupo segue invisível mesmo com coords do evento:
  investigar render.
