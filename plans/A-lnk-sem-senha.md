# Fase A — `.lnk` sem senha (bloqueadora)

## Problema

O `.lnk` por conta grava a senha em texto puro no disco
(`GameLauncher.cs:45` monta `user:{Login} pwd:{plaintext} ...`,
`GameLauncher.cs:67-80` copia para `ShortcutDefinition.Arguments`,
`ShortcutCreator.cs:32,35` persiste em
`%AppData%\PwAssistant\clients\{accountId}.lnk`). Quem ler o arquivo
recupera a senha. Viola a lei nº 4 do `AGENTS.md`.

## Mudança

1. `GameLauncher.cs`: `.lnk` passa a guardar **só identidade**
   (alvo, working dir, ícone da classe, `accountId` se preciso) —
   **nenhum argumento com segredo**. A senha é resolvida em runtime
   via `AccountStore.RevealPassword` (DPAPI) na hora do Play e montada
   só no `ProcessStartInfo.Arguments` em memória.
2. Confirmar que o agrupamento da taskbar continua funcionando só com
   identidade do atalho (alvo + AppUserModelID/ícone): validar com
   2 Plays (classes iguais e diferentes) → 2 botões separados.
3. `Core/Ux/LogRedactor.cs`: alargar além de `pwd:\S+` — mascarar
   `user:`, `role:` e login isolado onde aparecerem em mensagens de
   launch (manter teste que hoje retém `user:hero`? Não — atualizar
   `LoggingTests` para o novo comportamento).
4. Varrer sinks de log do launch (`MainViewModel` linhas ~518-615) para
   garantir que nenhum carrega segredo fora do padrão mascarado.
5. `doc/01-modelo-dados-e-telas.md:61`: trocar "AES" por DPAPI
   (`ProtectedData`, `CurrentUser`, sem entropia).

## Risco residual (aceito e documentado)

A senha continua visível na **linha de comando do processo** durante o
launch (Task Manager / Process Explorer) — inerente ao protocolo
`startbypatcher`, que exige `user:`/`pwd:` via args. Não há outro canal.
Registrar em `doc/04-arquitetura.md` como risco aceito.

## Aceite

- [ ] Nenhum `.lnk` em `%AppData%\PwAssistant\clients\` contém `pwd:`.
- [ ] 2 Plays (classes iguais e diferentes) → 2 botões separados na
      taskbar, cada um com o ícone da classe.
- [ ] `dotnet build` 0/0 + `dotnet test` verde (inclui `LoggingTests`
      atualizados).
