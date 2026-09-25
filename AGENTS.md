# AGENTS.md — pwassistant

Instruções permanentes para qualquer agente/sessão neste repo. Ler antes de
qualquer tarefa. Contexto em 30 segundos: `doc/HANDOFF.md` → guias
`doc/00-visao-geral.md` → `doc/07-backlog.md` conforme necessário.

## Docs: congelados vs. vivos

- **Congelados (guias, não reescrever):** `doc/00`–`doc/05`. Só mexer na
  exceção da lei nº 5 (descoberta que contradiz o guia — doc primeiro).
- **Vivos (atualizar no mesmo passo):** `doc/06-marcos.md` (só carimbo de
  status: data + resultado + commit), `doc/07-backlog.md` (move item para
  feito + commit), `doc/HANDOFF.md` (reescrito ao fim da sessão, template
  abaixo).

## Stack e comandos

- C# / .NET 8 + WPF. Testes: xUnit. SO de execução/validade: **Windows**
  (WinAPI + jogo não rodam no Linux).
- Comandos: `dotnet build pwassistant.sln`, `dotnet test`, `dotnet publish` (detalhes
  por marco). PowerShell 5.1 nos scripts de prova (`tools/provas-winapi/`).

## Idioma e estilo (convenção do repo)

- Código 100% em **inglês**: identificadores, arquivos, diretórios, testes,
  schema de banco/JSON, logs técnicos. Português (pt-BR) **somente** em textos
  visíveis ao usuário (UI, mensagens apresentadas) e nos docs do repo.
- Clean code por padrão, sem precisar pedir: SRP, funções pequenas, nomes
  expressivos, sem duplicação nem código morto, erros tratados com intenção
  (nunca `catch` vazio), `using`/dispose correto. Calibrado com YAGNI para
  projeto pessoal/simples: sem camadas especulativas — só o que o marco
  atual exige.
- Perguntar antes só em trade-off real (ex.: retry/backoff vs falhar rápido;
  senha mestra vs DPAPI).

## Leis do projeto (não negociar sem o usuário)

1. **Sem driver kernel, sem injeção/DLL no jogo, sem `SendInput` como padrão.**
   Caminho único do v1: `PostMessage` com priming (receita exata no
   `doc/04-arquitetura.md` + skill `pw-winapi`).
2. **Todo P/Invoke em `src/PwAssistant.WinApi`.** Nenhum `DllImport` fora dele.
   `Core` não conhece WinAPI (fala por `IInputStrategy`/`IWindowTarget`).
3. **Alvo sempre PID → HWND via `EnumWindows`.** Nunca `MainWindowHandle`
   cacheado, nunca título como chave, nunca broadcast cego (só HWNDs de PIDs
   do próprio launcher).
4. **Segredos nunca em texto puro** (disco, log, exceção). Runtime nunca persiste.
5. **Docs antes/depois do código:** descoberta que muda comportamento atualiza
   `doc/` no mesmo passo; `doc/06-marcos.md` marca aceite com data.

## Armadilhas conhecidas (custo já pago — não repetir)

- `lParam = 0` em `WM_KEYDOWN` falha: exige scan code (`MapVirtualKey`).
- `$Pid` é variável automática reservada no PowerShell (usar `$TargetPid`).
- `ElementClient Window` não tem filhas: alvo é o top-level.
- Skills do jogo têm **cooldown** — validar repetibilidade com toggle
  (F1 montaria), não com skill.
- Click no chão 3D não passa sem foco (fora do v1); UI-click passa (C4).
- Retorno `!= 0` de `PostMessage` ≠ efeito no jogo (só prova entrega).

## Fluxo de validação contra o jogo

- Sempre com **foco fora do jogo**, ação de baixo risco primeiro
  (F1 montaria / UI reversível), countdown ≥ 3 s.
- Controles: T0 puro deve falhar sem foco (se passar, algo mudou no engine —
  parar e investigar antes de prosseguir).
- Registrar resultados no marco correspondente antes de commitar.

## Commits

- Só com pedido explícito. **Um commit por etapa/bloco concluído** — cada
  unidade de trabalho fechada (ex.: bootstrap da solution, WinApi + Probe,
  Core + testes, App WPF) gera um commit próprio, independente de
  corresponder ou não a um marco inteiro do `doc/06-marcos.md`.
- Commits atômicos (padrão de mercado): cada commit compila e mantém os
  testes verdes; uma mudança lógica por commit. Se um arquivo mistura dois
  blocos, separar por hunk (`git add -p` ou equivalente) em vez de agrupar
  por arquivo.
- Docs junto do código que os afeta no mesmo commit (ex.: descoberta `03`
  com o fix); bookkeeping de processo (carimbos do `06`, `HANDOFF`
  reescrito, `README` revisado) em `docs:` separado(s).
- Padrão: Conventional Commits, tudo em inglês, imperativo, curto:
  `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:` + escopo opcional
  (ex.: `feat(winapi): add background key press with activation priming`).
- Nunca commitar segredos, `.user`, `bin/`, `obj/`.

## Fim de sessão (obrigatório se mexeu no repo)

1. `dotnet build pwassistant.sln` + `dotnet test` verdes (ou registrar o que quebrou).
2. Commits por etapa/bloco concluído (regra acima).
3. Reescrever `doc/HANDOFF.md` neste template, sempre curto (história fica
   no git log, nunca em prosa acumulada). Sessão só-leitura não precisa disso.

```markdown
# HANDOFF — estado em <data>, base <commit>
## Pronto (validado)
## Pronto (código, pendente de jogo/Windows)
## Próximo passo
## Backlog pós-validação (ponteiro p/ `doc/07-backlog.md`)
## Perguntas abertas
```
