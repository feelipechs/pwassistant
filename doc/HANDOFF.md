# HANDOFF — ditto (ponto de entrada para IAs)

> Lido em 30 segundos: estado, base, próximo passo. Detalhe técnico nos
> `doc/00–07`; história no `git log`. Reescrito ao fim de cada sessão que
> mexe no repo (template e regra no `AGENTS.md`).

## Estado em 2026-09-16, base `7634840`

PW Launcher/Helper (Perfect World, servidor privado): launcher multi-conta
via `startbypatcher` + macros sem foco via `PostMessage` com priming
(T1 tecla / C4 UI-click). Sem driver, sem injeção, sem `SendInput` padrão.

## Pronto (validado no Linux)

- `dotnet build ditto.sln` — 0 erros, 0 warnings (App WPF incluso, via
  `EnableWindowsTargeting`; só executa no Windows).
- `dotnet test` — 30/30 verde (`tests/PwHelper.Core.Tests`).
- `dotnet publish src/PwHelper.App -r win-x64 --self-contained` — ok.
- `grep DllImport src` — só `PwHelper.WinApi/NativeMethods.cs` (lei nº 2).
- Segredo: DPAPI `CurrentUser` atrás de `ISecretProtector` (decisão no `06`).

## Pronto (código, pendente de jogo/Windows)

Aceite real dos marcos M1–M6, nesta ordem (`doc/06-marcos.md`):

1. `PwHelper.Probe key --pid <pid> --key F1` — F1 toggle 3/3 sem foco, 2 PIDs.
2. `PwHelper.Probe click --pid <pid> --x <cx> --y <cy>` — UI-click sem foco.
3. Preset 2 contas sem foco + offline pulada; Sync A→B manual; fluxo App
   completo; checklist 5 contas.
- Controles: T0 puro deve falhar sem foco (`tools/provas-winapi/`); se
  passar, o engine mudou — parar e investigar.

## Próximo passo

Ir ao Windows e rodar o item 1 acima. Nada de código novo antes disso —
qualquer falha na receita T1/C4 muda `WinApi` e tudo acima dela.

## Backlog pós-validação

`doc/07-backlog.md`: gerência de membros do grupo, CRUD de preset
(duplicar, reordenar, drag&drop por último), mini-mode da janela de grupo.
Nada disso muda modelo nem estratégia de input (triagem feita).

## Perguntas abertas

- Nenhuma bloqueante. Dúvidas futuras do usuário entram aqui.
