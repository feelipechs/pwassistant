# Finalização — overview (base `1c7559e`)

Objetivo: tornar o app distribuível para outras pessoas — seguro, com
instalador + updater — sem regressão no que já funciona.

## Ordem (sequencial, cada fase só começa com a anterior verde)

1. **A — `.lnk` sem senha** (`plans/A-lnk-sem-senha.md`): bloqueador de
   segurança. Sem ela, não distribuir.
2. **B — retenção de logs** (`plans/B-logs-retencao.md`): 30 dias + cap.
3. **C — hardening** (`plans/C-hardening.md`): refactors pequenos e seguros.
4. **D — desacoplamento VM** (`plans/D-vm-decoupling.md`): `IDialogService`;
   reforma maior, isolada em commits próprios.
5. **E — instalador + updater** (`plans/E-velopack.md`): Velopack + GitHub
   Releases, sobre base já segura.
6. **F — validação e release** (`plans/F-release.md`): checklist final.

## Regras globais (valem p/ todas as fases)

- `dotnet build pwassistant.sln` 0 erros/warnings + `dotnet test` verde
  antes e depois de cada bloco.
- Código em inglês, UI em pt-BR via `.resx` (convenção do repo).
- Segredos nunca em texto puro (disco, log, exceção) — lei nº 4.
- Todo P/Invoke continua em `PwAssistant.WinApi` — lei nº 2.
- Sem commits sem pedido explícito; sem push sem pedido explícito.
- Mudança de comportamento só onde o plano manda; refactor é mecânico.
