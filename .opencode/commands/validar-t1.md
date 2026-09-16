---
description: Revalida a técnica T1 (tecla sem foco) contra o jogo real
agent: build
---

Revalide a técnica T1 de input sem foco (skill `pw-winapi`) contra o jogo real:

1. Confirme 2 `elementclient_64` rodando e liste HWNDs (modo `-List` de `tools/provas-winapi/teste-postmessage-v2.ps1` — só leitura).
2. Peça ao usuário para rodar `tools/provas-winapi/teste-bateria.ps1 -Variant T1 -TargetPid <PID> -Key F1 -Countdown 6` com foco fora do jogo e reportar (montaria toggle).
3. Se falhar, PARE: trate como regressão de engine — investigue antes de qualquer código (ver `doc/03-pesquisa-e-validacoes.md` item 1).
4. Registre o resultado (data + PIDs) no marco correspondente em `doc/06-marcos.md`.
