# Marcos — PwAssistant (tasks executáveis por IA)

Ordem estrita: cada marco só começa com o anterior aceito. Critério de aceite
é binário (passou/não passou); sem "quase pronto". Ao concluir um marco,
atualizar este arquivo (data + resultado) e commitar separadamente.

## M0 — Provas WinAPI (CONCLUÍDO 2026-09-10)

- Bateria PowerShell em `tools/provas-winapi/` (T0–T5, C0/C4, higiene).
- Resultado: teclas e UI-clicks sem foco via priming (T1/C4); chão 3D fora.
- Aceite: F1 toggle 3/3 sem foco em 2 PIDs + UI-click (Sim/Não) sem foco. ✅

## M1 — `PwAssistant.WinApi` + console de validação

- Escopo: solução `pwassistant.sln`; `PwAssistant.WinApi` com `EnumWindows→HWND`,
  `MapVirtualKey`, `PostMessageBackgroundStrategy` (receita exata do
  `04-arquitetura.md`: prime T1 / prime C4 / higiene `WA_INACTIVE`); console
  `tools`-like que replica T1 (tecla) e C4 (UI-click) contra 2 PIDs reais.
- Aceite: console monta/desmonta (F1) 3/3 sem foco + click de UI sem foco,
  observados no jogo; nenhum `DllImport` fora do `WinApi` (`grep` comprova).
- Pré-requisito: SDK .NET 8 instalado no Windows.
- **Status (2026-09-16):** código implementado no Linux
  (`src/PwAssistant.WinApi`, `src/PwAssistant.Probe`); `dotnet build` + `grep`
  verdes. Aceite contra o jogo real **pendente no Windows**:
  `PwAssistant.Probe key --pid <pid> --key F1` e
  `PwAssistant.Probe click --pid <pid> --x <cx> --y <cy>`.
- **Aceite (2026-09-17, Windows):** ✅ `Probe key --pid 2992/7688 --key F1`
  (montaria) 3/3 em cada PID sem foco + `Probe click --x 1343 --y 305`
  (fechar inventário) 1/1 em cada PID sem foco.

## M2 — `PwAssistant.Core` (models + storage)

- Escopo: models verbatim do `01-modelo-dados-e-telas.md` + `05-regras`;
  storage JSON único com segredos AES (decidir: senha mestra vs DPAPI —
  registrar decisão aqui); testes xUnit de regras puras (sem WinAPI/jogo).
- Aceite: `dotnet test` verde; senha nunca em texto puro (inspeção de arquivo);
  runtime nunca persiste (teste dedicado).
- **Decisão (2026-09-16): DPAPI (`DataProtectionScope.CurrentUser`).**
  Sem senha para digitar; chave guardada pelo Windows, amarrada ao
  usuário/máquina. Contrato atrás de `ISecretProtector` para permitir troca
  futura sem reescrever o storage.
- **Status (2026-09-16):** implementado (`src/PwAssistant.Core` + 30 testes
  xUnit verdes no Linux).

## M3 — `MacroExecutor` + presets

- Escopo: executor sequencial/simulado por PID com `DelayBeforeMs`,
  `Repeat`, skip-de-offline-com-log, cancel por `jobId`; presets
  F1–F8 (auto) e custom (tecla + click relativo).
- Aceite: preset de 2 contas (F1 + UI-click) executa sem foco nas duas,
  observado no jogo; conta offline pulada sem abortar.
- **Status (2026-09-16):** `MacroExecutor` + `MacroJobRunner` + `PresetEditor`
  implementados; lógica coberta por testes com estratégia fake. Aceite contra
  o jogo **pendente no Windows**.
- **Aceite (2026-09-17, Windows):** ✅ `Probe preset --pid 2992 --pid 7688
  --x 1343 --y 305` (`Simultaneous`): F1 na conta 1 + UI-click (fechar
  inventário) na conta 2 no mesmo disparo sem foco; conta fantasma
  `SKIPPED (offline)`, lote não abortado (`canceled=False`).

## M4 — `SyncService` (Sync Click)

- Pré-requisito: prova manual A→B à mão (roteiro na sessão; scripts atuais).
- Escopo: `WH_MOUSE_LL`, master = foco, coords relativas, fan-out via M3,
  liga/desliga, filtro (só esq., só janela de jogo).
- Aceite: click de UI na master replica na 2ª conta sem foco em <1 s,
  sem eco (monitorar que o hook não re-dispara).
- **Status (2026-09-16):** `SyncService` (Core, testado) + `MouseHook`
  (WH_MOUSE_LL) + `SyncController` (fan-out) implementados. Prova manual A→B
  e aceite **pendentes no Windows**.
- **Aceite (2026-09-17, Windows):** ✅ `Probe sync --pid 2992 --pid 7688`
  bidirecional: físico na 2992 → réplica na 7688 `(1346,303) OK`;
  físico na 7688 → réplica na 2992 `(1340,307) OK`. Sem eco (1 réplica
  por clique). Harness novo: `probe sync` (hook + `MessageLoop` no
  `WinApi`, fan-out no Probe, sem principal fixo).

## M5 — `PwAssistant.App` (WPF/MVVM)

