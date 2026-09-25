# HANDOFF — estado em 2026-09-25, base 9f9aae4
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Rename `ditto` → `pwassistant` commitado (`9440254`): props, testes,
  FeedUrl, remote, títulos; repo GitHub renomeado (verificado via HTTPS).
- Docs commitados (`9f9aae4`): README de produto, `ui-audit.md` removido,
  carimbos U15–U20 + instalador + rename no `doc/06`.
- 1.0.0 local (`Ditto.PwAssistant`) desinstalado antes do rename.
## Pronto (código, pendente de jogo/Windows)
- Repack 1.0.0 com `packId PwAssistant` + install local + Release v1.0.0.
- Update delta 1.0.1 fim-a-fim; validação in-game das fases A–E.
## Próximo passo
- `vpk pack --packId PwAssistant --packVersion 1.0.0` e install local.
## Backlog pós-validação (ponteiro p/ `doc/07-backlog.md`)
- Ver arquivo; U9-futuro (grade do Mini) aberto.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
- SSH do remote negado nesta máquina (`git@github.com: Permission denied`);
  push/release podem exigir HTTPS ou chave.
