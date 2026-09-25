# PwAssistant

Launcher multi-conta + assistente de macros para Perfect World (servidor
privado). Input sem foco via `PostMessage` com priming de ativação —
sem driver, sem injeção, sem `SendInput` como padrão.

## Instalar (usuários)

1. Baixe o `PwAssistant-win-Setup.exe` em
   https://github.com/feelipechs/pwassistant/releases.
2. Rode — instala por usuário, sem perguntas, e abre sozinho.
3. Sem certificado de code signing: o Windows SmartScreen vai avisar
   na primeira execução. É esperado até o instalador ganhar reputação.

Atualizações são automáticas: o app avisa em português quando houver
versão nova, baixa o delta, aplica e reinicia sozinho.

## Segurança (resumo)

- Senhas: criptografadas com DPAPI (`CurrentUser`) em
  `%AppData%\PwAssistant\accounts.json`. Nunca em texto puro.
- Atalhos por conta (`.lnk`): guardam só identidade + login (sem senha).
  A senha existe no disco apenas no instante do Play.
- Logs em `%AppData%\PwAssistant\logs\`: sem segredos, com retenção
  (30 dias + teto de 50 MB).
- Residual conhecido: a senha aparece na linha de comando do processo
  durante o Play (Task Manager) — inerente ao protocolo do client.

## Uso

1. Cadastre o servidor apontando o `elementclient.exe` **dentro da
   pasta x64**, adicione as contas e dê Play.
2. Modo Grupo: Fire dispara o preset; Sync replica clicks; Mini é o
   controle compacto durante o jogo.
3. Foco fora do jogo ao disparar; ação de baixo risco primeiro
   (F1 montaria).

## Desenvolver

```bash
dotnet build pwassistant.sln
dotnet test

# App self-contained para Windows:
dotnet publish src/PwAssistant.App -c Release -r win-x64 --self-contained /p:PublishSingleFile=true -o ./publish
```

Só roda no Windows (`EnableWindowsTargeting` deixa compilar no Linux).
Saídas de build vão para `%TEMP%\pwassistant-build`
(via `Directory.Build.props`), nunca para dentro do repo.

## Regras do projeto

Ver `AGENTS.md` e `doc/`. Resumo: código em inglês, UI/docs em pt-BR;
todo P/Invoke em `PwAssistant.WinApi`; alvo sempre PID → HWND via
`EnumWindows`; segredos nunca em texto puro; docs atualizados no mesmo
passo do código que os afeta (`doc/`, `plans/`).

Mapa rápido: `doc/00-visao-geral.md` (comece aqui) →
`doc/04-arquitetura.md` (decisões) → `doc/06-marcos.md` (aceites) →
`doc/07-backlog.md` (futuro) → `plans/` (execução por fase).
