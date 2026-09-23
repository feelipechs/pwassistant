# UI Audit — estado em 2026-09-21 (Fase 0)

Levantamento para o plano `ui-theme-and-layout-plan.md`. Premissas verificadas
no código; divergências registradas aqui (o código prevalece).

## Premissas (verificadas)

- WPF .NET 8 (`UseWPF`, `net8.0-windows`); zero libs de UI (só
  `CommunityToolkit.Mvvm` + `DependencyInjection`); moldura custom
  (`WindowChrome` + `TitleBar` + estilo `ThemedWindow` nas 11 janelas;
  overlay de captura segue `None` nativo).
- Sem `app.manifest` — PerMonitorV2 é o default do .NET 8 (confirmar nos
  screenshots 100–200%).
- Ícones: Segoe MDL2 Assets (checados no cmap da fonte); fonte Segoe UI.
- Repo usa `doc/`, não `docs/` — este arquivo mora em `doc/ui-audit.md`.

## Números (14 arquivos XAML em `src`)

- Cores hardcoded: **0 nas telas** — as 15 hex vivem só em
  `Themes/Tokens.xaml` (1 ocorrência cada).
- `StaticResource`: 288 · `DynamicResource`: **0** (migração pendente, F2).
- `ControlTemplate`: só em `Themes/Controls.xaml`.
- `Canvas`/`SizeToContent`: 0. `ScrollViewer`: 11 usos.
- `MinWidth`/`MinHeight`/`UseLayoutRounding`/`SnapsToDevicePixels`: só
  `MinHeight` em estilos + `MinWidth` pontuais; **nenhum**
  `UseLayoutRounding`/`SnapsToDevicePixels` (adicionar nas janelas, F3).
- `FontSize` usados: 11 (11×), 12 (11×), 13 (3×), 14 (3×), 16 (4×) →
  mapear para `Font.Size.Sm/Md/Lg` (13 some).
- `Width`/`Height` fixos: ícones (24/8), janelas (Main 1020×660 mín
  800×480; Grupo 980×640; Mini 300×320; dialogs), alturas de lista
  (240/140/120/110/84), cards (212/300). Exceções documentadas abaixo.

## Telas × controles

| Tela | Controles principais |
|---|---|
| `MainWindow` | sidebar `ListBox` (ServerRow), tab strip (`RadioButton`+`TabToggle`), cards `WrapPanel` (`SquareCard`, Play ícone, `InputBox`), header `Grid` |
| `GroupWindow` | pool (coluna = alvo de drop), cards `WrapPanel` (`GroupCard`), `...` `Popup`, DnD (ghost `Canvas`/`GhostLayer`, highlight pelo remetente, `LiveMove`) |
| `GroupPresetsWindow` / `GroupFormationsWindow` | listas + DnD (presets) |
| `MiniWindow` | `ComboBox`, lista compacta, pílulas preset + switch, `NOACTIVATE` (não mexer) |
| `PresetEditor` | linhas (`ComboBox`×4, `TextBox`, botões-ícone) + DnD |
| `AccountDialog` / `ServerDialog` / `SettingsWindow` / `TextPromptDialog` | `TextBox`, `PasswordBox`, `ComboBox` (editável na Tag), `ToggleSwitch` |
| `ClickCaptureOverlay` | overlay fullscreen (fora do tema) |

## Divergências do plano

1. Modo Grupo **não tem abas** Membros/Presets/Formações — é cards + pool +
   janelinhas. Fluxo fora de escopo; F4 troca só tokens lá. Ordem de
   migração vira: Mini → Main → dialogs → editores → Grupo (tokens).
2. Deleção é vermelha sempre (`DangerButton`); vira cinza + hover destrutivo (F2).
3. Seleção é fill gold; vira `RaisedHover` + barra lateral 2px `Primary` (F2).
4. Confirms são `MessageBox` — **mantidos** (fluxo validado; plano proíbe
   mudar comportamento).
5. `Size.Control` 36 × Mini compacto (28–32): Mini entra como exceção
   documentada, não cresce.

## Exceções de medida (documentadas, não normalizar)

- `Size.Control` = 24 global (densidade escolhida pelo autor contra a
  diretriz 32–36 de alvo de toque).
- Fonte: `Cascadia Code, Consolas` global (mono em toda a UI; ícones em MDL2).
- Mini compacto: paddings 4–8, imagem 20, lista 84px, pills ~26px.
- Ícones/avatares: 16/20/24/28/32/40/48/56 (`Size.Icon.*` + exceções pontuais).
- Janelas e alturas de lista acima; cards 212/300; pills e switches.

## Mapa de chaves legado → shadcn (migração F4)

| Legado | Novo | Nota |
|---|---|---|
| `BackgroundColor/Brush #14161A` | `Color/Brush.Background #0A0A0A` | fundo |
| `SurfaceColor/Brush #1C1F26` | `Color/Brush.Card #171717` | superfície |
| `CardColor/Brush #22262F` | `Color/Brush.Raised #262626` | cards/itens |
| `BorderColor/Brush #2F3542` | `Brush.Input #737373` (controles) / `Brush.Border #262626` (divisórias) | ver regra: decorativa nunca em controle |
| `AccentColor/Brush #D9A441` (+Hover/Pressed) | `Brush.Primary #FAFAFA` (+`PrimaryHover`) | **troca gold→branco: é o novo tema** |
| `AccentInk #1A1405` | `Brush.PrimaryForeground #171717` | texto sobre Primary |
| `TextPrimary #F0F2F5` | `Brush.Foreground #FAFAFA` | |
| `TextSecondary/Muted #A8B0BC/#6E7681` | `Brush.MutedForeground #A3A3A3` | |
| `Danger #E5534B` (texto) | `Brush.Destructive #F87171` | DangerHover some |
| (novo) fundos destrutivos | `Brush.DestructiveSolid #C62828` + `OnDestructiveSolid` | confirmações |
| `Success #3FB950` | `Brush.Success #4ADE80` | |
| `Warning #D29922` | `Brush.Warning #FBBF24` | |
| `AppFont` | `Font.Family` | |
| Radius 4/6/8, Spaces | `Radius.Sm/Md`, `Space.*`, `Inset.*` | radius 8 vira exceção ou some |