- Escopo: shell (servidores + cards + play via `startbypatcher` com polling
  de HWND), CRUD servidor/conta, Modo Grupo (grid online + botões de preset).
- Aceite: fluxo completo sem foco do app: login 2 contas → preset → Sync,
  tudo observável no jogo; overlay de captura de posição funcional.
- **Status (2026-09-16):** `PwAssistant.App` (WPF/MVVM + DI, `MainWindow`,
  `GroupWindow`, diálogos, overlay, `.resx` pt-BR) implementado; compila e
  publica `win-x64` self-contained a partir do Linux. Aceite funcional
  **pendente no Windows**.
- **Aceite (2026-09-17, Windows):** ✅ fluxo completo pelo App com 2 contas
  (`flp-wb`, `flp-wf`): Play via `startbypatcher` (após fix `WorkingDirectory`,
  sem erro `configs.pck`) → Fire do preset seedado `F1 x2` (`fired=2 skipped=0`,
  countdown 3 s) → Sync via checkbox bidirecional com janelas sobrepostas
  (após fix conversão-única + master por `WindowFromPoint`, sem eco) →
  overlay (`Testar captura`, diagnóstico temporário até o B2):
  `captured=(0.98,0.40)` estável em 2 cliques no X do inventário.
  Corrigidos no caminho: lista de servidores invisível (faltava `ItemsSource`),
  mesmo defeito em `GroupList`/`OnlineList`, + cabeçalho "Contas — {0}" e
  destaque de seleção.

## M6 — Endurecimento v1

- Escopo:Countdown/UX de disparo, hotkey global (`RegisterHotKey`), tratamento
  de crash de client (cancela jobs do PID), revisão de AV/Defender do
  self-contained, `README` de uso.
- Aceite: checklist executado contra 5 contas (meta do usuário).
- **Status (2026-09-16):** countdown (`Countdown.RunAsync` + 3 s padrão no
  disparo), hotkey global (`GlobalHotKeyManager`, formato `CTRL+SHIFT+F9`),
  crash-watch (morte do client cancela jobs e marca offline) e `README` de
  uso implementados. Checklist contra 5 contas **pendente no Windows**.
- **Parcial (2026-09-17, Windows):** crash-watch validado (card nasce Online
  após Play — após fix `NotifyPropertyChangedFor(StatusText)` — e volta a
  Offline ao fechar o client na mão, App responsivo). Hotkey `CTRL+SHIFT+F9`
  gravada no preset `F1 x2` via seed (sem UI); disparo com jogo em foco
  pendente de teste. Checklist proposto como adaptado a 2 contas.
- **Hotkey (2026-09-17, Windows):** ✅ `CTRL+SHIFT+F9` com jogo em foco
  dispara `F1 x2` nas 2 contas (countdown 3 s). Causa do 1º failure:
  condição de corrida — registro ocorria no `SourceInitialized`, antes do
  `LoadAsync`; corrigido (registro pós-carga via
  `RegisterPresetHotkeys`, idempotente).
- **Aceite final (2026-09-17, Windows):** ✅ checklist adaptado a 2 contas
  (usuário tem 2; decisão: o fan-out é por conta com o mesmo caminho de
  código, então 2 provam o mecanismo — 5 seria repetição, não cobertura
  nova). Cobertura executada: login ×2 via Play, Fire `fired=2`, Sync
  bidirecional, hotkey com jogo em foco, crash-watch Online→Offline,
  overlay `captured=(0.98,0.40)` estável, `README` revisado. AV/Defender:
  sem alertas observados nos binários Debug usados na sessão; publish
  Release self-contained segue pendente de teste dedicado.
- **Publish Release (2026-09-22, Windows):** ✅ `dotnet publish
  src/PwAssistant.App -c Release -r win-x64 --self-contained
  /p:PublishSingleFile=true -o ./publish` → `PwAssistant.App.exe`
  (~147 MB) + `Resources/Classes/*.ico` ao lado; `publish/` no
  `.gitignore`. AV/Defender do bundle segue pendente.
- **Finalização código (2026-09-25, sem jogo):** U15–U20 em código
  (sheets, fusão presets, in-game round, UI round, segredos no launch,
  desacoplamento VM, updater Velopack) — build 0/0, testes 78/78.
  Validação in-game pendente.
- **Instalador 1.0.0 (2026-09-25, Windows):** ✅ `vpk pack`
  (`packId Ditto.PwAssistant`) → `Setup.exe` instala per-user sem
  perguntas e abre pelo Menu Iniciar. Sem assinatura (SmartScreen
  esperado). Instalação local de teste; distribuição ainda não feita.
- **Rename (2026-09-25):** repo `ditto` → `pwassistant`, `packId` →
  `PwAssistant`, `FeedUrl` + remote atualizados. Instalação 1.0.0
  antiga desinstalada antes; repack pendente.

## Convenções de marco

- Um commit por etapa/bloco concluído (não necessariamente um por marco);
  docs atualizados no mesmo commit do código que os afeta (`04/05/06` +
  `HANDOFF` quando comportamento muda).
- Descoberta que contradiz doc: parar, atualizar doc, depois codar.
- Provas contra o jogo real SEMPRE com foco fora do jogo e ação de baixo
  risco primeiro (F1 montaria / UI reversível).
