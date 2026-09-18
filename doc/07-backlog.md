# Backlog pós-v1 — ditto

Itens fora dos marcos M1–M6, triados em 2026-09-16 a partir do fluxo
desejado (estilo PW Helper). Nenhum exige mudança de modelo (`01`) nem de
estratégia de input (`04`): ordem = ordem da lista `Actions`, tempo = 
`DelayBeforeMs`, duplicar = clonar objeto, presets por grupo = `GroupId`.
Tudo é camada App (B1b adiciona a entidade `Formation`, nova e aditiva,
sem migração). Só implementar após o aceite M1 no Windows.
(M1–M6 aceitos em 2026-09-17; backlog liberado.)

Isolamento por grupo (aceite transversal, zero código): `Preset.GroupId`
amarra cada preset a um grupo — presets de um grupo nunca afetam outro.
Validar em cada item abaixo.

## B1 — Gerência de membros do grupo

- Adicionar/remover contas de um grupo; grid continua exibindo só online.
- Grid reativo: some ao fechar o client (refresh ao ativar a janela +
  ao trocar de grupo); fantasma até refresh é polimento pós-v1.
- Aceite: montar grupo de 3 contas, 1 offline some do grid sem erro,
  volta ao logar (refresh).
- **Aceite (2026-09-17, Windows):** ✅ add/remover com online e offline,
  persistência reabrindo o App, grid reativo ao reativar a janela.
  Contexto: sair do jogo é pelo menu interno (X minimiza p/ tray) —
  crash-watch observa morte do processo, independente do como. Crash
  encontrado no caminho: continuação pós-`await` fora do dispatcher
  (`NotSupportedException` no `Rebuild`) — corrigido ( awaits da camada
  App que tocam coleção de UI ficam no contexto UI). Auto-refresh
  orientado a evento: polimento pós-v1 (manual iguala o PW Helper).

## B1b — Formações (salvar/carregar PT)

- Formação = retrato nomeado e ordenado de membros (`Formation {Id, Name,
  AccountIds[]}`, lista nova em `AppData`, aditivo sem migração). Sem
  presets (já persistem por grupo).
- Salvar a partir do grupo atual; carregar aplica num grupo (Guid
  desconhecido pulado com log, nunca aborta). A ordem define o numpad
  do B4.
- Aceite: salvar PT de 2 contas, esvaziar o grupo, carregar de volta
  na mesma ordem.
- **Aceite (2026-09-17, Windows):** ✅ salvar (`PT principal`), esvaziar,
  persistência reabrindo o App, carregar de volta na mesma ordem
  (`members=2`).

## B2 — CRUD de preset completo

- Criar/editar/excluir preset; adicionar/remover ação por conta
  (tecla + click com captura via overlay); tempo em ms; duplicar preset;
  reordenar ações/personagens (lista simples primeiro).
- Campo de hotkey editável no preset (formato `CTRL+SHIFT+F9`, validado pelo
  `ParseHotkey`; registrar/desregistrar ao salvar) — pedido do usuário em
  2026-09-17 (hoje só via seed no JSON).
- Drag & drop na ordem: só se a lista simples incomodar em uso real.
- Aceite: duplicar preset de 2 contas, ajustar delays, disparar sem foco
  nas duas; linha inválida (click sem posição) bloqueia o save.
- **Parcial (2026-09-17, Windows):** ✅ criar preset, linhas editáveis no
  lugar (conta/tipo/tecla/captura/delay), duplicar/excluir/↑↓ linha,
  validação por linha no save, último servidor lembrado,
  duplicar preset (sem hotkey), renomear, hotkey editável com gravação
  por tecla e re-registro live. Bug no caminho: combos vazios (faltava
  `DataContext = this` no editor). Auto-foco da janela-alvo antes do
  overlay + volta do foco ao editor (`WindowFocus`/`Activate`).
- **Aceite final (2026-09-17, Windows):** ✅ B2 completo — duplicar preset
  de 2 contas, delays ajustados, disparo sem foco nas duas, click sem
  posição bloqueia o save, hotkey gravada dispara com jogo em foco.

## B2d — Click direito (botão por linha + prova no jogo)

- Modelo: `GameAction.MouseButton {Left, Right}`, default `Left`
  (aditivo; presets existentes intactos).
- Estratégia: variante direita da C4 (mesmo prime; `WM_RBUTTONDOWN/UP`).
  Receita sem-foco a provar — se refutada, documentar em `03` e manter
  o seletor desabilitado até nova receita (lei nº 5).
