# HANDOFF — estado em 2026-10-01, base 326db76
## Pronto (validado)
- Build 0 erros/warnings; testes 79/79.
- Lote 3 editor (`0990ffd`, validado no Windows): warning dock Top,
  "MODO SEQUENCIAL" removido, badge ⓘ de órfão, foco sem puxão.
- **Clicks: sonho realizado** — SEGUIR/ACEITAR PT executam sem perder
  foco (R2 validada: zero chamada no caminho de click). Modelo 1 provado
  p/ clicks, sem asterisco.
- Descobertas lei nº 5: A/B fg; dev (duas metades); timeout efetivo ~1
  ms aqui (negado irreproduzível localmente); 2ª opinião cruzada com o
  binário (higiene co-fator; frame-sampling líder p/ teclas; sem KEYUP
  em todo o binário; teclas = PostMessageW mesmo HWND).
## Pronto (código, pendente de jogo/Windows)
- Fase 4b (`66e5c31`): foco forçado + restore, `FocusedDispatch` off.
- Receita limpa R2 (`58dcae9`): soft só-teclas + por tecla; `[fg]`/
  `[bg]` com `gui`/`focus`/`pumpMs`; matches classe+título; Probe
  `--hold`; fix re-registro hotkeys.
- Teclas puras (`326db76`, com doc): `SendKeyPureAsync` (KEYDOWN
  lParam=0, sem UP) + caminho sem chamada/gap/restore + linha `[pure]`
  + flag `PureBackgroundKeys` ("TECLAS 100% BACKGROUND (TESTE)", exige
  Clean) + blindagem `matches[0]` por classe. Célula sem-chamada+exato:
  nunca testada — é o próximo tiro.
## Próximo passo
- Probe 2×2 bg-only (hold 50/400 × higiene, 10/célula) + teste P2
  (TESTE F3 + RECEITA LIMPA + TECLAS PURAS, foco no main, toggle só;
  se prender, um tap físico solta). Zero troca + executa = teclas
  fechadas; zero troca + ignora = negado (irmão/timeout) ou foco.
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Ver arquivo; ambientes prod/dev/test aguardando explicação do usuário.
## Perguntas abertas
- Certificado de code signing (pago) vs SmartScreen — pendente.
