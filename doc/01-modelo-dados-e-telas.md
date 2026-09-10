# Modelo de Dados e Telas

## Modelo de dados

```
Servidor
 ├─ Id
 ├─ Nome
 ├─ CaminhoElementClient   (path do element x64 daquele server)
 ├─ ForceServer            (opcional — pula tela de seleção de servidor no jogo)
 └─ Contas: List<Conta>

Conta
 ├─ Id
 ├─ ServidorId
 ├─ Login
 ├─ SenhaCriptografada
 ├─ Role                   (nick do personagem)
 ├─ Tag / Cor              (organização visual, opcional)
 ├─ Favorito: bool
 ├─ TempoTotalJogadoMs     (acumulado, calculado por sessão)
 ---- campos de runtime (não persistidos) ----
 ├─ ProcessId: int?
 ├─ Hwnd: IntPtr?
 └─ Status: Online | Offline

Grupo
 ├─ Id
 ├─ Nome
 └─ ContaIds: List<Guid>   (membros possíveis; nem todos precisam estar online)

Preset
 ├─ Id
 ├─ GrupoId
 ├─ Nome
 ├─ Hotkey                 (opcional)
 ├─ ModoExecucao: Sequencial | Simultaneo
 └─ Acoes: List<AcaoPorConta>

AcaoPorConta
 ├─ ContaId
 └─ Acao: Acao

Acao
 ├─ Tipo: Tecla | Click
 ├─ Tecla: string?                    (se Tipo = Tecla)
 ├─ PosicaoRelativa: (x, y)?          (se Tipo = Click; relativa ao client area)
 ├─ DelayAntesMs: int
 └─ Repeticao: (Vezes, IntervaloMs)?  (opcional — combo/loop)
```

Notas:
- Senha fica sempre criptografada em disco (AES, chave derivada de senha
  mestra ou de segredo local da máquina — decidir na implementação).
- `PosicaoRelativa` é relativa ao client area da janela (via
  `ScreenToClient`), não à tela. Mesmo com todas as janelas no mesmo
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
  (cruzamento ContaId → Hwnd ativo).
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
  (feature avançada do PW Helper PRO) — revisitar depois que o core
  estiver estável.
