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
 └─ AccountIds: List<Guid>   (ordenados; organização pura — excluir a tab
     nunca exclui contas)

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
- Senha fica sempre criptografada em disco (`EncryptedPassword`, AES, chave
  derivada de senha mestra ou de segredo local da máquina — decidir na
  implementação).
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
  `00-visao-geral.md`), mas a estrutura de arquivo único facilita
  adicionar depois.

## Telas

### Shell principal
- Menu lateral: lista de Servidores + botão "Adicionar Servidor" (seleciona
  o `elementclient.exe` daquele server).
- Área central: cards de personagem do servidor selecionado (nick, tag,
  status online/offline, tempo jogado).
- Botão "Adicionar Conta" (login/senha/role).
- Botão de play no card → spawna processo com `startbypatcher`.

### Modo Grupo (janela separada)
- Lista de grupos (criar/editar/excluir).
- Grid mostrando as contas do grupo **que estão online no momento**
  (cruzamento AccountId → Hwnd ativo).
- Área de Presets, cada um como um botão:
  - Criar preset: selecionar contas online → definir Ação por conta
    (tecla ou click, com captura de posição via overlay) → definir delay
    → salvar.
  - Clicar no botão do preset → dispara `MacroExecutor` (ver
    `02-motor-de-macro.md`).

## Fora do escopo do modelo v1

- Mapa interativo / marcações de mundo.
- Auto forja.
- Perfis de formação exportáveis com reconexão automática de personagem
  (recurso avançado) — revisitar depois que o core
  estiver estável.
