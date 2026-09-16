# Handoff — PW Launcher/Helper (retomar contexto)

Este documento resume tudo decidido até agora, pra colar em outra IA/sessão
e continuar o projeto sem perder contexto. Os 4 documentos técnicos
completos (`00` a `03`) também fazem parte do projeto — anexe-os se
possível. Aqui vai um resumo executivo + o ponto exato onde paramos.

## O projeto

Ferramenta desktop pessoal para Perfect World (servidor privado),
projeto próprio para uso pessoal. Duas frentes:

1. **Launcher/gerenciador de contas** — CRUD de servidores/contas, login
   automático via `startbypatcher user:LOGIN pwd:SENHA role:NICK` passado
   como argumento pro `elementclient.exe` (element x64 do jogo).
2. **Macro/automação multi-conta** — gravar posição de click e tecla,
   reproduzir em várias contas (processos) ao mesmo tempo. Inclui "Modo
   Grupo" com Presets configuráveis (ação por conta + delay).

## Decisões já fechadas

- **Stack: C# / .NET 8 + WPF.** Motivo: WinAPI via P/Invoke é maduro pra esse
  tipo de automação, WPF dá GUI de verdade sem gambiarra, distribuição em .exe
  self-contained.
- **Ambiente:** desenvolvimento auxiliado por IA. Máquina de validação é
  Windows; Linux usado só pra gerar código/documentação, sem execução real.
- **Arquitetura em camadas:**
  ```
  PwHelper.Core     → models, lógica de negócio, storage (sem WinAPI)
  PwHelper.WinApi   → wrapper isolado de P/Invoke
  PwHelper.App      → WPF, telas, viewmodels (MVVM)
  ```
  `WinApi` isolada de propósito, porque a estratégia de input ainda está
  em definição (ver abaixo).
- **Modelo de dados:** Server → Accounts; Group → Presets → AccountAction →
  Action (Type Key/Click, RelativePosition, DelayBeforeMs,
  Repeat opcional). Detalhes completos em `01-modelo-dados-e-telas.md`.

## Onde paramos (ponto crítico em aberto)

**Testamos no Windows se `PostMessage` (WM_KEYDOWN/WM_KEYUP) consegue
acionar uma ação no jogo sem a janela estar em foco. Resultado: NÃO
funcionou.** A mensagem foi enviada com sucesso (retorno da API OK), mas o
personagem só reagiu quando a janela estava realmente em foco. Isso indica
que o engine do PW usa DirectInput/raw input, que ignora mensagens Win32
clássicas postadas via `PostMessage`.

Isso é um problema central porque **execução simultânea real em várias
contas depende de conseguir mandar input sem foco** (só uma janela pode
ter foco por vez no Windows).

**Observação relevante:** ferramentas existentes executam ações em background
sem tirar o foco visível da tela do usuário. Isso sugere técnica além de
`PostMessage` simples.

## Atualizacao 2026-09-10 — breakthrough: T1 funciona sem foco

Bateria diferencial (`teste-bateria.ps1`, variantes T0-T5/C0-C4) executada contra
`elementclient_64` real: T0 (PostMessage puro) falha sem foco; **T1 (priming
`WM_ACTIVATE`/`WM_SETFOCUS`/`WM_ACTIVATEAPP` + tecla com scan code) FUNCIONA sem foco**
(F1 = montaria, foco fora do jogo o tempo todo). T2/T3/T5 falham sem foco. Descobertas
laterais: a janela do jogo nao tem filhas (alvo = top-level `ElementClient Window`); sem
DLLs injetados nos clients; análise externa indicou executor de background baseado em
`PostMessageW` (sem driver/injeção), reforçando o caminho próprio. Driver kernel
descartado em definitivo.
Pendente: C4 (click com priming), higiene `WA_INACTIVE` (`teste-higiene.ps1`), repetir em
2o PID/outras teclas. Proximo passo: esqueleto C# com `PostMessageBackgroundStrategy`
(prime + send).

> **Nota histórica (superada em 2026-09-10):** hipóteses originais abaixo,
> mantidas para registro. O breakthrough T1 (ver bloco acima) as substitui;
> driver kernel descartado em definitivo.
>
> ### Hipóteses originais (registro)

1. **`AttachThreadInput` + `SetForegroundWindow` + `SendInput` + restaurar
   foco original**, tudo em poucos milissegundos — troca real de foco,
   mas rápido o suficiente para não ser perceptível/incômodo pro usuário.
   Testar primeiro por ser a abordagem mais simples de implementar com
   WinAPI pura, sem dependências externas.
2. **Driver de input virtual em nível de kernel** — ~~descartado em
   2026-09-10 (ver bloco acima)~~.
3. Investigar se existe alguma correlação entre foco e "última janela
   ativa do processo" que o DirectInput possa aceitar sem foco do SO
   propriamente dito (menos provável — superado pelo T1).

## Próximos passos (atual)

Ver `06-marcos.md` (M1 em diante). O roteiro abaixo é histórico (superado):

1. ~~Testar hipótese 1 (`AttachThreadInput`) no Windows~~ — virou fallback
   documentado, só se um dia provado necessário.
2. ~~Pesquisar/testar driver de input virtual~~ — descartado.
3. ~~Fechar `IInputStrategy` após teste~~ — feito: `PostMessageBackgroundStrategy`
   (prime + send), ver `04-arquitetura.md`.

## Arquivos de referência do projeto

- `00-visao-geral.md` — objetivo, escopo, stack
- `01-modelo-dados-e-telas.md` — modelo de dados e telas do app
- `02-motor-de-macro.md` — arquitetura do motor de macro (`IInputStrategy`,
  `MacroExecutor`, captura de posição, execução de preset)
- `03-pesquisa-e-validacoes.md` — checklist de validações técnicas,
  incluindo o resultado do teste de `PostMessage` sem foco (negativo) e as
  hipóteses de próximo passo listadas acima
