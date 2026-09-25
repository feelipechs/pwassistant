---
name: pw-winapi
description: Receita validada de input sem foco no elementclient do PW via PostMessage com priming (teclas T1, UI-clicks C4, higiene WA_INACTIVE) e armadilhas conhecidas
---

# pw-winapi — input sem foco no Perfect World (elementclient)

Usar sempre que a tarefa envolver enviar input ao jogo, P/Invoke de WinAPI,
`IInputStrategy`, `MacroExecutor` ou depurar "não funcionou sem foco".
Contrato completo: `doc/architecture.md`. Provas originais aposentadas
(histórico no git); revalidar via `dotnet run --project src/PwAssistant.Probe`.

## Receita de tecla (T1 — validada 3/3, 2 PIDs, sem foco)

Alvo: HWND top-level classe `ElementClient Window`, resolvido por
`EnumWindows` + `GetWindowThreadProcessId` (o jogo NÃO tem filhas;
`MainWindowHandle` cacheado é proibido como fonte).

1. Prime: `PostMessage(hwnd, WM_ACTIVATE, WA_ACTIVE, 0)` → 30 ms →
   `WM_SETFOCUS, 0, 0` → 30 ms → `WM_ACTIVATEAPP, 1, 0` → 30 ms.
2. `scan = MapVirtualKeyW(vk, MAPVK_VK_TO_VSC /* 0 */)`.
3. `WM_KEYDOWN, vk, 1 | (scan << 16)` → 50 ms →
   `WM_KEYUP, vk, DOWN | (1<<30) | (1<<31)`.
4. Higiene opcional: `WM_ACTIVATE, WA_INACTIVE, 0` + `WM_ACTIVATEAPP, 0, 0`.

## Receita de UI-click (C4 — validada: popup grupo, Sim/Não, sem foco)

Coordenada em **client area** (`MAKELPARAM = (y<<16)|(x&0xFFFF)`):

1. `WM_NCHITTEST, 0, lp` → 20 ms → `WM_MOUSEMOVE, 0, lp` → 20 ms →
   `WM_SETCURSOR, hwnd, HTCLIENT | (WM_LBUTTONDOWN<<16)` → 20 ms.
2. `WM_LBUTTONDOWN, MK_LBUTTON, lp` → 50 ms → `WM_LBUTTONUP, 0, lp`.

## Limites (não tentar furar sem nova prova)

- Click no **chão 3D** não passa sem foco. `PostThreadMessage`, `DOWN+CHAR+UP`
  e hold/repeat também falham sem foco (variantes T2/T3/T5 refutadas).
- Sem driver, sem injeção, sem `SendInput` padrão (descartados com prova).
- `PostMessage != 0` só prova entrega, não efeito.

## Armadilhas

- `lParam = 0` nunca (exige scan code). `$Pid` é reservado no PowerShell.
- Validar repetibilidade com toggle (F1 montaria): skills têm cooldown e
  falseiam "falhou". Controles: T0 puro deve falhar sem foco.
- Validação sempre com foco fora do jogo, countdown ≥ 3 s, baixo risco primeiro.
- Coordenadas de click: client area (converter via `ScreenToClient`), de
  preferência relativas (0–1) para tolerar janelas distintas.
