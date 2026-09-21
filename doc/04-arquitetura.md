# Arquitetura — ditto (PW Assistant próprio)

Decisões travadas por prova real (ver `03-pesquisa-e-validacoes.md` item 1 e
`HANDOFF.md`): input sem foco via `PostMessage` com priming de ativação.
Sem driver, sem injeção, sem foco real, sem flicker.

## Solução e projetos (.NET 8, C#)

```
pwassistant.sln
├─ src/PwAssistant.Core    → models, regras, MacroExecutor, SyncService, storage (SEM WinAPI)
├─ src/PwAssistant.WinApi  → TODO P/Invoke isolado (único projeto que referencia user32)
└─ src/PwAssistant.App     → WPF + MVVM (depende de Core e WinApi)
tests/PwAssistant.Core.Tests → xUnit, só lógica sem WinAPI/jogo
```

Regras de dependência (valem para IA e revisões):

1. `Core` NÃO referencia `WinApi` nem `App`. O motor fala via interfaces
   (`IWindowTarget`, `IInputStrategy`) definidas no `Core`.
2. Todo P/Invoke mora em `WinApi`. Nenhum `DllImport` fora dele.
3. `App` não posta mensagem diretamente — só via `Core` + `WinApi`.

Eficiência (requisito do projeto):

- Executor **in-process**: sem exe auxiliar por preset, sem pipe JSON/stdin,
  sem spawn por disparo: chamada direta in-process.
- `PostMessage` é fire-and-forget (sem espera por resposta, exceto variante
  diagnóstica com `SendMessageTimeout`).
- Fan-out sequencial por PID (~1–5 ms/conta) — indistinguível de simultâneo
  para o caso de uso, sem troca de contexto do Windows.

## `IInputStrategy` — contrato (receita validada)

`PostMessageBackgroundStrategy` (implementação padrão e única do v1):

**Tecla** (alvo = HWND top-level `ElementClient Window` resolvido por
`EnumWindows` + `GetWindowThreadProcessId`; o jogo não tem filhas):

1. Prime: `WM_ACTIVATE (WA_ACTIVE, lParam=0)` → 30 ms → `WM_SETFOCUS (0,0)` →
   30 ms → `WM_ACTIVATEAPP (1,0)` → 30 ms.
2. `WM_KEYDOWN (vk, lParam = 1 | scan<<16)`, `scan = MapVirtualKey(vk, MAPVK_VK_TO_VSC)`.
3. 50 ms → `WM_KEYUP (vk, lParam = DOWN | (1<<30) | (1<<31))`.
4. Higiene opcional: `WM_ACTIVATE (WA_INACTIVE,0)` + `WM_ACTIVATEAPP (0,0)`.

**Click de UI** (coordenada de client area):

1. Prime de mouse: `WM_NCHITTEST (0, MAKELPARAM)` → 20 ms →
   `WM_MOUSEMOVE (0, MAKELPARAM)` → 20 ms →
   `WM_SETCURSOR (hwnd, HTCLIENT | (WM_LBUTTONDOWN<<16))` → 20 ms.
2. `WM_LBUTTONDOWN (MK_LBUTTON, MAKELPARAM)` → 50 ms →
   `WM_LBUTTONUP (0, MAKELPARAM)`, `MAKELPARAM = (y<<16) | (x & 0xFFFF)`.

**Limites conhecidos (não tentar furar no v1):**

- Click no **chão 3D** (andar) não passa sem foco — fora do escopo v1.
  Cobertura: teclas de movimento (se o jogo tiver) ou funcionalidade
  "Seguir" do próprio jogo (iniciada por click de UI, que funciona).
- `PostThreadMessage`, `DOWN+CHAR+UP`, hold/repeat: testados, não funcionam
  sem foco — não reimplementar sem nova prova (ver `tools/provas-winapi/`).

Fallback documentado (não implementar até haver prova de necessidade):
`ForegroundSwapSendInputStrategy` (`AttachThreadInput` + `SendInput` + restaura
foco). Driver kernel e injeção estão **descartados em definitivo**.

## `MacroExecutor` — semântica

- Entrada: lista ordenada de `(IWindowTarget, Action)` + `ExecutionMode`.
- `Sequential`: por conta, na ordem — espera `DelayBeforeMs`, executa via
  `IInputStrategy`, aplica `Repeat` (vezes × intervalo) antes da próxima.
- `Simultaneous`: uma task por conta com seu próprio `DelayBeforeMs`.
  Com `PostMessage` (sem foco) é simultâneo de verdade; documentar que com a
  estratégia de fallback seria rajada sequencial rápida.
- Resolução de alvo: `AccountId → Hwnd` no momento do disparo (via processo
  ativo); conta offline/ admisssível é pulada com log, nunca aborta o lote.
