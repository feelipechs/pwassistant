# Marcos — ditto (tasks executáveis por IA)

Ordem estrita: cada marco só começa com o anterior aceito. Critério de aceite
é binário (passou/não passou); sem "quase pronto". Ao concluir um marco,
atualizar este arquivo (data + resultado) e commitar separadamente.

## M0 — Provas WinAPI (CONCLUÍDO 2026-09-10)

- Bateria PowerShell em `tools/provas-winapi/` (T0–T5, C0/C4, higiene).
- Resultado: teclas e UI-clicks sem foco via priming (T1/C4); chão 3D fora.
- Aceite: F1 toggle 3/3 sem foco em 2 PIDs + UI-click (Sim/Não) sem foco. ✅

## M1 — `PwHelper.WinApi` + console de validação

- Escopo: solução `ditto.sln`; `PwHelper.WinApi` com `EnumWindows→HWND`,
  `MapVirtualKey`, `PostMessageBackgroundStrategy` (receita exata do
  `04-arquitetura.md`: prime T1 / prime C4 / higiene `WA_INACTIVE`); console
  `tools`-like que replica T1 (tecla) e C4 (UI-click) contra 2 PIDs reais.
- Aceite: console monta/desmonta (F1) 3/3 sem foco + click de UI sem foco,
  observados no jogo; nenhum `DllImport` fora do `WinApi` (`grep` comprova).
- Pré-requisito: SDK .NET 8 instalado no Windows.
- **Status (2026-09-16):** código implementado no Linux
  (`src/PwHelper.WinApi`, `src/PwHelper.Probe`); `dotnet build` + `grep`
  verdes. Aceite contra o jogo real **pendente no Windows**:
  `PwHelper.Probe key --pid <pid> --key F1` e
  `PwHelper.Probe click --pid <pid> --x <cx> --y <cy>`.

## M2 — `PwHelper.Core` (models + storage)

- Escopo: models verbatim do `01-modelo-dados-e-telas.md` + `05-regras`;
  storage JSON único com segredos AES (decidir: senha mestra vs DPAPI —
  registrar decisão aqui); testes xUnit de regras puras (sem WinAPI/jogo).
- Aceite: `dotnet test` verde; senha nunca em texto puro (inspeção de arquivo);
  runtime nunca persiste (teste dedicado).
- **Decisão (2026-09-16): DPAPI (`DataProtectionScope.CurrentUser`).**
  Sem senha para digitar; chave guardada pelo Windows, amarrada ao
  usuário/máquina. Contrato atrás de `ISecretProtector` para permitir troca
  futura sem reescrever o storage.
- **Status (2026-09-16):** implementado (`src/PwHelper.Core` + 30 testes
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

## M4 — `SyncService` (Sync Click)

- Pré-requisito: prova manual A→B à mão (roteiro na sessão; scripts atuais).
- Escopo: `WH_MOUSE_LL`, master = foco, coords relativas, fan-out via M3,
  liga/desliga, filtro (só esq., só janela de jogo).
- Aceite: click de UI na master replica na 2ª conta sem foco em <1 s,
  sem eco (monitorar que o hook não re-dispara).
- **Status (2026-09-16):** `SyncService` (Core, testado) + `MouseHook`
  (WH_MOUSE_LL) + `SyncController` (fan-out) implementados. Prova manual A→B
  e aceite **pendentes no Windows**.

## M5 — `PwHelper.App` (WPF/MVVM)

- Escopo: shell (servidores + cards + play via `startbypatcher` com polling
  de HWND), CRUD servidor/conta, Modo Grupo (grid online + botões de preset).
- Aceite: fluxo completo sem foco do app: login 2 contas → preset → Sync,
  tudo observável no jogo; overlay de captura de posição funcional.
- **Status (2026-09-16):** `PwHelper.App` (WPF/MVVM + DI, `MainWindow`,
  `GroupWindow`, diálogos, overlay, `.resx` pt-BR) implementado; compila e
  publica `win-x64` self-contained a partir do Linux. Aceite funcional
  **pendente no Windows**.

## M6 — Endurecimento v1

- Escopo:Countdown/UX de disparo, hotkey global (`RegisterHotKey`), tratamento
  de crash de client (cancela jobs do PID), revisão de AV/Defender do
  self-contained, `README` de uso.
- Aceite: checklist executado contra 5 contas (meta do usuário).
- **Status (2026-09-16):** countdown (`Countdown.RunAsync` + 3 s padrão no
  disparo), hotkey global (`GlobalHotKeyManager`, formato `CTRL+SHIFT+F9`),
  crash-watch (morte do client cancela jobs e marca offline) e `README` de
  uso implementados. Checklist contra 5 contas **pendente no Windows**.

## Convenções de marco

- Um commit por etapa/bloco concluído (não necessariamente um por marco);
  docs atualizados no mesmo commit do código que os afeta (`04/05/06` +
  `HANDOFF` quando comportamento muda).
- Descoberta que contradiz doc: parar, atualizar doc, depois codar.
- Provas contra o jogo real SEMPRE com foco fora do jogo e ação de baixo
  risco primeiro (F1 montaria / UI reversível).
