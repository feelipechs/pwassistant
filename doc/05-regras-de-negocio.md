# Regras de Negócio — PwAssistant

Fonte: `01-modelo-dados-e-telas.md` (modelo) + provas da Fase 1
(`03-pesquisa-e-validacoes.md`, `HANDOFF.md`). Este arquivo é a referência
normativa; em caso de conflito com código, o código deve ser corrigido
(ou a regra atualizada explicitamente com data e motivo).

## Entidades e invariantes

- **Server**: `Id, Name, ElementClientPath, ForceServer?, Accounts[]`.
  `ElementClientPath` aponta o `elementclient.exe` daquele server.
- **Account**: `Id, ServerId, Login, EncryptedPassword, Role, Tag/Color?,
  IsFavorite, TotalPlayedTimeMs` + runtime (`ProcessId?, Hwnd?, Status`).
  - Senha **nunca** em texto puro em disco ou log.
  - Runtime **nunca** persiste; recalculado ao abrir (PID → HWND via
    `EnumWindows`, nunca via cache).
- **Group**: `Id, Name, AccountIds[]`. Membro offline é admitido no grupo,
  mas **pulado silenciosamente com log** no disparo (nunca aborta o lote).
  Excluir grupo pede confirmação e exclui os presets dele em cascata.
- **AccountTab**: `Id, ServerId, Name` (+ `AccountIds` legado, ver `01`).
  Organização pura (ex.: PT1, PT2): a tab guarda nome/ordem e os membros
  são as contas com `Account.Tag == Name` (case-insensitive). Renomear
  retaggeia em massa; excluir limpa as Tags (contas caem em "Todas",
  nunca são excluídas). "Todas" é implícita.
- **Server**: excluir pede confirmação e limpa em cascata (contas saem de
  grupos, tabs e ações de preset que as citam).
- **Preset**: `Id, GroupId, Name, Hotkey?, ExecutionMode, Actions[]`.
- **AccountAction**: `AccountId + Action`.
- **Action**: `Type (Key|Click), Key?, RelativePosition?, DelayBeforeMs,
  Repeat?`.
  - `RelativePosition` é fração da client area (0.0–1.0), NÃO pixel absoluto
    — tolera janelas de tamanhos/posições distintos (mesma convenção do Sync).
  - `DelayBeforeMs < 0` é normalizado para 0.
  - Click sem `RelativePosition` é inválido (rejeitar na edição, não no disparo).

## Execução de preset

1. Resolver `Hwnd` atual de cada `AccountId` (processo ativo). Sem HWND → pula + log.
2. `Sequential`: ordem definida, `DelayBeforeMs` antes de cada ação,
   `Repeat` (vezes × intervalo) esgota antes da próxima conta.
3. `Simultaneous`: uma task por conta; cada uma respeita seu `DelayBeforeMs`.
4. Toda ação usa a receita da estratégia ativa (v1: priming + send + higiene
   opcional, ver `04-arquitetura.md`). Retorno da API (`PostMessage != 0`)
   NÃO significa efeito no jogo — logar, mas não tratar como sucesso.
5. Cancelamento por `jobId`: para o lote entre ações (nunca no meio de um
   par DOWN/UP — par é atômico).

## Modo Grupo e Sync Click

- Grid do grupo mostra **apenas contas online** (cruzamento `AccountId → Hwnd`).
- Sync Click (quando implementado, M4): master = janela em foco; só botão
  esquerdo; só dentro de janela de jogo registrada; réplica em UI-clicks.
  Clicks no chão 3D **não replicam** (limitação do v1, documentada ao usuário
  se exposta em UI).
- Hotkey global de preset: via `RegisterHotKey`; disparo fora de foco do app.

## Launcher

- Play no card = spawn `elementclient.exe startbypatcher user:LOGIN pwd:SENHA
  role:NICK` (processo filho independente por conta; PIDs distintos).
- Mapear delay entre spawn e janela visível (risco conhecido do item 3 do `03`):
  polling de HWND com timeout, não `Sleep` fixo.
- Título da janela pode variar com `role` — exibição, nunca chave de busca
  (chave é sempre PID → HWND).

## Limitações do v1 (escopo fechado)

- Sem mapa interativo, auto forja, latência, backup automático.
- Sem click no chão 3D sem foco; sem driver; sem injeção; sem `SendInput`
  como padrão (só fallback documentado, se um dia provado necessário).
- Implementação 100% própria: sem binários de terceiros, drivers ou injeção.

## Segurança e higiene

- Segredos: AES em repouso; nunca em log, exceção ou clipboard.
- Reveal em tela (2026-09-20, código pendente de validação): botão
  Mostrar/Ocultar no card da conta, a pedido, com auto-hide em 15 s;
  mesmo modelo de exposição do copiar (clipboard com auto-clear 30 s);
  nunca em log, exceção ou disco.
- Mensagens forjadas vão **só** para HWNDs de PIDs do próprio launcher
  (nunca broadcast/enumeração cega).
- Higiene `WA_INACTIVE` após rajadas longas (receita validada em
  `tools/provas-winapi/teste-higiene.ps1`).
