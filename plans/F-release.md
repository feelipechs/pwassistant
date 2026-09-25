# Fase F — validação e release (checklist)

## Validação técnica

- [ ] `dotnet build pwassistant.sln` 0 erros/warnings.
- [ ] `dotnet test` verde (71 + novos das Fases B–D).
- [ ] `git status` limpo; `git stash` vazio.

## Validação in-game (Windows, foco fora do jogo)

- [ ] Ação de baixo risco primeiro (F1 montaria / UI reversível),
      countdown ≥ 3 s.
- [ ] T0 puro falha sem foco (se passar, parar e investigar).
- [ ] Regressão dos sheets (Fase D): cada sheet aberta uma vez.
- [ ] Taskbar: 2 Plays → 2 botões com ícones (Fase A).
- [ ] Nenhum `.lnk` com `pwd:`; logs sem segredo.

## Docs e commits

- [ ] Descoberta que muda comportamento atualiza `doc/` no mesmo
      passo (lei nº 5); carimbo em `doc/06-marcos.md`.
- [ ] Commits por bloco (Conventional Commits, inglês, imperativo),
      **só com pedido explícito**; um `docs:` separado p/ HANDOFF.
- [ ] Reescrever `doc/HANDOFF.md` no template do `AGENTS.md`.

## Release (só com pedido explícito)

- [ ] `push` + GitHub Release com `Setup.exe` + nota SmartScreen.
