# HANDOFF — estado em 2026-10-01, base 12111e4
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
- Descobertas lei nº 5: engine ignora PostMessage sem foreground sob
  carga (A/B + logs); resposta do dev (SetForeground sem Ctrl/input
  junto; bloqueio do lock é o caso background); paradoxo do grant:
  hotkey com protocolo estrito ainda dá setfg=1 (debugger e admin
  descartados; lock em default) — causa raiz ainda aberta.
## Pronto (código, pendente de jogo/Windows)
- Fase 4b (`66e5c31`): foco forçado + restore, `FocusedDispatch` off.
- Receita limpa (`83dcd19`): soft SetForeground por conta + `setfg=1/0`
  + envios nus + restore condicional; flag de sessão `CleanDispatch`.
- Instrumento (`12111e4`): `[batch] prev + owner + dbg + admin` no
  verbose — arbitra a causa do grant sem inferência.
- APPTESTE republicado (pasta, `APPTESTE.exe`, zip ~66 MB): `12111e4`.
- Bug real encontrado no caminho: hotkeys só registram no startup
  (`HotkeysChanged` sem assinante) — fix pendente.
## Próximo passo
- Reteste estrito no dev com o instrumento: foco no main → 2 s →
  hotkey; ler `[batch]` + `setfg` + quem executou. Depois: fix do
  re-registro de hotkeys + decisão do default.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
