# ditto — PW Assistant

Launcher multi-conta + assistente de macros para Perfect World (servidor
privado). Input sem foco via `PostMessage` com priming de ativação —
sem driver, sem injeção, sem `SendInput` como padrão.

## Projetos

```
pwassistant.sln
├─ src/PwAssistant.Core        → models, regras, MacroExecutor, SyncService, storage (sem WinAPI)
├─ src/PwAssistant.WinApi      → P/Invoke isolado (único projeto com DllImport)
├─ src/PwAssistant.App         → WPF + MVVM (só roda no Windows)
├─ src/PwAssistant.Probe       → console de validação M1 (só roda no Windows)
└─ tests/PwAssistant.Core.Tests → xUnit, lógica pura (roda em qualquer SO)
```

## Comandos

```bash
dotnet build pwassistant.sln
dotnet test tests/PwAssistant.Core.Tests

# App self-contained para Windows (funciona a partir do Linux):
dotnet publish src/PwAssistant.App -r win-x64 --self-contained -c Release
```

No Linux, o projeto `PwAssistant.App` compila graças a
`EnableWindowsTargeting`, mas só **executa** no Windows.
Para rodar os testes é preciso o **runtime .NET 8** instalado
(lado a lado com outras versões, sem conflito).

## Acesso Controlado a Pastas (Windows Security)

Saídas de build (`bin/`, `obj/`) vão para `%TEMP%\ditto-build`
(via `Directory.Build.props`), nunca para dentro do repo — assim o
compilador não bate no Acesso Controlado mesmo com o repo dentro de
Documentos. Se o Windows barrar o App em runtime (raro: só escrevemos
em `%AppData%`), libere em Segurança do Windows → Proteção contra
ransomware → *Permitir um aplicativo*.

## Uso no Windows (fluxo validado — ver `doc/06-marcos.md`)

```powershell
# 1. App: cadastrar servidor (elementclient.exe) + contas, Play x2
dotnet run --project src/PwAssistant.App

# 2. Modo grupo: Fire dispara o preset; checkbox Sync replica clicks
# 3. Hotkey global do preset (com o jogo em foco): CTRL+SHIFT+F9
```

Console de diagnóstico (sem App, jogo sem foco, countdown de 3 s):

```powershell
# Tecla F1 (montaria) num PID — esperado: monta/desmonta
dotnet run --project src/PwAssistant.Probe -- key --pid <pid> --key F1

# UI-click (coordenada de client area) — esperado: click de UI
dotnet run --project src/PwAssistant.Probe -- click --pid <pid> --x 1343 --y 305

# Preset F1 + click nas 2 contas (Simultaneous) + skip de offline
dotnet run --project src/PwAssistant.Probe -- preset --pid <pid1> --pid <pid2> --x 1343 --y 305

# Sync listener: click físico em qualquer janela replica na outra (ENTER para)
dotnet run --project src/PwAssistant.Probe -- sync --pid <pid1> --pid <pid2>
```

Controles: a variante T0 pura (`tools/provas-winapi/teste-bateria.ps1 -Variant T0`)
deve **falhar** sem foco — se passar, o engine mudou; parar e investigar.

Limites do v1: click no chão 3D não passa sem foco; grupo/preset ainda
sem telas de gerência (ver `doc/07-backlog.md`); dados em
`%AppData%\PwAssistant\accounts.json` (senhas via DPAPI).

## Regras do projeto

Ver `AGENTS.md` e `doc/`. Resumo: código em inglês, UI/docs em pt-BR;
todo P/Invoke em `PwAssistant.WinApi`; alvo sempre PID → HWND via
`EnumWindows`; segredos nunca em texto puro (DPAPI, escopo CurrentUser);
docs atualizados no mesmo passo do código que os afeta.
