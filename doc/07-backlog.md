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
  validação por linha no save, último servidor lembrado. Bug no caminho:
  combos vazios (faltava `DataContext = this` no editor). Auto-foco da
  janela-alvo antes do overlay (`WindowFocus`, explícito a pedido).
  Falta: duplicar preset, campo de hotkey, renomear preset.

## B3 — Mini-mode da janela de grupo

- Janela compacta (tamanho de calculadora), `Topmost`, colapsável,
  só com botões de preset + liga/desliga do Sync.
- Aceite: jogável ao lado do client sem atrapalhar; config completa
  continua na janela normal.

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
