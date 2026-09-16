# Backlog pós-v1 — ditto

Itens fora dos marcos M1–M6, triados em 2026-09-16 a partir do fluxo
desejado (estilo PW Helper). Nenhum exige mudança de modelo (`01`) nem de
estratégia de input (`04`): ordem = ordem da lista `Actions`, tempo = 
`DelayBeforeMs`, duplicar = clonar objeto, presets por grupo = `GroupId`.
Tudo é camada App. Só implementar após o aceite M1 no Windows.

## B1 — Gerência de membros do grupo

- Adicionar/remover contas de um grupo; grid continua exibindo só online.
- Aceite: montar grupo de 3 contas, 1 offline some do grid sem erro,
  volta ao logar (refresh).

## B2 — CRUD de preset completo

- Criar/editar/excluir preset; adicionar/remover ação por conta
  (tecla + click com captura via overlay); tempo em ms; duplicar preset;
  reordenar ações/personagens (lista simples primeiro).
- Drag & drop na ordem: só se a lista simples incomodar em uso real.
- Aceite: duplicar preset de 2 contas, ajustar delays, disparar sem foco
  nas duas; linha inválida (click sem posição) bloqueia o save.

## B3 — Mini-mode da janela de grupo

- Janela compacta (tamanho de calculadora), `Topmost`, colapsável,
  só com botões de preset + liga/desliga do Sync.
- Aceite: jogável ao lado do client sem atrapalhar; config completa
  continua na janela normal.
