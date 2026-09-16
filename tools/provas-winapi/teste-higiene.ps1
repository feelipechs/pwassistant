# teste-higiene.ps1 -- higiene do flag "ativo" (complemento da bateria T1).
# -Mode Prime:   igual ao T1 (ACTIVATE + SETFOCUS + ACTIVATEAPP + tecla) -- controle positivo
# -Mode Restore: devolve o estado (ACTIVATE WA_INACTIVE + ACTIVATEAPP 0) SEM enviar tecla
# -Mode Full:    Prime + tecla + Restore (pipeline completo proposto p/ o app final)
# Uso: .\teste-higiene.ps1 -Mode Full -TargetPid 11708 -Key F1 -Countdown 6
# (jogo SEM foco; depois rode T1 de novo p/ confirmar que continua funcionando apos o Restore)
param(
    [string]$Mode = "Full",
    [int]$TargetPid = 0,
    [string]$Key = "F1",
    [int]$Countdown = 6
)
if (-not ("PwHigWin32" -as [type])) {
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class PwHigWin32 {
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
}
"@
}
$WM_KEYDOWN = 0x0100; $WM_KEYUP = 0x0101
$WM_ACTIVATE = 0x0006; $WM_SETFOCUS = 0x0007; $WM_ACTIVATEAPP = 0x001C
$WA_ACTIVE = 1; $WA_INACTIVE = 0
$KeyMap = @{ "SPACE" = 0x20; "ENTER" = 0x0D; "F1" = 0x70; "F2" = 0x71; "F3" = 0x72; "F4" = 0x73; "F5" = 0x74; "F6" = 0x75; "F7" = 0x76; "F8" = 0x77 }
function Resolve-Vk([string]$name) {
    $n = $name.Trim().ToUpper()
    if ($KeyMap.ContainsKey($n)) { return $KeyMap[$n] }
    if ($n.Length -eq 1) { return [int][char]$n }
    throw "Tecla desconhecida: $name"
}
function Get-HwndForPid([uint32]$tpid) {
    $found = New-Object Collections.Generic.List[IntPtr]
    $cb = { param([IntPtr]$h, [IntPtr]$l)
        $wpid = 0
        [void][PwHigWin32]::GetWindowThreadProcessId($h, [ref]$wpid)
        if ($wpid -eq $tpid -and [PwHigWin32]::IsWindowVisible($h)) { $found.Add($h) }
        return $true
    }.GetNewClosure()
    $del = [PwHigWin32+EnumWindowsProc]$cb
    $script:__keep = $del
    [void][PwHigWin32]::EnumWindows($del, [IntPtr]::Zero)
    if ($found.Count -eq 0) { throw "Sem janela visivel para o PID $tpid" }
    return $found[0]
}
function PM([IntPtr]$h, [uint32]$m, [long]$w, [long]$l, [string]$tag) {
    $r = [PwHigWin32]::PostMessageW($h, $m, [IntPtr]$w, [IntPtr]$l)
    Write-Host ("  {0}: msg=0x{1:X} w=0x{2:X} l=0x{3:X} -> ret={4}" -f $tag, $m, $w, $l, $r.ToInt64())
}
$proc = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
if (-not $proc) { Write-Host "Processo $TargetPid nao encontrado."; exit 1 }
$hwnd = Get-HwndForPid ([uint32]$TargetPid)
Write-Host ("Alvo HWND=0x{0:X} modo={1}" -f $hwnd.ToInt64(), $Mode)
Write-Host "Envio em ${Countdown}s... jogo SEM foco, NAO clique nele."
Start-Sleep -Seconds $Countdown
$doPrime = ($Mode -eq "Prime" -or $Mode -eq "Full")
$doRestore = ($Mode -eq "Restore" -or $Mode -eq "Full")
if ($doPrime) {
    $vk = Resolve-Vk $Key
    $scan = [PwHigWin32]::MapVirtualKeyW([uint32]$vk, 0)
    $dn = 1 -bor ($scan -shl 16)
    $up = $dn -bor (1 -shl 30) -bor (1 -shl 31)
    PM $hwnd $WM_ACTIVATE $WA_ACTIVE 0 "ACTIVATE"
    Start-Sleep -Milliseconds 30
    PM $hwnd $WM_SETFOCUS 0 0 "SETFOCUS"
    Start-Sleep -Milliseconds 30
    PM $hwnd $WM_ACTIVATEAPP 1 0 "ACTIVATEAPP"
    Start-Sleep -Milliseconds 30
    PM $hwnd $WM_KEYDOWN $vk $dn "DOWN"
    Start-Sleep -Milliseconds 50
    PM $hwnd $WM_KEYUP $vk $up "UP"
}
if ($doRestore) {
    Start-Sleep -Milliseconds 100
    PM $hwnd $WM_ACTIVATE $WA_INACTIVE 0 "DEACTIVATE"
    Start-Sleep -Milliseconds 30
    PM $hwnd $WM_ACTIVATEAPP 0 0 "DEACTIVATEAPP"
    Write-Host "Estado devolvido. Rode T1 (ou -Mode Prime) de novo p/ confirmar que segue funcionando."
}
Write-Host "Pronto. Observe o jogo."
