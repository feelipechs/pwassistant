# HANDOFF — estado em 2026-09-25, base 1c7559e (trabalho não commitado sobre ela)
## Pronto (validado)
- Build 0 erros/warnings; testes 78/78 (71 + 7 novos: redactor, scrub/cleanse, retenção).
- Fases A–E implementadas e verdes, sem commit (aguardando pedido): `.lnk` só-identidade + scrub + cleanse no startup, redactor `pwd/user/role` case-insensitive, retenção 30d/50MB, hardening C (6 itens), `IDialogService` + `ClientWindowMarker` (VMs sem Window/WinApi), Velopack 1.0.1 + `Main()` customizado + `AppUpdater` (feed `feelipechs/ditto`).
- `vpk pack` verificado localmente: `Ditto.PwAssistant-win-Setup.exe` + delta gerados, sem assinatura.
- Planos em `plans/` (00, A–F); `doc/04` com U19/U20; `doc/01` DPAPI corrigido.
## Pronto (código, pendente de jogo/Windows)
- Tudo acima + instalador: instalar `Setup.exe` em máquina limpa, update delta fim-a-fim, regressão dos sheets, 2 Plays → 2 botões taskbar, `.lnk` sem `pwd:`.
## Próximo passo
- Commitar por fase (A→E + docs) com pedido explícito; depois validação in-game + release.
## Backlog pós-validação (ponteiro p/ `doc/07-backlog.md`)
- Ver arquivo; U9-futuro (grade do Mini) aberto.
## Perguntas abertas
- Certificado de code signing (pago) vs conviver com SmartScreen — decisão pendente.
- Risco residual documentado: senha visível na linha de comando durante o launch (protocolo `startbypatcher`).
