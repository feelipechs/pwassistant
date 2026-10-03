# HANDOFF — estado em 2026-10-03, base 724d693 (+polimento visual não commitado)
## Pronto (validado)
- Build solution 0 erros/0 warnings; testes 116/116 (Core 92 + Avalonia 24).
- Migração Avalonia + moldura nativa (sessões anteriores); boot estável 25 s.
- Polimento visual desta sessão (pedidos do usuário, tudo validado em build):
  - Editor de presets sem azul: `Classes` primary/outline/icon em Salvar,
    Cancelar, Gravar atalho, Import/Export, adicionar, bulk e linhas.
  - Ícones: revertidos 3 swaps preventivos meus (`E816` captura, `E838`
    browse, `E740` mini-card voltaram ao original); carga de formação
    `E73E→E768` (▶ comprovado); Formations ganhou `Classes="icon"`.
    `E765/E962` mantidos — confirmar por print se algum segue quebrado.
  - Toggles `Classes="Small"` (Mini header + pílulas); Mini compacto
    (fileira padding 2, fire 9px/16px, cantos 6); tabs alinhadas
    (`MinHeight` 28 + `VerticalAlignment` Center nos 3).
  - Fontes presets menores: nomes da lista 11px, pílulas do Mini 9px.
  - Cards: Main 1100px/cards 250 (3×262=786 na área útil, 3 por fileira);
    Grupo 920px/cards 290 (2×300=600, sem sobra à direita).
## Pronto (código, pendente de jogo/Windows)
- Usuário valida por prints: azul fora dos presets, ícones formações +
  captura + adds, toggles pequenos, pílulas compactas, tabs alinhadas,
  fontes, 3 cards, grupo sem sobra.
## Próximo passo
- Prints novos → finos restantes → commit por escopo (pedido explícito) →
  release por `doc/release.md`.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Inalterado. Dívidas: `DialogOwner.Own` no-op; `TrayManager.ShowBalloon`
  no-op; sheets overlays (não dialogs Ursa); toggle/check template Semi
  (só cores); `NoWarn AVLN3001`.
## Perguntas abertas
- Nada. Commit só com pedido explícito (pendente).
