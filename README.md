# PwAssistant

[![Release](https://img.shields.io/github/v/release/feelipechs/pwassistant)](https://github.com/feelipechs/pwassistant/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Launcher multi-conta + assistente de macros para Perfect World (servidor
privado). Input sem foco via `PostMessage` com priming de ativação —
sem driver, sem injeção, sem `SendInput` como padrão.

## Instalação

1. Baixe o `PwAssistant-win-Setup.exe` em
   https://github.com/feelipechs/pwassistant/releases.
2. Rode — instala por usuário, sem perguntas, e abre sozinho.
3. Sem certificado de code signing: o Windows SmartScreen vai avisar
   na primeira execução. É esperado até o instalador ganhar reputação.

## Uso

1. Cadastre o servidor apontando o `elementclient.exe` **dentro da
   pasta x64**, adicione as contas e dê Play.
2. Modo Grupo: Fire dispara o preset; Sync replica clicks; Mini é o
   controle compacto durante o jogo.
3. Foco fora do jogo ao disparar; ação de baixo risco primeiro
   (F1 montaria).

## Segurança

- Suas senhas ficam criptografadas no próprio PC — o app nunca as
  salva em texto puro, em logs ou em atalhos.
- Logs locais não contêm dados sensíveis.

## Atualizações

Automáticas via delta: quando houver release nova o app avisa em
português, baixa só a diferença, aplica e reinicia sozinho. A versão
instalada aparece no canto da janela principal, ao lado das
configurações.

## Documentação

Mapa rápido: `doc/overview.md` (comece aqui) →
`doc/architecture.md` (decisões) → `doc/milestones.md` (aceites) →
`doc/backlog.md` (futuro) → `doc/release.md` (publicar).
Regras de contribuição e sessão em `AGENTS.md`.

## Licença

MIT — ver `LICENSE`.
