---
description: Revalida a técnica T1 (tecla sem foco) contra o jogo real
agent: build
---

Revalide a técnica T1 de input sem foco (skill `pw-winapi`) contra o jogo real:

1. Confirme 2 `elementclient_64` rodando (Gerenciador de Tarefas) e anote os PIDs.
2. Peça ao usuário para rodar `dotnet run --project src/PwAssistant.Probe -- key --pid <PID> --key F1` com foco fora do jogo e reportar (montaria toggle).
3. Se falhar, PARE: trate como regressão de engine — investigue antes de qualquer código (ver receita T1 em `doc/architecture.md`).
4. Registre o resultado (data + PIDs) no marco correspondente em `doc/milestones.md`.