- Cancelamento cooperativo por `jobId` (protocolo próprio:
  `execute` / `cancel` / `cancelAll`).

## `SyncService` (Sync Click) — desenho validado no conceito

- Captura: hook global `WH_MOUSE_LL`; click físico dentro da janela em foco
  (master) → `GetClientRect` + `ScreenToClient` → **coordenada relativa**
  (fração da client area — tolera janelas de tamanhos distintos).
- Fan-out: para cada conta sincronizada ≠ master, converte fração →
  client area dela e dispara click de UI (receita C4) via `MacroExecutor`.
- Sem eco: réplicas são mensagens postadas (fora do sistema de input) — o
  hook nunca as vê; sem loop.
- Regras: só botão esquerdo, só dentro de janela de jogo registrada;
  liga/desliga explícito; latência esperada <30 ms para 5 contas.
- Prova pendente: réplica manual A→B à mão (roteiro no `HANDOFF`/sessão);
  automatizar só após essa prova (marco M4).

## Storage

- v1: arquivo JSON único + criptografia de segredos (AES; derivação de chave
  a decidir no M2 — senha mestra vs segredo de máquina via DPAPI).
- Campos de runtime (`ProcessId`, `Hwnd`, `Status`) nunca persistem.
- Backup automático: fora do v1.

## App / UI (WPF)

- **WPF (.NET 8) + MVVM** com `CommunityToolkit.Mvvm` (source generators:
  `[ObservableProperty]`, `[RelayCommand]` — padrão Microsoft, sem boilerplate).
- **DI** com `Microsoft.Extensions.DependencyInjection` (registro por camada;
  `Core` sem dependência de UI).
- **Janelas**: `MainWindow` (shell: sidebar de servers + cards de conta, ver
  telas no `01`), `GroupWindow` (grid só-online + botões de preset),
  diálogos (`ServerDialog`, `AccountDialog`, `PresetEditor`) e overlay
  transparente de captura de click (fullscreen, fecha no primeiro click e
  converte via `ScreenToClient`).
- **Estilo v1**: recursos XAML próprios, tema escuro simples, sem toolkit
  pesado (ex.: MahApps) — reavaliar só se a UI pedir.
- **U2 iconografia + layout (2026-09-21, código pendente de validação):**
  glyphs **Segoe MDL2 Assets** (nativa do Windows): `+` add, lápis edit,
  lixeira delete, engrenagem settings, disquete save, play/stop, copiar,
  olho reveal — CRUD e verbos de ação são ícone com tooltip do resx;
  navegação/rótulos ("Modo Grupo", Fire) continuam texto. `ComboBox` e
  `CheckBox` com templates próprios; `TabToggle`/`TabItem` com underline
  gold. `MainWindow`: sidebar com `+` acima da lista, linha de servidor
  rica (`N contas • M online` + dot, via `ServerRow`), header com strip de
  tabs (`AccountTab` + "Todas" + gerência), cards quadrados em `WrapPanel`
  (`SquareCard`: imagem da classe, pill de status, Play ícone full-width,
  credenciais com botões-ícone). `GroupWindow` em abas gerenciais
  (Membros | Presets | Formações) com `+` de novo grupo no topo e
  lápis/lixeira de grupo (cascata com confirmação).
- **U3 responsividade (2026-09-21, código pendente de validação):** coluna
  direita da `MainWindow` em `Grid` (header/strip/`*` — `ScrollViewer` em
  `StackPanel` nunca scrolla); ações de servidor só no hover/seleção;
  credenciais em caixas `InputBox`; dots de status via `Style` (valor local
  vence trigger — nunca fixar `Fill` direto). `GroupWindow`: membros lado a
  lado (todos | online compacto, sem filtro); formações via botão `...`
  (`Popup`, sem aba); Mini mostra só o nome do preset (loop no toggle).
- **U4 refinamentos (2026-09-21, código pendente de validação):** `ToggleSwitch`
  (CheckBox) para Sync/Focus/Loop/opções; `ScrollBar` fina; Mini sem collapse
  e sem barrinha; tabs por `Tag` (ver `01`/`05`); Save em texto + Cancelar;
  layouts `DockPanel` extremidade-a-extremidade; membros com lista de adição
  (`+` por linha); preset mostra só o nome do personagem, duplicar insere
  abaixo, ícones teclado/mouse e glyphs MDL2 nas linhas.
