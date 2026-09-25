# Modelo de Dados e Telas

## Modelo de dados

```
Server
 ├─ Id
 ├─ Name
 ├─ ElementClientPath   (path do element x64 daquele server)
 ├─ ForceServer          (opcional — pula tela de seleção de servidor no jogo)
 └─ Accounts: List<Account>

Account
 ├─ Id
 ├─ ServerId
 ├─ Login
 ├─ EncryptedPassword
 ├─ Role                 (nick do personagem)
 ├─ Tag / Color          (organização visual, opcional)
 ├─ IsFavorite: bool
 ├─ TotalPlayedTimeMs    (acumulado, calculado por sessão)
 ---- runtime fields (não persistidos) ----
 ├─ ProcessId: int?
 ├─ Hwnd: IntPtr?
 └─ Status: Online | Offline

Group
 ├─ Id
 ├─ Name
 └─ AccountIds: List<Guid>   (membros possíveis; nem todos precisam estar online)

AccountTab
 ├─ Id
 ├─ ServerId
 ├─ Name
 └─ AccountIds: List<Guid>   (legado: migração 2026-09-21 preencheu
     `Account.Tag` e limpou; membros = contas com `Tag == Name`,
     case-insensitive; excluir a tab nunca exclui contas)

Preset
 ├─ Id
 ├─ GroupId
 ├─ Name
 ├─ Hotkey               (opcional)
 ├─ ExecutionMode: Sequential | Simultaneous
 └─ Actions: List<AccountAction>

AccountAction
 ├─ AccountId
 └─ Action: Action

Action
 ├─ Type: Key | Click
 ├─ Key: string?                    (se Type = Key)
 ├─ RelativePosition: (x, y)?       (se Type = Click; relativa ao client area)
 ├─ DelayBeforeMs: int
 └─ Repeat: (Times, IntervalMs)?    (opcional — combo/loop)
```

Notas:
- Senha fica sempre criptografada em disco (`EncryptedPassword`, DPAPI
  `CurrentUser` via `ProtectedData`, sem entropia — decisão da
  implementação; protege contra roubo de disco e outros usuários do SO,
  não contra processos do mesmo usuário).
- `RelativePosition` é fração da client area da janela (via
  `ScreenToClient`), não pixel de tela. Mesmo com todas as janelas no mesmo
  tamanho/posição, isso deixa o dado correto por definição, não por
  coincidência de configuração do usuário.
- Campos de runtime (`ProcessId`, `Hwnd`, `Status`) nunca vão para o JSON
  de persistência — são recalculados a cada abertura do app, verificando
  processos ativos.

## Storage

- Arquivo local (JSON) por enquanto, criptografado.
- Backup/restauração automática fica fora do escopo v1 (ver
  `overview.md`), mas a estrutura de arquivo único facilita
  adicionar depois.

## Telas

### Shell principal
- Menu lateral: lista de Servidores + botão "Adicionar Servidor" (seleciona
  o `elementclient.exe` daquele server).
- Área central: cards de personagem do servidor selecionado (nick, tag,
  status online/offline, tempo jogado).
- Botão "Adicionar Conta" (login/senha/role).
- Botão de play no card → spawna processo com `startbypatcher`.

### Modo Grupo (janela separada, repaginada em cards em 2026-09-21)

- Esquerda: pool de personagens sem grupo (dot online/offline); arrastar
  para um card adiciona (sai do pool; ✕ devolve).
- Direita: um card por grupo (clicar ativa — borda gold; sync, troca de
  janelas, Mini e hotkeys seguem o ativo). Cada card tem `...` com
  Renomear/Excluir/Duplicar, Presets em drill-in na própria janela
  (lista + editor lado a lado) e formações em painel (snapshot dos
  membros do card).
- Offline edita preset normalmente; no disparo é pulado com log.

## Fora do escopo do modelo v1

- Mapa interativo / marcações de mundo.
- Auto forja.
- Perfis de formação exportáveis com reconexão automática de personagem
  (recurso avançado) — revisitar depois que o core
  estiver estável.
