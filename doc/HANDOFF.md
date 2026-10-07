# HANDOFF — estado em 2026-10-07, base 724d693 (+fix foco não commitado)
## Pronto (validado)
- Build solution 0 erros/0 warnings; testes 121/121 (Core 92 + Avalonia 29).
- **Freeze do jogo resolvido (causa raiz provada por intervenção):**
  1. Disparos sobrepostos (3 loops BAU + manual) compartilhavam
     `_batchActive`/`_worker` no singleton `FocusedInputStrategy` — o
     `EndBatch` de um jogava o outro no caminho legado (priming +
     higiene `WA_INACTIVE`) = client "congelado". Fix: escopo por
     disparo via `AsyncLocal<BatchScope>` (só esse arquivo; loops,
     dispatcher e sender intactos). `fires=N` no log `[mode]` prova a
     sobreposição (visto até 4).
  2. Sync click usava o legado com higiene e congelava as desfocadas
     (EA/SK, HWNDs confirmadas no log; WR focada intacta). Fix: 1 linha
     no DI — `PostMessageBackgroundStrategy(applyHygiene: false)`
     (priming mantido, só a cauda `WA_INACTIVE` removida; worker/sender
     intocados, sender nunca teve higiene — correção ao CONTEXTO antigo).
  3. Prova: loops + sync com **zero `HYGIENE`** no log + zero freeze
     (antes: `HYGIENE` em alvos de loop durante loops).
- Testes novos `FocusedInputStrategyTests` (5): sobreposição isolada
  (falha no código antigo com o vazamento exato), sync fora de batch
  imediato, buffer-silencioso-até-flush, `EndBatch` sem `Begin` no-op,
  modo `legacy-anomaly`. Mutação verificada (antigo falha, novo passa).
- Descoberta no caminho: `GetForegroundLockTimeoutMs()` é volátil
  (launchers reescrevem em runtime) — testes fixam via seam interno
  `TestLockTimeoutMs` + `InternalsVisibleTo` (sem mudança de
  comportamento; produção segue amostrando por disparo).
- `LoopController`: log por iteração (`loop iter=N` + estado por conta)
  reaplicado — foi o que permitiu o diagnóstico; manter.
## Pronto (código, pendente de jogo/Windows)
- Teste 100% do baú (F7/Y, F8/Y e teclas longas) — usuário fará depois.
- Passo 2 de reserva (só se o freeze voltar): sync pelo click limpo
  `SendUiClickCleanAsync` (sem priming). Higiene-off já provado basta.
- Lote visual revertido segue pendente (spinner-arco, blink, rename,
  wheel-guard, ms, mini compacto) — usuário prepara a lista.
## Próximo passo
- Usuário: lista de ajustes de UI → lote visual. Commit do fix de foco
  só com pedido explícito (pendente).
## Backlog pós-validação (ponteiro p/ `doc/backlog.md`)
- Inalterado. Dívida nova: `logs/FocusedInputStrategy.cs` + `.patch`
  do agente externo (gitignored, apagar após incorporar de vez).
- `codigo-teste/CONTEXTO.md` atualizado com o desfecho (sender bare,
  patch, decisão da higiene).
## Perguntas abertas
- Nada. Sem commit (pedido explícito pendente).
