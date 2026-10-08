# HANDOFF — estado em 2026-10-08, base 733e644 (+lotes UI não commitados)
## Pronto (validado)
- Build solution 0 erros/0 warnings; testes 121/121 (Core 92 + Avalonia 29).
- Lote UI aplicado, **não commitado** (usuário valida por prints/uso antes):
  - Wheel-guard: `ComboBox` fechado não troca seleção, repassa ao
    `ScrollViewer` (`App.axaml.cs`).
  - Ícones cortados: `MinWidth/MinHeight 28` + conteúdo centralizado no
    estilo `Button.icon` (`Styles/Shadcn.axaml`).
  - Rename grupo: proxy `GroupCard.GroupName` + re-raise `MiniGroupTitle`
    (`GroupViewModel.cs`, `GroupWindow.axaml`).
  - Campo ms: proxy `DelayText` (vazio→0, inválido mantém + `InvalidDelay`
    no save) + blink `Opacity` do loop (`MiniWindow.axaml`).
  - Loading: arco geométrico (`Ellipse` + `StrokeDashArray`, gira no lugar
    por construção) dentro do botão Play (`MainWindow.axaml`). Lição do
    erro anterior: glifo em caixa menor que a fonte orbita; sem fonte,
    sem órbita. `StrokeDashArray` exige vírgula (`8,28`), e o Toolkit
    stripa sufixo `Async` (`RefreshStatusCommand` mantido).
  - Sheets: `ClassBox`/`TagBox` com stretch + ícones (`ClassImageConverter`
    aceita nome de arquivo; cards com URI inalterados).
  - Mini compacto: fileira padding 1, sem margem, imagem 12px, fonte 11px,
    ellipsis + tooltip; `ListBoxItem` padding/margin 0.
  - Aba Todas: `RebuildAccounts` reconcilia por id (reutiliza cards,
    preserva spinner/senha, `RefreshIdentity` p/ edição) — fim do lag.
  - Freezes: scan online no pool (`RefreshServerRowsAsync`,
    `FirePreset`, startup), `RebuildAll` reconcilia `GroupCard` por id,
    scan >250 ms sempre loga, `OnClosed` com dispose isolado + log.
  - Save: `PersistPresetAsync` com `Saving…` (resx EN+pt-BR), save no pool,
    pós-save na UI, navegação antecipada no editor.
## Pronto (código, pendente de jogo/Windows)
- Tudo acima: usuário valida por prints/uso (spinner, blink, rename, ms,
  sheets, mini, tabs, freezes, save). Commit só após validação explícita.
- Lote 2026-10-08, **não commitado** (build 0/0, testes 121/121):
  - Olho da senha: `TogglePasswordVisibility` voltou a síncrono; auto-hide
    15 s em fire-and-forget por conta (`_passwordHide`, cancela no hide
    manual) — fim do travamento global (`MainViewModel.cs`).
  - Sentence-case nos `Strings.resx` EN+pt-BR (só 1ª maiúscula; `GroupMode`,
    `TAB`, siglas de tecla e nomes de TAB mantidos).
  - Ícones: 17 pares classe `.png`+`.ico` de `/images` (sigla→chave) +
    `App.ico`/`AppIcon.png` de `icon-transparent.*` (sem mudança de código).
  - Sheet servidor: hint único acima do input, **x64** em negrito
    (`ClientPath` virou formato `{0}` EN+pt-BR; `PathHint` e
    `ServerPathHint` removidos; título do picker usa `string.Format`).
  - Taskbar: `.ico` novos não haviam chegado ao output (17/17 obsoletos);
    `build --no-incremental` recopiou (SHA256 ok). Sem mudança de código.
- Teste 100% do baú (F7/Y, F8/Y) segue pendente.
## Próximo passo
- Validação do usuário → ajustes → commits atômicos por bloco (pedido
  explícito) → passo 2 do sync só se o freeze voltar.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Inalterado. Taskbar pausada; conta-pós-crash ignorado (pedido);
  "loop em muitas contas" era skip de conta fechada (documentar).
## Perguntas abertas
- Nada. Sem commit (pedido explícito pendente).
