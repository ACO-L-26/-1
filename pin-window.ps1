
# Find Chrome app window with "Music Pet" title and make it always-on-top
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinAPI {
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_SHOWWINDOW = 0x0040;
}
"@

$topmost = [WinAPI]::HWND_TOPMOST
$flags = [WinAPI]::SWP_NOMOVE -bor [WinAPI]::SWP_NOSIZE -bor [WinAPI]::SWP_SHOWWINDOW

# Find Chrome window containing "Music Pet" in title
$found = $false
$callback = {
    param([IntPtr]$hwnd, [IntPtr]$lparam)
    $length = [WinAPI]::GetWindowTextLength($hwnd)
    if($length -gt 0) {
        $sb = New-Object System.Text.StringBuilder($length + 1)
        [WinAPI]::GetWindowText($hwnd, $sb, $sb.Capacity) | Out-Null
        $title = $sb.ToString()
        if($title -match "Music Pet") {
            [WinAPI]::SetWindowPos($hwnd, $topmost, 0, 0, 340, 500, $flags) | Out-Null
            Write-Host "Pet window pinned to top: $title"
            $script:found = $true
            return $false
        }
    }
    return $true
}

[WinAPI]::EnumWindows($callback, [IntPtr]::Zero)
if(-not $found) { Write-Host "Pet window not found. Make sure Chrome is running with pet.html" }
