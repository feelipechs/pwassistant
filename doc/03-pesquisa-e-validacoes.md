# Pesquisas e Validações — a fazer no Windows

Este documento é o checklist do que precisa ser testado na prática antes
de fechar decisões de arquitetura. Atualizar conforme os testes forem
feitos (resultado, data, observações).

## 1. PostMessage sem foco funciona no engine do PW?

**Por quê importa:** define se execução simultânea real é possível, e se
`IInputStrategy` pode ser só `PostMessage` ou precisa de fallback com
foco.

- [ ] Testar `WM_KEYDOWN`/`WM_KEYUP` postado numa janela sem foco → uma
      skill é executada?
- [ ] Testar `WM_LBUTTONDOWN`/`WM_LBUTTONUP` postado numa janela sem foco
      → um click no chão/alvo funciona?
- [ ] Testar em cenário simples primeiro: aceitar diálogo de convite de
      grupo (ação de baixo risco, fácil de verificar visualmente).
- [ ] Se falhar: testar `SendInput` com alternância de foco programática
      (`SetForegroundWindow` + delay pequeno) como fallback.
- **Resultado:** _(preencher depois do teste)_

## 2. `startbypatcher` — comportamento real

- [ ] Confirmar que múltiplas instâncias do `elementclient.exe` com
      `startbypatcher` abrem processos independentes (PIDs distintos) sem
      conflito.
- [ ] Confirmar se existe algum limite de instâncias simultâneas imposto
      pelo client ou pelo servidor privado.
- [ ] Verificar comportamento se `role:NICK` não existir ainda na conta
      (primeiro login) — cai em tela de criação de personagem?
- **Resultado:** _(preencher depois do teste)_

## 3. Identificação de janela por processo

- [ ] Validar que dá para mapear `ProcessId` → `Hwnd` de forma confiável
      logo após o spawn (pode haver delay entre o processo iniciar e a
      janela principal aparecer — mapear esse tempo).
- [ ] Verificar se o título da janela muda com base no `role` (nick) —
      isso pode simplificar exibição na UI mesmo sem depender só do PID.
- **Resultado:** _(preencher depois do teste)_

## 4. Overlay de captura de posição

- [ ] Validar que um overlay transparente por cima da janela do jogo não
      interfere no client (ex.: jogo capturando o click do overlay em vez
      do overlay capturar).
- **Resultado:** _(preencher depois do teste)_

## 5. Antivírus / falso positivo

- [ ] Testar se o executável gerado (self-contained .NET) dispara alerta
      de Windows Defender/AV por causa do uso de `PostMessage`/simulação
      de input. Se sim, avaliar assinatura de código ou ajuste de
      publish settings.
- **Resultado:** _(preencher depois do teste)_

## Como usar este documento

Depois de cada rodada de teste no Windows, atualizar o campo
**Resultado** de cada item com o que foi observado. Isso vira a base para
decidir, com dados reais, qual `IInputStrategy` implementar primeiro (ver
`02-motor-de-macro.md`).
