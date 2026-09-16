# PW Launcher/Helper — Visão Geral

## Objetivo

Ferramenta desktop pessoal para gerenciar múltiplas contas do Perfect World
(servidor privado) e automatizar ações repetitivas (macros de click/tecla)
em várias contas simultaneamente. Projeto próprio, escopo definido abaixo.

## Escopo inicial (v1)

- Login automático via `startbypatcher` (multi-conta, multi-servidor)
- CRUD de servidores e contas
- Gravação e execução de macros (click/tecla) por conta
- Modo Grupo com Presets (ação configurável por conta, disparo em botão)

Fora de escopo por enquanto: mapa interativo, auto forja, teste de latência, barra de ferramentas
substituindo a do Windows, backup automático.

## Stack escolhida

**C# / .NET 8 + WPF**

Motivos:

- P/Invoke para WinAPI (`PostMessage`, `SendMessage`, `FindWindow`,
  `EnumWindows`, `GetWindowRect`, `ScreenToClient`) é maduro e tem grande
  volume de exemplos vindos de ferramentas de automação de MMORPG.
- WPF permite GUI real (data binding, MVVM) sem gambiarra — importante
  porque o app tem bastante superfície de UI (cards de conta, grid de
  grupo, editor de presets).
- Distribuição como `.exe` self-contained, sem depender de runtime externo
  e com menor chance de falso-positivo de antivírus do que executáveis
  empacotados por empacotadores genéricos (relevante porque a ferramenta faz
  `PostMessage`/simulação de input, comportamento que AVs costumam
  observar de perto).

Alternativas consideradas e descartadas: ferramentas de macro com GUI
limitada para o CRUD/Modo Grupo e stacks com distribuição inferior a um
`.exe` self-contained.

## Ambiente de desenvolvimento

- Geração de código: **opencode**, rodando localmente.
- Testes e validação de automação de janela: **somente no Windows**
  (WinAPI e o próprio jogo não rodam no Linux). O trabalho no Linux se
  limita a estruturar código, documentação e lógica que não dependa de
  WinAPI/execução real do jogo.
- Por isso, antes de fechar a arquitetura completa, o próximo passo é
  validar no Windows as premissas técnicas de mais risco — ver
  `03-pesquisa-e-validacoes.md`.
- **Status (2026-09-16):** arquitetura fechada em `04-arquitetura.md`
  (premissas do item 1 do `03` validadas); código M1–M6 implementado,
  aceite contra o jogo pendente no Windows (`06-marcos.md`).

## Camadas do projeto (proposta)

```
PwHelper.Core     → models, lógica de negócio, storage (sem dependência de WinAPI)
PwHelper.WinApi   → wrapper isolado de P/Invoke (FindWindow, PostMessage, etc.)
PwHelper.App      → WPF, telas, viewmodels (MVVM)
```

Separar `WinApi` do `Core` é proposital: se a estratégia de input mudar
(ex.: `PostMessage` não funcionar em algumas telas do jogo e for preciso
cair para foco real + alternância rápida de janela), a troca fica isolada
nessa camada sem mexer no resto do app.
