param(
    [string] $AvdName = '',
    [ValidateRange(320, 3840)] [int] $Width = 1280,
    [ValidateRange(320, 2160)] [int] $Height = 760
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SectorLabEmulatorWindow {
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
}
'@
$windows = @(Get-Process -Name qemu-system-x86_64 -ErrorAction SilentlyContinue | Where-Object {
    $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle.StartsWith('Android Emulator - ') -and
    (-not $AvdName -or $_.MainWindowTitle.StartsWith('Android Emulator - ' + $AvdName + ':'))
})
if ($windows.Count -ne 1) { throw 'Select exactly one running Android Emulator window with -AvdName.' }
$window = $windows[0].MainWindowHandle
$previousDpi = [SectorLabEmulatorWindow]::SetThreadDpiAwarenessContext([IntPtr](-4))
try {
    # Physical pixels avoid the misplaced window seen with Windows display scaling.
    $area = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $fitWidth = [Math]::Min($Width, $area.Width - 100)
    $fitHeight = [Math]::Min($Height, $area.Height - 100)
    $x = $area.Left + [int](($area.Width - $fitWidth) / 2)
    $y = $area.Top + [int](($area.Height - $fitHeight) / 2)
    $null = [SectorLabEmulatorWindow]::ShowWindowAsync($window, 9)
    if (-not [SectorLabEmulatorWindow]::SetWindowPos($window, [IntPtr]::Zero, $x, $y, $fitWidth, $fitHeight, 0x0044)) {
        throw 'Could not resize the Android Emulator window.'
    }
    Start-Sleep -Milliseconds 500
    $rectangle = New-Object SectorLabEmulatorWindow+Rect
    if (-not [SectorLabEmulatorWindow]::GetWindowRect($window, [ref] $rectangle)) { throw 'Could not read the emulator window bounds.' }
    # Qt preserves the virtual screen aspect ratio; center its resulting size.
    $actualWidth = $rectangle.Right - $rectangle.Left
    $actualHeight = $rectangle.Bottom - $rectangle.Top
    $x = $area.Left + [int](($area.Width - $actualWidth) / 2)
    $y = $area.Top + [int](($area.Height - $actualHeight) / 2)
    if (-not [SectorLabEmulatorWindow]::SetWindowPos($window, [IntPtr]::Zero, $x, $y, 0, 0, 0x0045)) {
        throw 'Could not center the Android Emulator window.'
    }
    if (-not [SectorLabEmulatorWindow]::GetWindowRect($window, [ref] $rectangle)) { throw 'Could not verify the emulator window bounds.' }
    $fits = $rectangle.Left -ge $area.Left -and $rectangle.Top -ge $area.Top -and
        $rectangle.Right -le $area.Right -and $rectangle.Bottom -le $area.Bottom
    if (-not $fits) { throw 'The emulator window still exceeds the primary screen working area.' }
    [PSCustomObject]@{
        window = $windows[0].MainWindowTitle
        bounds = @($rectangle.Left, $rectangle.Top, $rectangle.Right, $rectangle.Bottom)
        workingArea = @($area.Left, $area.Top, $area.Right, $area.Bottom)
        fits = $fits
    } | ConvertTo-Json -Compress
} finally {
    if ($previousDpi -ne [IntPtr]::Zero) { $null = [SectorLabEmulatorWindow]::SetThreadDpiAwarenessContext($previousDpi) }
}
