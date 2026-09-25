---
description: Atualiza docs de contexto após mudança de comportamento descoberto
agent: build
---

Quando uma descoberta mudar comportamento documentado:

1. Atualize primeiro `doc/architecture.md` e/ou `doc/business-rules.md` (normativos).
2. Registre o resultado datado em `doc/milestones.md`.
3. Acrescente entrada datada em `doc/HANDOFF.md` (histórico de sessão).
4. Só então ajuste código — nunca o inverso (regra do `AGENTS.md`).
5. Resuma as mudanças para revisão antes de qualquer commit (commits só com pedido explícito).
