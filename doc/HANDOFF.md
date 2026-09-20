# HANDOFF — estado em 2026-09-20, base cd54b9d

## Pronto (validado)

- M1–M6, B1/B1b/B2/B2d/B3/B4/B4-config, B5, taskbar (2 botões + ícones,
  classes iguais e diferentes), loop 1 s, fechar-X desliga modos,
  apelido/classe, copiar + auto-clear 30 s, mini 1-clique (NOACTIVATE),
  destaque ~250 ms, modo por preset + `i/n (Role)`, Fire⇄Parar,
  Play⇄Stop, gravação livre de tecla, Save/descarta, mini só-online.
- Build 0 erros/warnings em `%TEMP%\ditto-build`; testes 52/52.
- `DllImport`/COM só em `PwAssistant.WinApi` (lei nº 2).

## Pronto (código, pendente de jogo/Windows)

- Publish Release self-contained + teste AV/Defender dedicado.
- `images/` original (PNGs/ICOs do usuário) untracked na raiz.

## Próximo passo

Nova sessão: B4-config já validado pelo usuário (“tudo funcional”);
restam polimentos (dnd condicional, auto-refresh do grid por evento,
visibilidade da senha, ícone do exe a partir dos ICOs).

## Backlog pós-validação (ponteiro p/ `doc/07-backlog.md`)

Ver arquivo; B4-config carimbado; polimentos acima + Play→Stop feito.

## Perguntas abertas

- Nenhuma bloqueante.
