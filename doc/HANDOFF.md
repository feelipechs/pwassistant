# HANDOFF — estado em 2026-10-01, base 83dcd19
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
- Descobertas lei nº 5: engine ignora PostMessage sem foreground sob
  carga (A/B + logs); resposta do dev (SetForeground sem Ctrl/input
  junto; bloqueio do lock é o caso background); releitura: a receita
  tem duas metades inseparáveis (com-Ctrl força com input, sem-Ctrl
  bloqueia sem input) — meu erro foi remover a chamada em vez do grant.
- Prova cruzada no log (2 contas, 5 disparos): executor segue o
  foreground com as receitas atuais; `[fg]` estável provou o fix
  sem-troca da build anterior.
## Pronto (código, pendente de jogo/Windows)
- Fase 4b (`66e5c31`): foco forçado + restore, `FocusedDispatch` off.
- Receita limpa (`83dcd19`, com doc): soft SetForeground por conta +
  `setfg=1/0` no verbose + envios nus + restore condicional; flag de
  sessão `CleanDispatch`. A/B previsto: botão (com grant, setfg=1)
  vs. hotkey/loop (sem grant, setfg=0).
- APPTESTE republicado (pasta, `APPTESTE.exe`, zip ~66 MB): `83dcd19`.
## Próximo passo
- A/B botão vs. hotkey no PC fraco (nunca junto do instalado):
  verbose + RECEITA LIMPA, tecla e click. Hotkey 2/2 sem troca =
  bypass fiel reproduzido; falha mesmo com setfg=0 = foco forçado
  vira o único caminho.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
