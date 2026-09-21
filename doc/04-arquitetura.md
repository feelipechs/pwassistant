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