- **U5 polimento UX (2026-09-21, código pendente de validação):** `ComboBox`
  editável com `PART_EditableTextBox`; Tag travada ao criar conta dentro da
  tab; `RemoveFromTab` sem `ConfigureAwait(false)` antes de tocar coleções
  de UI (crash cross-thread); `ListBoxItem` com template próprio (seleção
  gold, sem azul/cinza do default); `MainWindow` em clusters `DockPanel`;
  status humanizados via resx;   Mini com switches e sem legenda.
  Formações em `GroupFormationsWindow` (clique aplica + fecha); DnD nas
  linhas do preset (sem setas, com linha de inserção) e na ordem dos
  presets (`MovePresetAsync`, helper `InsertionPreview` compartilhado).
- **U7 grupo final (2026-09-21, código pendente de validação):** retag de tabs
  por servidor (sem vazamento cross-server) + trava anti-duplicada;
  `ScrollBar` com botões colapsados e Thumb mínimo (paging na trilha sai);
  `...` como `Button` + `IsMenuOpen`; fantasma de arrasto (`DragAdorner`)
  + highlight `IsDragOver` (gap posicional não se aplica a alvo-card);
  formações separadas (Salvar via prompt, Carregar por linha);
  `Ungrouped` = "Disponíveis"/"Available"; `+` grupo à direita.
  Play/Fire concorrentes (`AllowConcurrentExecutions`), countdown 0 nos
  3 pontos, `IsLaunching` por card (`…` + anti-duplo-launch); Mini com
  mini-cards nome+ícone (sem login, sem DnD).
  `+` grupo na barra superior; popup com `PlacementTarget` + transparência;
  cards 300px em `WrapPanel`; reorder de membros com linha de inserção
  (`InsertionAdorner`, payload `MemberDrag`); ordem do grupo dirige
  numpad/foco; membership 100% DnD (pool vira alvo de drop p/ desagrupar),
  mini-cards só ícone+nome (`MiniCard`).
- **U8 validação UX (2026-09-21, código pendente de validação):** `ComboBox`
  editável com zona de seta clicável (`*` + 28px); `ScrollViewer` no
  `AccountDialog`; cards em `WrapPanel` 300px; `IsDragging` global acende
  todos os cards + contador Enter/Leave contra flicker; `App.ico`
  multi-tamanho (16/32/48/256) gerado do PNG 1920.
- **U6 cards de grupo (2026-09-21, código pendente de validação):** `GroupCard`
  (membros, contagens, `IsActive`, `IsMenuOpen`) + `Pool` (sem grupo) +
  `ActiveCard` (espelha `SelectedGroup`, preservando sync/foco/Mini/hotkeys);
  `RebuildAll` por união; DnD pool→card (`DropAccountOntoGroupAsync`);
  presets em `GroupPresetsWindow` (ativa o grupo ao abrir); formações por
  card;   sem `ConfigureAwait(false)` antes de tocar coleções de UI.
- **U8 validação UX (2026-09-21, código pendente de validação):** um drop =
  um move (`e.Handled` no drop interno; sem isso o move duplicava e dois
  `SaveAsync` concorriam no `.tmp` → `IOException`); `SaveAsync` com
  `SemaphoreSlim` + teste de saves paralelos; pool sem seleção
  (`ItemsControl`); drops com try/catch → `StatusMessage`.
- **U1 Redesign (2026-09-20, código pendente de validação no Windows):**
  cláusula acima acionada — redesign completo mantido em XAML próprio, sem
  novo pacote: `Themes/Tokens.xaml` (paleta dark + gold, radius, espaçamentos,
  fonte) + `Themes/Controls.xaml` (Button Primary/Danger/Ghost/Icon,
  ToggleButton com estado checked dourado, inputs, ListBox, CardBorder,
  SectionTitle/Muted/Status). `App.xaml` só mescla os dicionários (aliases
  `BackgroundBrush/SurfaceBrush/AccentBrush` preservados). `MiniWindow`
  mantém `WS_EX_NOACTIVATE` + `Focusable=False` (1-clique sem roubar foco).
  `<ApplicationIcon>` = `Resources\Classes\guerreiro.ico` (placeholder; ícone
  próprio do app pendente). `images/` raiz é duplicata dos originais do
  usuário (verificado por hash) e está no `.gitignore`; canônico é
  `Resources/Classes`.
- **Idioma**: código/XAML-names em inglês; todo texto visível em pt-BR via
  `.resx` (`Resources.pt-BR`), nunca hardcoded em inglês na tela.
- ViewModels nunca referenciam HWND/PID (falam com `Core` por Ids); `App`
  nunca posta mensagem (só via `Core` + `WinApi`).

## Diagrama de disparo (preset → jogo)
```
Preset (Core) → MacroExecutor resolve Hwnd por AccountId
  → IInputStrategy.PostBackground(hwnd, acao)
    → prime (ACTIVATE/SETFOCUS[/mouse]) → DOWN → [CHAR? não] → UP → [higiene]
  → log por conta (ok/pulada/erro), nunca aborta o lote
```
