if (-not ("Win32" -as [type])) {
Add-Type @"
using System;
using System.Runtime.InteropServices;

public class Win32 {
    [DllImport("user32.dll")]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
"@
}

$testpid = 11708

$proc = Get-Process -Id $testpid -ErrorAction SilentlyContinue
if (-not $proc) { Write-Host "Processo nao encontrado."; exit }

$hwnd = $proc.MainWindowHandle
Write-Host "Handle: $hwnd (PID $testpid)"

$WM_KEYDOWN = 0x0100
$WM_KEYUP   = 0x0101
$VK_SPACE   = 0x20

Write-Host "Enviando ESPACO em 3 segundos... olhe a janela do jogo, NAO clique nela."
Start-Sleep -Seconds 3
[void][Win32]::PostMessage($hwnd, $WM_KEYDOWN, [IntPtr]$VK_SPACE, [IntPtr]::Zero)
Start-Sleep -Milliseconds 50
[void][Win32]::PostMessage($hwnd, $WM_KEYUP, [IntPtr]$VK_SPACE, [IntPtr]::Zero)

Write-Host "Pronto. O personagem pulou? (verifique visualmente)"