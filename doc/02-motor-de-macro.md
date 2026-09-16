# Motor de Macro

## Camadas

```
IWindowTarget    → abstrai "onde" mandar input (hwnd de uma conta específica)
IInputStrategy   → abstrai "como" mandar input (PostMessage vs foco+SendInput)
Action           → dado puro (tipo, posição/tecla, delay, repetição)
MacroExecutor    → recebe (IWindowTarget, Action)[], decide ordem/paralelismo
```

`IInputStrategy` fica isolada de propósito: ainda não sabemos se
`PostMessage` (sem precisar de foco) funciona em todas as telas do engine
do PW. Se algumas ações exigirem foco real, a troca de estratégia fica
contida nessa interface, sem reescrever o resto do app.

### IInputStrategy — duas implementações possíveis

1. **PostMessage/SendMessage direto no HWND**
   - `WM_LBUTTONDOWN`/`WM_LBUTTONUP`, `WM_KEYDOWN`/`WM_KEYUP`
   - Não exige foco → permite execução simultânea real em várias janelas.
   - Risco: parte da engine do jogo (DirectInput/raw input, algumas telas
     de HUD) pode ignorar mensagens postadas.

2. **SendInput com alternância de foco**
   - Simula input real do sistema, mas só funciona na janela em foco.
   - Execução "simultânea" vira alternância rápida entre janelas
     (sequencial, mas rápido o suficiente para parecer coordenado).
   - Mais compatível com engines que ignoram `PostMessage`, porém mais
     lento e mais sensível a travar se uma janela não responde a tempo.

Decisão de qual usar (ou uma combinação, por tipo de ação) depende dos
testes práticos no Windows — ver `03-pesquisa-e-validacoes.md`.

## Captura de posição de click

- Overlay transparente sobre a janela alvo (ou fullscreen).
- Captura o próximo click do mouse.
- Converte coordenada de tela → coordenada de client area via
  `ScreenToClient(hwnd, point)`.
- Fecha o overlay e retorna a coordenada para o formulário de edição da
  Ação/Preset.

## Execução de Preset

`MacroExecutor.Execute(preset)`:

1. Resolve `Hwnd` atual de cada `AccountId` envolvida (via processo ativo).
2. Se `ExecutionMode == Sequential`:
   - Para cada AccountAction, na ordem definida: aguarda `DelayBeforeMs`,
     executa a ação via `IInputStrategy`, segue para a próxima.
3. Se `ExecutionMode == Simultaneous`:
   - Dispara uma task/thread por conta, cada uma aguardando seu próprio
     `DelayBeforeMs` e executando via `IInputStrategy`.
   - **Só é verdadeiramente simultâneo se a estratégia ativa for
     PostMessage** (sem depender de foco). Com SendInput+foco, o
     "simultâneo" é, na prática, sequencial rápido.
4. Se `Repeat` estiver definida na Action, repete conforme
   `Times`/`IntervalMs` antes de passar para a próxima conta (sequencial)
   ou dentro da própria task (simultâneo).

## Hotkey global de disparo de preset

- Registrar hotkey global (via `RegisterHotKey` do WinAPI) para permitir
  disparar um preset sem precisar estar com o app em foco.