- Editor: seletor Esquerdo/Direito por linha de click.
- Aceite: click direito no retrato do personagem abre o menu de contexto
  (convidar/seguir) sem foco, 2/2.
- **Aceite (2026-09-17, Windows):** ✅ menu de contexto 2/2 sem foco.
  Decisão de desenho: a captura ouve sempre o botão esquerdo (posição não
  tem botão; ouvir o direito abriria menus durante a mira) — o botão do
  seletor vale só no disparo.

## B3 — Mini-mode da janela de grupo

- Janela compacta (tamanho de calculadora), `Topmost`, colapsável,
  só com botões de preset + liga/desliga do Sync.
- Aceite: jogável ao lado do client sem atrapalhar; config completa
  continua na janela normal.
- **Aceite (2026-09-18, Windows):** ✅ compacta `Topmost` colapsável com
  Fire + Sync + legenda B4; lista de membros com a conta ativa destacada
  (poll 500 ms); Fire/toggles sem roubar foco (`Focusable=False`,
  1 clique); toggles moram só no mini; `Cancelar disparos` com tooltip.
  B4-config (teclas configuráveis) vira item futuro, conjunto fixo
  documentado no B4.
- **Pós-B3 (2026-09-18):** mini nunca ativa (`WS_EX_NOACTIVATE`: 1 clique
  dispara com jogo focado — `Focusable=False` sozinho não bastava);
  tracking da ativa em 250 ms; modo por preset no editor (padrão
  Simultaneous); Fire mostra `i/n (Role)` por conta (`PresetProgress`).

## B5 — Contas (CRUD + credenciais)

- Editar conta (login/role; senha vazia mantém), excluir conta (limpa
  grupos + ações que a citam), login visível com copiar, senha mascarada
  com copiar (`Clipboard`; mesma exposição do reveal no disparo).
  Visibilidade da senha: futuro.
- Aceite: editar login/role e trocar senha pela UI; excluir some dos
  grupos/presets e persiste; copiar cola login/senha.
  (Validação no jogo pendente — roteiro entregue.)
- **Aceite (2026-09-18, Windows):** ✅ edição/remoção/cópia + apelido
  (espelha o role com "mesmo do personagem") + classe com ícone.

## Taskbar por conta (2026-09-18)

- Cada janela viva recebe `AppUserModelID` distinto
  (`PwAssistant.Client.{guid}`) via `SHGetPropertyStoreForWindow` →
  um botão de taskbar por client. Via `.lnk` refutada no jogo
  (`QueryInterface` p/ `IPropertyStore` falhou no `Play` — documentado
  aqui pela lei nº 5); launch segue `.exe` direto.
- Aceite: 2 Plays → 2 botões separados na taskbar.

## Refinos pós-validação (2026-09-18, Windows)

- Fire vira **Parar** com preset em loop (mesmo comando, rótulo alterna).
- Play vira **Stop** online (`CloseMainWindow` + `Kill` após 5 s;
  crash-watch conclui).
- Removidos: Fire do modo grupo e botão Cancelar (`CancelAll` segue no
  código para jobs/loops).

## B4 — Troca de janela por tecla (foco explícito a pedido)

- `` ` `` avança para o próximo membro online (ordem do grupo, cíclico);
  toque seco de Shift (<~300 ms sem outra tecla) alterna entre as duas
  últimas; numpad 1–9,0 foca o 1º–10º membro (limite da PT).
- Mecanismo: hook de teclado baixo nível (`WH_KEYBOARD_LL`, mesmo padrão
  do `MouseHook`) + `SetForegroundWindow`. `RegisterHotKey` não registra
  modificador puro — por isso o hook. Foco só com o modo ativo e janelas
  do grupo vivas; resto do v1 segue sem-foco. `SendInput`/driver/injeção
  seguem proibidos.
- Aceite: com 2 contas online sobrepostas, `` ` `` alterna o foco entre
  elas; numpad 1/2 foca por posição; Shift-toque alterna as duas últimas
  sem disparar ao usar Shift como modificador no jogo.
- **Aceite (2026-09-17, Windows):** ✅ os três modos com jogo focado,
  validado com 3 contas, sem eco de foco. Caminho até lá: hook entrega +
  alvo certo, mas `SetForegroundWindow` negado (pisca) → Attach
  insuficiente (mesmo elevado) → **tap Alt + retry resolve**. Receia
  confirmada por interoperabilidade com o PW Helper instalado
  (`focus-window-by-pid.exe` = `SetForegroundWindow`+`ShowWindow`+
  `keybd_event`; `play-preset-background.exe` = `PostMessage`, mesma
  filosofia do T1/C4).
