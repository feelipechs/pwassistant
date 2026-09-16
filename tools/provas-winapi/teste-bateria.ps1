# teste-bateria.ps1 -- COPIAR PARA C:\Users\chagass\Documents\teste-pw\
# Bateria diferencial: descobre QUAL diferenca faz o engine aceitar input sem foco.
# T0 = baseline (DOWN/UP com scan code) | T1 = priming WM_ACTIVATE/SETFOCUS | T2 = DOWN+CHAR+UP
# T3 = hold/repeat | T5 = PostThreadMessage | C0 = click baseline | C4 = click com priming de mouse
#
# Exemplos (janela do jogo SEM foco, olhe o jogo sem clicar nele):
#   .\teste-bateria.ps1 -Variant T0 -TargetPid 11708 -Key SPACE
#   .\teste-bateria.ps1 -Variant T1 -TargetPid 11708 -Key SPACE
#   .\teste-bateria.ps1 -Variant T2 -TargetPid 11708 -Key SPACE
#   .\teste-bateria.ps1 -Variant T3 -TargetPid 11708 -Key F1
#   .\teste-bateria.ps1 -Variant T5 -TargetPid 11708 -Key SPACE
#   .\teste-bateria.ps1 -Variant C0 -TargetPid 11708 -ClickX 400 -ClickY 300
#   .\teste-bateria.ps1 -Variant C4 -TargetPid 11708 -ClickX 400 -ClickY 300
param(
    [string]$Variant = "T0",
    [int]$TargetPid = 0,
    [string]$Key = "SPACE",
    [int]$ClickX = 400,
    [int]$ClickY = 300,
    [int]$Countdown = 3
)
if (-not ("PwBatWin32" -as [type])) {
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class PwBatWin32 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyW(uint uCode, uint uMapType);
    [DllImport("user32.dll")]
    public static extern IntPtr PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool PostThreadMessageW(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
}
"@
}
$WM_KEYDOWN = 0x0100; $WM_KEYUP = 0x0101; $WM_CHAR = 0x0102
$WM_ACTIVATE = 0x0006; $WM_SETFOCUS = 0x0007; $WM_ACTIVATEAPP = 0x001C
$WM_MOUSEMOVE = 0x0200; $WM_SETCURSOR = 0x0020; $WM_NCHITTEST = 0x0084
$WM_LBUTTONDOWN = 0x0201; $WM_LBUTTONUP = 0x0202
$WA_ACTIVE = 1; $HTCLIENT = 1; $MK_LBUTTON = 0x0001
$KeyMap = @{
    "SPACE" = 0x20; "ENTER" = 0x0D; "ESC" = 0x1B; "ESCAPE" = 0x1B; "TAB" = 0x09;
    "F1" = 0x70; "F2" = 0x71; "F3" = 0x72; "F4" = 0x73;
    "F5" = 0x74; "F6" = 0x75; "F7" = 0x76; "F8" = 0x77;
    "F9" = 0x78; "F10" = 0x79; "F11" = 0x7A; "F12" = 0x7B;
}
function Resolve-Vk([string]$name) {
    $n = $name.Trim().ToUpper()
    if ($KeyMap.ContainsKey($n)) { return $KeyMap[$n] }
    if ($n -match '^(0X[0-9A-F]+|\d+)$') { return [int]$n }
    if ($n.Length -eq 1) { return [int][char]$n }
    throw "Tecla desconhecida: $name"
}
function Get-HwndForPid([uint32]$tpid) {
    $found = New-Object Collections.Generic.List[IntPtr]
    $cb = { param([IntPtr]$h, [IntPtr]$l)
        $wpid = 0
        [void][PwBatWin32]::GetWindowThreadProcessId($h, [ref]$wpid)
        if ($wpid -eq $tpid -and [PwBatWin32]::IsWindowVisible($h)) { $found.Add($h) }
        return $true
    }.GetNewClosure()
    $del = [PwBatWin32+EnumWindowsProc]$cb
    $script:__keep = $del
    [void][PwBatWin32]::EnumWindows($del, [IntPtr]::Zero)
    if ($found.Count -eq 0) { throw "Sem janela visivel para o PID $tpid" }
    return $found[0]
}
function Get-KeyLParams([uint32]$vk) {
    $scan = [PwBatWin32]::MapVirtualKeyW($vk, 0)
    $dn = 1 -bor ($scan -shl 16)
    $up = $dn -bor (1 -shl 30) -bor (1 -shl 31)
    return @{ Scan = $scan; Down = $dn; Up = $up }
}
function PM([IntPtr]$h, [uint32]$m, [long]$w, [long]$l, [string]$tag) {
    $r = [PwBatWin32]::PostMessageW($h, $m, [IntPtr]$w, [IntPtr]$l)
    Write-Host ("  {0}: msg=0x{1:X} w=0x{2:X} l=0x{3:X} -> ret={4}" -f $tag, $m, $w, $l, $r.ToInt64())
}
$proc = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
if (-not $proc) { Write-Host "Processo $TargetPid nao encontrado."; exit 1 }
$hwnd = Get-HwndForPid ([uint32]$TargetPid)
$tid = 0
[void][PwBatWin32]::GetWindowThreadProcessId($hwnd, [ref]$tid)
Write-Host ("Alvo HWND=0x{0:X} thread={1} variante={2}" -f $hwnd.ToInt64(), $tid, $Variant)
Write-Host "Envio em ${Countdown}s... jogo SEM foco, NAO clique nele."
Start-Sleep -Seconds $Countdown
switch ($Variant.ToUpper()) {
    "T0" {
        # Baseline: DOWN/UP com scan code (igual v2, deve funcionar COM foco e falhar SEM)
        $vk = Resolve-Vk $Key; $k = Get-KeyLParams $vk
        Write-Host "T0 baseline VK=0x$('{0:X}' -f $vk) scan=0x$('{0:X}' -f $k.Scan)"
        PM $hwnd $WM_KEYDOWN $vk $k.Down "DOWN"
        Start-Sleep -Milliseconds 50
        PM $hwnd $WM_KEYUP $vk $k.Up "UP"
    }
    "T1" {
        # Priming de ativacao: finge que a janela foi ativada, depois manda a tecla
        $vk = Resolve-Vk $Key; $k = Get-KeyLParams $vk
        Write-Host "T1 priming + VK=0x$('{0:X}' -f $vk)"
        PM $hwnd $WM_ACTIVATE $WA_ACTIVE 0 "ACTIVATE"
        Start-Sleep -Milliseconds 30
        PM $hwnd $WM_SETFOCUS 0 0 "SETFOCUS"
        Start-Sleep -Milliseconds 30
        PM $hwnd $WM_ACTIVATEAPP 1 0 "ACTIVATEAPP"
        Start-Sleep -Milliseconds 30
        PM $hwnd $WM_KEYDOWN $vk $k.Down "DOWN"
        Start-Sleep -Milliseconds 50
        PM $hwnd $WM_KEYUP $vk $k.Up "UP"
    }
    "T2" {
        # Trio completo: DOWN + CHAR + UP (TranslateMessage geraria o CHAR numa tecla real)
        $vk = Resolve-Vk $Key; $k = Get-KeyLParams $vk
        $ch = if ($vk -eq 0x20) { 0x20 } elseif ($vk -ge 0x41 -and $vk -le 0x5A) { $vk + 32 } else { $vk }
        Write-Host "T2 trio VK=0x$('{0:X}' -f $vk) CHAR=0x$('{0:X}' -f $ch)"
        PM $hwnd $WM_KEYDOWN $vk $k.Down "DOWN"
        Start-Sleep -Milliseconds 30
        PM $hwnd $WM_CHAR $ch $k.Down "CHAR"
        Start-Sleep -Milliseconds 30
        PM $hwnd $WM_KEYUP $vk $k.Up "UP"
    }
    "T3" {
        # Hold com auto-repeat: DOWN, espera, DOWN repetido (repeat=2 + prev-state), espera, UP
        $vk = Resolve-Vk $Key; $k = Get-KeyLParams $vk
        $rep = 2 -bor ($k.Scan -shl 16) -bor (1 -shl 30)
        Write-Host "T3 hold VK=0x$('{0:X}' -f $vk) (DOWN, 300ms, DOWN-repeat, 200ms, UP)"
        PM $hwnd $WM_KEYDOWN $vk $k.Down "DOWN1"
        Start-Sleep -Milliseconds 300
        PM $hwnd $WM_KEYDOWN $vk $rep "DOWN2-repeat"
        Start-Sleep -Milliseconds 200
        PM $hwnd $WM_KEYUP $vk $k.Up "UP"
    }
    "T5" {
        # Mensagem na fila da THREAD em vez do HWND (sync-clients importa PostThreadMessage)
        $vk = Resolve-Vk $Key; $k = Get-KeyLParams $vk
        Write-Host "T5 thread-msg tid=$tid VK=0x$('{0:X}' -f $vk)"
        $r1 = [PwBatWin32]::PostThreadMessageW($tid, $WM_KEYDOWN, [IntPtr]$vk, [IntPtr]$k.Down)
        Write-Host "  T-DOWN -> ret=$r1"
        Start-Sleep -Milliseconds 50
        $r2 = [PwBatWin32]::PostThreadMessageW($tid, $WM_KEYUP, [IntPtr]$vk, [IntPtr]$k.Up)
        Write-Host "  T-UP -> ret=$r2"
    }
    "C0" {
        # Click baseline em coordenada de client area
        $lp = ($ClickY -shl 16) -bor ($ClickX -band 0xFFFF)
        Write-Host "C0 click ($ClickX,$ClickY)"
        PM $hwnd $WM_LBUTTONDOWN $MK_LBUTTON $lp "DOWN"
        Start-Sleep -Milliseconds 50
        PM $hwnd $WM_LBUTTONUP 0 $lp "UP"
    }
    "C4" {
        # Click com priming de mouse: HITTEST + MOVE + SETCURSOR antes do click
        $lp = ($ClickY -shl 16) -bor ($ClickX -band 0xFFFF)
        $sc = ($WM_LBUTTONDOWN -shl 16) -bor $HTCLIENT
        Write-Host "C4 click-priming ($ClickX,$ClickY)"
        PM $hwnd $WM_NCHITTEST 0 $lp "HITTEST"
        Start-Sleep -Milliseconds 20
        PM $hwnd $WM_MOUSEMOVE 0 $lp "MOVE"
        Start-Sleep -Milliseconds 20
        PM $hwnd $WM_SETCURSOR $hwnd.ToInt64() $sc "SETCURSOR"
        Start-Sleep -Milliseconds 20
        PM $hwnd $WM_LBUTTONDOWN $MK_LBUTTON $lp "DOWN"
        Start-Sleep -Milliseconds 50
        PM $hwnd $WM_LBUTTONUP 0 $lp "UP"
    }
    default { Write-Host "Variante desconhecida. Use T0,T1,T2,T3,T5,C0,C4."; exit 1 }
}
Write-Host "Pronto. Aconteceu algo no jogo? (verifique visualmente)"
