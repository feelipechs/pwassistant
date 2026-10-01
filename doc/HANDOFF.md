# HANDOFF — estado em 2026-10-01, base 58dcae9
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
- Descobertas lei nº 5: engine ignora PostMessage sem foreground sob
  carga (A/B + logs); resposta do dev; releitura grant (duas metades);
  timeout efetivo ~1 ms aqui (caso negado irreproduzível localmente).
- 2ª opinião (doc em `logs/`, cruzada com binário): higiene rebaixada
  a co-fator; frame-sampling é a hipótese líder p/ teclas; R2 do
  binário (clicks sem foco, teclas com); sem `KEYUP` em todo o binário
  (teclas = `PostMessageW` mesmo HWND, zero `SendMessage`).
## Pronto (código, pendente de jogo/Windows)
- Fase 4b (`66e5c31`): foco forçado + restore, `FocusedDispatch` off.
- Receita limpa R2 (`58dcae9`, com doc): soft call só em teclas +
  por tecla (clicks puros); `[fg]`/`[bg]` com `gui`/`focus`/`pumpMs`
  (`GetGUIThreadInfo` + `WM_NULL`); `matches` classe+título no
  firelog verboso; Probe `--hold` p/ o 2×2; **fix re-registro de
  hotkeys** (`AppState.HotkeysChanged` + log de registro).
- APPTESTE republicado (pasta, `APPTESTE.exe`, zip ~66 MB): `58dcae9`.
- Down-only exato rebaixado: só se hold-400 falhar. Bit-24 e DPI no
  backlog (não explicam o atual).
## Próximo passo
- Sem build (usuário, 2 min): SPI efetivo antes/depois do Helper.
- Probe 2×2 bg-only (hold 50/400 × higiene, 10/célula) + 10 clicks
  limpos puros no app. Matriz decide: frame-sampling vs higiene vs H1.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
