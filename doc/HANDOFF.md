# HANDOFF — estado em 2026-09-20, base <pendente de commit>

## Pronto (validado)

- M1–M6, B1/B1b/B2/B2d/B3/B4/B4-config, B5, taskbar (2 botões + ícones,
  classes iguais e diferentes), loop 1 s, fechar-X desliga modos,
  apelido/classe, copiar + auto-clear 30 s, mini 1-clique (NOACTIVATE),
  destaque ~250 ms, modo por preset + `i/n (Role)`, Fire⇄Parar,
  Play⇄Stop, gravação livre de tecla, Save/descarta, mini só-online.
- Build Release 0 erros/warnings em `%TEMP%\ditto-build`; testes 52/52.
- `DllImport` só em `PwAssistant.WinApi` (lei nº 2).

## Pronto (código, pendente de jogo/Windows)

- Publish Release self-contained win-x64 em `%TEMP%\ditto-publish`
  (funciona; Defender: sem alertas, scan dedicado pulado por exclusão).
- `<ApplicationIcon>` = `guerreiro.ico` (placeholder; ícone próprio pendente).
- `images/` raiz confirmado duplicata de `Resources/Classes` (hash igual),
  agora no `.gitignore`.
- U1 Redesign (XAML próprio, sem pacote novo): `Themes/Tokens.xaml` +
  `Themes/Controls.xaml`, 7 janelas reestilizadas; Mini preserva
  NOACTIVATE + `Focusable=False`.
- Fase 3 polimentos: auto-refresh do grid por evento
  (`RefreshIfOnlineChanged`: poll 2 s no `GroupWindow`, ~2 s no `Mini`
  via tick 8/8, rebuild só quando o conjunto online muda); reveal de
  senha no card (Mostrar/Ocultar via resx, auto-hide 15 s, nunca em log);
  drag & drop nas linhas do `PresetEditor` (só de superfícies passivas —
  TextBox/ComboBox/Button não disparam; botões ↑↓ mantidos).
- Roteiro Windows: smoke das 9 janelas em DPI 100/150% + Mini dispara
  sem foco com jogo focado + screenshots; checar grid some sozinho ao
  matar client, reveal esconde em 15 s, arrastar linha reordena.

## Próximo passo

- Validar U1 + Fase 3 no Windows (roteiro acima) e publish com o visual novo.

## Backlog pós-validação (ponteiro p/ `doc/07-backlog.md`)

Ver arquivo; B4-config carimbado; polimentos acima + Play→Stop feito.

## Perguntas abertas

- Ícone próprio do app (não-classe) — desenhar na U1 final ou manter placeholder?
