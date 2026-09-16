# ditto — PW Launcher/Helper

Launcher multi-conta + assistente de macros para Perfect World (servidor
privado). Input sem foco via `PostMessage` com priming de ativação —
sem driver, sem injeção, sem `SendInput` como padrão.

## Projetos

```
ditto.sln
├─ src/PwHelper.Core        → models, regras, MacroExecutor, SyncService, storage (sem WinAPI)
├─ src/PwHelper.WinApi      → P/Invoke isolado (único projeto com DllImport)
├─ src/PwHelper.App         → WPF + MVVM (só roda no Windows)
├─ src/PwHelper.Probe       → console de validação M1 (só roda no Windows)
└─ tests/PwHelper.Core.Tests → xUnit, lógica pura (roda em qualquer SO)
```

## Comandos

```bash
dotnet build ditto.sln
dotnet test tests/PwHelper.Core.Tests

# App self-contained para Windows (funciona a partir do Linux):
dotnet publish src/PwHelper.App -r win-x64 --self-contained -c Release
```

No Linux, o projeto `PwHelper.App` compila graças a
`EnableWindowsTargeting`, mas só **executa** no Windows.

## Validação no Windows (aceite pendente — ver `doc/06-marcos.md`)

Com o jogo **sem foco**, countdown de 3 s, ação de baixo risco primeiro:

```powershell
# M1: tecla F1 (montaria) sem foco — esperado: monta/desmonta
.\PwHelper.Probe.exe key --pid <pid> --key F1

# M1: UI-click sem foco (coordenada de client area) — esperado: click de UI
.\PwHelper.Probe.exe click --pid <pid> --x 400 --y 300
```

Controles: a variante T0 pura (`tools/provas-winapi/teste-bateria.ps1 -Variant T0`)
deve **falhar** sem foco — se passar, o engine mudou; parar e investigar.

## Regras do projeto

Ver `AGENTS.md` e `doc/`. Resumo: código em inglês, UI/docs em pt-BR;
todo P/Invoke em `PwHelper.WinApi`; alvo sempre PID → HWND via
`EnumWindows`; segredos nunca em texto puro (DPAPI, escopo CurrentUser);
docs atualizados no mesmo passo do código que os afeta.
