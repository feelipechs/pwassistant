# HANDOFF — estado em 2026-10-01, base 352942c
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
- Log do irmão (v1.1.0, 30/09): entrega 100% ok (zero offline,
  zero PostMessage=false) — skips são jogo-ignora-mensagem; debounce
  comia os retries manuais dele.
## Pronto (código, pendente de jogo/Windows)
- Fix (`352942c`, Release v1.1.1 empacotada): debounce 150 ms; label
  Abrir/Fechar atualiza no rebuild; morte assíncrona (sem Invoke
  bloqueante), bulk com 1 refresh/toast/foco suprimido; guarda
  single-launch por model; toasts INICIADO/ENCERRADO + barra verde.
- Lote A segue em v1.1.0/v1.1.1 instalado no PC do irmão.
## Próximo passo
- Irmão testa v1.1.1 (travamento do Fechar Todas + debounce curto).
  Teste de delay 1 s entre skills pendente (dele) → decide Fase 4b
  (gap global vs receita Helper) e Fase 1 batch save.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
