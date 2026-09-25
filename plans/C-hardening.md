# Fase C — hardening (refactors pequenos, sem mudança de comportamento)

## Itens (um por commit/bloco, cada um com build+test)

1. **`Process` sem `Dispose`** (`MainViewModel.cs:593,597-598,636-637,641`):
   dar `Dispose()` ao remover de `_watched` e ao final de
   `StopClientAsync`. Vazamento de handle em sessões longas.
2. **`async void SaveAsync`** (`SettingsSheet.xaml.cs:140-155`): virar
   `async Task` aguardado por `async void OnSave`. Hoje o sheet reporta
   "salvo" antes do disco confirmar.
3. **Hook global com catch estreito** (`SyncController.cs:98-129`
   `OnLeftButtonDown`): envolver o corpo com `catch (Exception)` →
   log + return. Exceção de LINQ/estado hoje propagaria p/ o hook.
4. **`ContinueWith` legado** (`MainViewModel.cs:749,758`): trocar por
   `await` com o contexto de sincronização natural.
5. **`catch` vazios sem log**: `MainWindow.xaml.cs:135-138`
   (hotkey inválida) e `PresetEditorControl.xaml.cs:416-419`
   (import JSON descarta o erro real — logar a causa, manter a
   mensagem amigável). `MiniWindow.xaml.cs:117-120` e os catches
   best-effort documentados (`WindowIcon`, `WindowResize`,
   `WindowFocus`, `TrayManager`) ficam como estão.
6. **Fire-and-forget sem observação**: `App.xaml.cs:94`
   (`InitializeAndRegisterAsync` sem try/catch → exceção não
   observada) e `MainWindow.xaml.cs:133` (hotkey sem continuação de
   erro, ao contrário de `GroupViewModel.cs:524-533`). Adicionar
   tratamento/log nos dois pontos.

## Aceite (por item e no fim da fase)

- [ ] `dotnet build` 0/0 + `dotnet test` verde.
- [ ] Nenhuma mudança visível de comportamento (só robustez).
