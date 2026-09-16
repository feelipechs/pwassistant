# Pesquisas e Validações — a fazer no Windows

> **Status (2026-09-16):** item 1 **concluído** (T1/C4 validados — ver resultado
> no item e `HANDOFF.md`). Itens 2–5 seguem como checklist de validação dos
> marcos M5/M6 (`06-marcos.md`); em 2026-09-16 foi removido texto de resultado
> copiado por engano do item 1 nesses itens. Este arquivo continua necessário
> até lá.

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
- **Resultado (2026-09-10, `teste-bateria.ps1`):** `PostMessage` puro SEM foco falha (T0), mas
  funciona SEM foco com priming de ativacao (T1: `WM_ACTIVATE/WA_ACTIVE` + `WM_SETFOCUS` +
  `WM_ACTIVATEAPP` antes de `WM_KEYDOWN/UP` com scan code via `MapVirtualKey`). Testado com F1
  (montaria) em `elementclient_64` real, foco fora do jogo o tempo todo. T2 (DOWN+CHAR+UP),
  T3 (hold/repeat) e T5 (PostThreadMessage) nao funcionaram sem foco. A janela do jogo nao tem
  filhas (`ElementClient Window` e o proprio alvo). Higiene do flag (devolver `WA_INACTIVE`,
  `teste-higiene.ps1`) em validacao. Conclusao: `IInputStrategy` principal = `PostMessage` com
  priming; sem driver, sem foco real, sem flicker.

## 2. `startbypatcher` — comportamento real

- [ ] Confirmar que múltiplas instâncias do `elementclient.exe` com
      `startbypatcher` abrem processos independentes (PIDs distintos) sem
      conflito.
- [ ] Confirmar se existe algum limite de instâncias simultâneas imposto
      pelo client ou pelo servidor privado.
- [ ] Verificar comportamento se `role:NICK` não existir ainda na conta
      (primeiro login) — cai em tela de criação de personagem?
- **Resultado:** pendente — validar no Windows no marco M5
  (`06-marcos.md`).

## 3. Identificação de janela por processo

- [ ] Validar que dá para mapear `ProcessId` → `Hwnd` de forma confiável
      logo após o spawn (pode haver delay entre o processo iniciar e a
      janela principal aparecer — mapear esse tempo).
- [ ] Verificar se o título da janela muda com base no `role` (nick) —
      isso pode simplificar exibição na UI mesmo sem depender só do PID.
- **Resultado:** pendente — validar no Windows no marco M5
  (`06-marcos.md`).

## 4. Overlay de captura de posição

- [ ] Validar que um overlay transparente por cima da janela do jogo não
      interfere no client (ex.: jogo capturando o click do overlay em vez
      do overlay capturar).
- **Resultado:** pendente — validar no Windows no marco M5
  (`06-marcos.md`).

## 5. Antivírus / falso positivo

- [ ] Testar se o executável gerado (self-contained .NET) dispara alerta
      de Windows Defender/AV por causa do uso de `PostMessage`/simulação
      de input. Se sim, avaliar assinatura de código ou ajuste de
      publish settings.
- **Resultado:** pendente — validar no Windows no marco M6
  (`06-marcos.md`).

## Como usar este documento

Depois de cada rodada de teste no Windows, atualizar o campo
**Resultado** de cada item com o que foi observado. Isso vira a base para
decidir, com dados reais, qual `IInputStrategy` implementar primeiro (ver
`02-motor-de-macro.md`).
