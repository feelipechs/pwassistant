# HANDOFF — estado em 2026-09-17, base be09b9c

## Pronto (validado)

- M1 (PIDs 2992/7688): `Probe key F1` 3/3 por PID + `Probe click (1343,305)`
  1/1 por PID, sem foco.
- M3: `Probe preset` (`Simultaneous`, F1 + click) nas 2 contas sem foco;
  fantasma `SKIPPED (offline)`, `canceled=False`.
- M4: `Probe sync` bidirecional `(1346,303)`/`(1340,307) OK`, sem eco.
- M5 (contas `flp-wb`/`flp-wf`): Play via `startbypatcher` (fix
  `WorkingDirectory`) → Fire `F1 x2` (`fired=2`) → Sync bidirecional com
  sobreposição (fix conversão-única + Z-order) → overlay
  `captured=(0.98,0.40)` estável.
- M6: crash-watch Online→Offline ✅, countdown ✅, hotkey `CTRL+SHIFT+F9`
  com jogo em foco ✅ (após fix da corrida de inicialização), `README`
  revisado ✅, checklist final adaptado a 2 contas ✅.
- Fixes de UI: `ItemsSource` em `ServerList`/`GroupList`/`OnlineList`,
  cabeçalho "Contas — {0}", seleção com `AccentBrush`, `StatusText`
  reativo (`NotifyPropertyChangedFor`).
- `dotnet build` 0 erros/warnings; `dotnet test` 31/31 (runtime .NET 8
  lado a lado).
- `DllImport` só em `PwHelper.WinApi` (lei nº 2).

## Pronto (código, pendente de jogo/Windows)

- M2 sem pendência de jogo (DPAPI `CurrentUser` + 31 testes).
- Publish Release self-contained + teste AV/Defender dedicado (sem
  alertas nos binários Debug da sessão).

## Próximo passo

Backlog pós-v1 (`doc/07-backlog.md`): B1 membros do grupo, B2 CRUD de
preset (inclui campo de hotkey editável + remover botão diagnóstico
`Testar captura`), B3 mini-mode. Ou encerrar por aqui — v1 validado.

## Backlog pós-validação (ponteiro p/ `doc/07-backlog.md`)

Ver acima. Polimentos anotados: Play→Stop/indicador Online, auto-foco
da janela-alvo na captura, taskbar (Win: nunca combinar — sem código).

## Perguntas abertas

- Nenhuma bloqueante.
