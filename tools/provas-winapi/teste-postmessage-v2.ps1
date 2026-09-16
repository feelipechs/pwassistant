# teste-postmessage-v2.ps1 -- COPIAR PARA C:\Users\chagass\Documents\teste-pw\
# Receita correta de PostMessage para o elementclient do PW (teste de input sem foco).
# Evolucao do teste-postmessage.ps1: o teste antigo mandava WM_KEYDOWN/UP com
# lParam=0 para o MainWindowHandle. Isso falha por 2 motivos tecnicos:
#   1) lParam zerado nao carrega o scan code (via MapVirtualKey) que o engine pode exigir;
#   2) o alvo real costuma ser a janela FILHA do render (DirectX), nao a top-level.
#
# Uso seguro (so le, nao envia nada):
#   .\teste-postmessage-v2.ps1 -List
# Teste de TECLA sem foco: .\teste-postmessage-v2.ps1 -TargetPid 11708 -Key SPACE -TargetChildIndex 0
# Teste de CLICK sem foco:  .\teste-postmessage-v2.ps1 -TargetPid 11708 -ClickX 400 -ClickY 300 -TargetChildIndex 0
# Variante SendMessageTimeout: .\teste-postmessage-v2.ps1 -TargetPid 11708 -Key F1 -UseSendMessage
# IMPORTANTE: olhe a janela do jogo, NAO clique nela. O script conta 3s antes de enviar.
param(
    [switch]$List,
    [int]$TargetPid = 0,
    [string]$Key = "",
    [int]$ClickX = -1,
    [int]$ClickY = -1,
    [int]$TargetChildIndex = 0,
    [switch]$UseSendMessage,
    [int]$Countdown = 3
)
if (-not ("PwTestWin32" -as [type])) {
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class PwTestWin32 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyW(uint uCode, uint uMapType);
    [DllImport("user32.dll")]
    public static extern IntPtr PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left; public int Top; public int Right; public int Bottom;
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
    }
}
"@
}
$WM_KEYDOWN = 0x0100
$WM_KEYUP = 0x0101
$WM_LBUTTONDOWN = 0x0201
$WM_LBUTTONUP = 0x0202
$MK_LBUTTON = 0x0001
$SMTO_ABORTIFHUNG = 0x0002
$MAPVK_VK_TO_VSC = 0
$KeyMap = @{
    "SPACE" = 0x20; "ENTER" = 0x0D; "ESC" = 0x1B; "ESCAPE" = 0x1B; "TAB" = 0x09;
    "F1" = 0x70; "F2" = 0x71; "F3" = 0x72; "F4" = 0x73;
    "F5" = 0x74; "F6" = 0x75; "F7" = 0x76; "F8" = 0x77;
    "F9" = 0x78; "F10" = 0x79; "F11" = 0x7A; "F12" = 0x7B;
}
function Get-WindowText([IntPtr]$hwnd) {
    $sb = New-Object Text.StringBuilder 256
    [void][PwTestWin32]::GetWindowTextW($hwnd, $sb, 256)
    return $sb.ToString()
}
function Get-WindowClass([IntPtr]$hwnd) {
    $sb = New-Object Text.StringBuilder 256
    [void][PwTestWin32]::GetClassNameW($hwnd, $sb, 256)
    return $sb.ToString()
}
function Get-TopWindowsForPid([uint32]$targetPid) {
    $found = New-Object Collections.Generic.List[IntPtr]
    $cb = { param([IntPtr]$h, [IntPtr]$l)
        $wpid = 0
        [void][PwTestWin32]::GetWindowThreadProcessId($h, [ref]$wpid)
        if ($wpid -eq $targetPid -and [PwTestWin32]::IsWindowVisible($h)) { $found.Add($h) }
        return $true
    }.GetNewClosure()
    $del = [PwTestWin32+EnumWindowsProc]$cb
    $script:__keep1 = $del
    [void][PwTestWin32]::EnumWindows($del, [IntPtr]::Zero)
    return $found
}
function Get-ChildWindows([IntPtr]$parent) {
    $found = New-Object Collections.Generic.List[IntPtr]
    $cb = { param([IntPtr]$h, [IntPtr]$l)
        $found.Add($h)
        return $true
    }.GetNewClosure()
    $del = [PwTestWin32+EnumWindowsProc]$cb
    $script:__keep2 = $del
    [void][PwTestWin32]::EnumChildWindows($parent, $del, [IntPtr]::Zero)
    return $found
}
function Show-WindowTree([uint32]$targetPid) {
    $tops = Get-TopWindowsForPid $targetPid
    Write-Host "PID $targetPid -> $($tops.Count) janela(s) top-level visivel(is):"
    $i = 0
    foreach ($h in $tops) {
        $r = New-Object PwTestWin32+RECT
        [void][PwTestWin32]::GetWindowRect($h, [ref]$r)
        Write-Host ("  [top {0}] HWND=0x{1:X} titulo='{2}' classe='{3}' rect={4}x{5}" -f $i, $h.ToInt64(), (Get-WindowText $h), (Get-WindowClass $h), $r.Width, $r.Height)
        $kids = Get-ChildWindows $h
        $j = 0
        foreach ($k in $kids) {
            $vis = [PwTestWin32]::IsWindowVisible($k)
            $cr = New-Object PwTestWin32+RECT
            [void][PwTestWin32]::GetClientRect($k, [ref]$cr)
            Write-Host ("      [filha {0}] HWND=0x{1:X} visivel={2} classe='{3}' titulo='{4}' client={5}x{6}" -f $j, $k.ToInt64(), $vis, (Get-WindowClass $k), (Get-WindowText $k), $cr.Width, $cr.Height)
            $j++
        }
        $i++
    }
    return $tops
}
function Build-KeyLParam([uint32]$vk, [bool]$keyUp) {
    $scan = [PwTestWin32]::MapVirtualKeyW($vk, $MAPVK_VK_TO_VSC)
    $lp = 1 -bor ($scan -shl 16)
    if ($keyUp) { $lp = $lp -bor (1 -shl 30) -bor (1 -shl 31) }
    return @{ Scan = $scan; LParam = $lp }
}
function Send-WindowMessage([IntPtr]$hwnd, [uint32]$msg, [IntPtr]$wp, [IntPtr]$lp) {
    if ($UseSendMessage) {
        $res = [IntPtr]::Zero
        $r = [PwTestWin32]::SendMessageTimeoutW($hwnd, $msg, $wp, $lp, $SMTO_ABORTIFHUNG, 1000, [ref]$res)
        return "SendMessageTimeout ret=$($r.ToInt64()) result=$($res.ToInt64())"
    } else {
        $r = [PwTestWin32]::PostMessageW($hwnd, $msg, $wp, $lp)
        return "PostMessage ret=$($r.ToInt64())"
    }
}
function Resolve-Vk([string]$name) {
    $n = $name.Trim().ToUpper()
    if ($KeyMap.ContainsKey($n)) { return $KeyMap[$n] }
    if ($n -match '^(0X[0-9A-F]+|\d+)$') { return [int]$n }
    if ($n.Length -eq 1) { return [int][char]$n }
    throw "Tecla desconhecida: $name (use SPACE, ENTER, F1..F12, A..Z, 0..9 ou 0xNN)"
}
$elementclients = Get-Process -Name "elementclient_64" -ErrorAction SilentlyContinue
if ($List -or ($TargetPid -eq 0 -and $Key -eq "" -and $ClickX -lt 0)) {
    if (-not $elementclients) { Write-Host "Nenhum elementclient_64 rodando."; exit 0 }
    foreach ($p in $elementclients) {
        Write-Host ("=== elementclient PID {0} ===" -f $p.Id)
        Show-WindowTree ([uint32]$p.Id) | Out-Null
    }
    Write-Host ""
    Write-Host "Tecla: .\teste-postmessage-v2.ps1 -TargetPid <PID> -Key SPACE"
    Write-Host "Click: .\teste-postmessage-v2.ps1 -TargetPid <PID> -ClickX 400 -ClickY 300"
    exit 0
}
$proc = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
if (-not $proc) { Write-Host "Processo $TargetPid nao encontrado."; exit 1 }
$tops = Show-WindowTree ([uint32]$TargetPid)
if ($tops.Count -eq 0) { Write-Host "Sem janela visivel para o PID $TargetPid."; exit 1 }
$top = $tops[0]
$target = $top
$kids = Get-ChildWindows $top
if ($kids.Count -gt 0) {
    if ($TargetChildIndex -ge $kids.Count) { Write-Host "Indice de filha invalido (0..$($kids.Count-1))."; exit 1 }
    $target = $kids[$TargetChildIndex]
    Write-Host ("Alvo: filha {0} HWND=0x{1:X}" -f $TargetChildIndex, $target.ToInt64())
} else {
    Write-Host ("Alvo: top-level HWND=0x{0:X} (sem filhas)" -f $target.ToInt64())
}
$mode = if ($UseSendMessage) { "SendMessageTimeout" } else { "PostMessage" }
if ($Key -ne "") {
    $vk = Resolve-Vk $Key
    $dn = Build-KeyLParam $vk $false
    $up = Build-KeyLParam $vk $true
    Write-Host ("Tecla {0} (VK=0x{1:X}, scan=0x{2:X}) via {3} em {4}s... NAO clique no jogo." -f $Key.ToUpper(), $vk, $dn.Scan, $mode, $Countdown)
    Start-Sleep -Seconds $Countdown
    $o1 = Send-WindowMessage $target $WM_KEYDOWN ([IntPtr]$vk) ([IntPtr]$dn.LParam)
    Write-Host "  DOWN lParam=0x$('{0:X}' -f $dn.LParam) -> $o1"
    Start-Sleep -Milliseconds 50
    $o2 = Send-WindowMessage $target $WM_KEYUP ([IntPtr]$vk) ([IntPtr]$up.LParam)
    Write-Host "  UP   lParam=0x$('{0:X}' -f $up.LParam) -> $o2"
    Write-Host "Pronto. A acao aconteceu no jogo? (verifique visualmente)"
} elseif ($ClickX -ge 0 -and $ClickY -ge 0) {
    $lp = ($ClickY -shl 16) -bor ($ClickX -band 0xFFFF)
    Write-Host ("Click client ({0},{1}) lParam=0x{2:X} via {3} em {4}s... NAO clique no jogo." -f $ClickX, $ClickY, $lp, $mode, $Countdown)
    Start-Sleep -Seconds $Countdown
    $o1 = Send-WindowMessage $target $WM_LBUTTONDOWN ([IntPtr]$MK_LBUTTON) ([IntPtr]$lp)
    Write-Host "  DOWN -> $o1"
    Start-Sleep -Milliseconds 50
    $o2 = Send-WindowMessage $target $WM_LBUTTONUP ([IntPtr]0) ([IntPtr]$lp)
    Write-Host "  UP   -> $o2"
    Write-Host "Pronto. O click funcionou no jogo? (verifique visualmente)"
} else {
    Write-Host "Informe -Key <tecla> ou -ClickX/-ClickY. Veja -List para PIDs."
    exit 1
}

