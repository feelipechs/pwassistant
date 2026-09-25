# Fase D — desacoplamento VM (`IDialogService`)

## Problema

`MainViewModel` chama `MainWindow` direto em 9 pontos
(`MainViewModel.cs:329,346,359,384,418,442,468,664,693` via
`Application.Current.MainWindow is not MainWindow`) e
`GroupViewModel` chama `GroupWindow`/`MiniWindow` em ~10 pontos
(`OfType<GroupWindow>()`, `Hide()/Show()/Activate()`,
`AskPromptAsync/AskConfirmAsync`; fábrica `Func<MiniWindow>` em
`:129,190-198`). VMs ficam untestáveis e ferem o layering
documentado. `MainViewModel.cs:550,565,571,620,646` também toca
`account.WindowHandle` (`IntPtr`) direto, contra
`doc/04-arquitetura.md:319`.

## Mudança (refactor mecânico, zero comportamento novo)

1. Nova interface na camada App (ex.: `Services/IDialogService.cs`):
   `AskConfirmAsync`, `AskPromptAsync`, `AskServerAsync`,
   `AskAccountAsync`, `AskSettingsAsync`(se houver), `ShowFormations`,
   show/hide de Grupo e Mini — espelhando os métodos `Ask*` que hoje
   moram em `MainWindow.xaml.cs` / `GroupWindow.xaml.cs`.
2. Implementação roteando para a janela dona atual (mesma lógica de
   hoje, só realocada); registrar no DI em `App.xaml.cs`.
3. Migrar `MainViewModel` (um commit) e `GroupViewModel` (outro
   commit): trocar chamadas diretas por `_dialogs.*`. Remover
   `Func<MiniWindow>` em favor do serviço.
4. `WindowHandle` na VM: trocar por serviço de marcação via
   `AccountId` (`AppState.ResolveTarget()` / `IClientMarker`) — VM
   nunca referencia HWND/PID.
5. `DialogOwner.cs:12-19` já aponta nessa direção — reutilizar.

## Risco (conhecido e mitigado)

~20 call sites: esquecer um = quebra de build (detectável na hora);
trocar a janela dona = regressão visual (detectável na regressão
manual abaixo). Sem mudança de threading/semântica.

## Aceite

- [ ] `dotnet build` 0/0 + `dotnet test` verde por commit.
- [ ] Regressão manual no Windows: abrir cada sheet (server, account,
      tab, confirm, prompt, settings, formations, presets) uma vez em
      Main e Grupo; fluxo card→Mini intacto.
