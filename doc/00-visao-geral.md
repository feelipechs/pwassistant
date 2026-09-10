# PW Launcher/Helper — Visão Geral

## Objetivo

Ferramenta desktop pessoal para gerenciar múltiplas contas do Perfect World
(servidor privado) e automatizar ações repetitivas (macros de click/tecla)
em várias contas simultaneamente. Inspirada no PW Helper, mas com escopo
próprio e sem as limitações de funcionalidades pagas.

## Escopo inicial (v1)

- Login automático via `startbypatcher` (multi-conta, multi-servidor)
- CRUD de servidores e contas
- Gravação e execução de macros (click/tecla) por conta
- Modo Grupo com Presets (ação configurável por conta, disparo em botão)

Fora de escopo por enquanto (features do PW Helper que não são prioridade
agora): mapa interativo, auto forja, teste de latência, barra de ferramentas
substituindo a do Windows, backup automático.

## Stack escolhida

**C# / .NET 8 + WPF**

Motivos (contexto: dev vindo de Java/JavaScript, sem experiência prévia em
C#, mas com IA gerando a maior parte do código):

- Sintaxe e OOP próximos de Java → curva de aprendizado baixa para revisar
  o código gerado.
- P/Invoke para WinAPI (`PostMessage`, `SendMessage`, `FindWindow`,
  `EnumWindows`, `GetWindowRect`, `ScreenToClient`) é maduro e tem grande
  volume de exemplos vindos de ferramentas de automação de MMORPG.
- WPF permite GUI real (data binding, MVVM) sem gambiarra — importante
  porque o app tem bastante superfície de UI (cards de conta, grid de
  grupo, editor de presets).
- Distribuição como `.exe` self-contained, sem depender de runtime externo
  e com menor chance de falso-positivo de antivírus do que executáveis
  empacotados via PyInstaller (relevante porque a ferramenta faz
  `PostMessage`/simulação de input, comportamento que AVs costumam
  observar de perto).

Alternativas consideradas e descartadas por ora: AutoHotkey v2 (ótimo para
macro pura, mas GUI fraca para o CRUD/Modo Grupo), Python + pywin32
(prototipável no Linux, mas distribuição pior e menos referência
específica para esse nicho).

## Ambiente de desenvolvimento

- Geração de código: **opencode**, rodando localmente.
- Testes e validação de automação de janela: **somente no Windows**
  (WinAPI e o próprio jogo não rodam no Linux). O trabalho no Linux se
  limita a estruturar código, documentação e lógica que não dependa de
  WinAPI/execução real do jogo.
- Por isso, antes de fechar a arquitetura completa, o próximo passo é
  validar no Windows as premissas técnicas de mais risco — ver
  `03-pesquisa-e-validacoes.md`.

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
