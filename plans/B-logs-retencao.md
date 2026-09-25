# Fase B — retenção de logs (30 dias + cap)

## Problema

`Core/Ux/FileLogger.cs:30-38` só faz `CreateDirectory` + `AppendAllText`
em `%AppData%\PwAssistant\logs\app-yyyy-MM-dd.log`. Sem rotação, sem
teto, sem limpeza — acumula indefinidamente (e logs carregam logins).

## Mudança

1. `FileLogger`: na inicialização (antes do primeiro `Write`), rodar
   limpeza best-effort (nunca quebra o app se falhar):
   - apagar `app-*.log` com mais de **30 dias**;
   - teto total de **50 MB**: se exceder, apagar os mais antigos até
     ficar abaixo do teto.
2. Manter `LogRedactor` em todo `Write` (Fase A já o alargou).
3. Testes novos (`LoggingTests` ou `FileLoggerRetentionTests`):
   - arquivo com +30 dias é removido;
   - excedente acima do cap remove os mais antigos primeiro;
   - falha de IO na limpeza não derruba o logger.

## Não-escopo

Níveis de log / verbosity switch (só `Info,Warn,Error` hoje) — backlog.

## Aceite

- [ ] Pasta de logs respeita 30 dias + 50 MB após reinício do app.
- [ ] `dotnet build` 0/0 + `dotnet test` verde.
