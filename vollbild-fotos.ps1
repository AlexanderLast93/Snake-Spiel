# Startet Snake.exe aus dem Debug-Build, schaltet per Tastatur durch die Zustaende
# und legt Bildschirmfotos in "Claude outputs\" ab. Wird von vollbild-pruefen.cmd aufgerufen.

$ErrorActionPreference = "Continue"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public static class Win32 {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@

# Sonst liefert CopyFromScreen bei 125 % Skalierung ein verkleinertes Bild.
[Win32]::SetProcessDPIAware() | Out-Null

$root = $PSScriptRoot
$exe  = Join-Path $root "bin\Debug\net10.0-windows\Snake.exe"
$out  = Join-Path $root "Claude outputs"
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out | Out-Null }

if (-not (Test-Path $exe)) {
    Write-Output "FEHLER: $exe fehlt - Build fehlgeschlagen?"
    exit 2
}

function Shot([string]$name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $path = Join-Path $out $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "Foto: $name ($($b.Width)x$($b.Height))"
}

function Rect([IntPtr]$h, [string]$label) {
    $r = New-Object RECT
    [Win32]::GetWindowRect($h, [ref]$r) | Out-Null
    Write-Output ("{0}: {1},{2} - {3},{4}  ({5}x{6})" -f $label, $r.Left, $r.Top, $r.Right, $r.Bottom, ($r.Right - $r.Left), ($r.Bottom - $r.Top))
}

$script:Game = [IntPtr]::Zero

# Vor jeder Taste das Spiel nach vorn holen - andere Fenster koennen den Fokus
# zwischendurch stehlen (z. B. wenn versteckte Fenster wieder auftauchen).
function Keys([string]$k, [int]$ms) {
    if ($script:Game -ne [IntPtr]::Zero -and [Win32]::GetForegroundWindow() -ne $script:Game) {
        [Win32]::SetForegroundWindow($script:Game) | Out-Null
        Start-Sleep -Milliseconds 300
        Write-Output ("  Fokus zurueckgeholt vor '{0}': {1}" -f $k, ([Win32]::GetForegroundWindow() -eq $script:Game))
    }
    [System.Windows.Forms.SendKeys]::SendWait($k)
    Start-Sleep -Milliseconds $ms
}

Write-Output ("Bildschirm: " + [System.Windows.Forms.Screen]::PrimaryScreen.Bounds)
Write-Output ("Arbeitsbereich: " + [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea)

$p = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru
Start-Sleep -Seconds 6
$p.Refresh()
$h = $p.MainWindowHandle
$script:Game = $h
Write-Output "Prozess $($p.Id), Fenster '$($p.MainWindowTitle)', Handle $h"
[Win32]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 600
Write-Output ("Vordergrund ist Snake: " + ([Win32]::GetForegroundWindow() -eq $h))

Rect $h "Vollbild-Rechteck"
Shot "01-vollbild-menue.png"

Keys "{F3}" 400                 # Messanzeige an
Keys "3" 6000                   # Schnell starten, sechs Sekunden laufen lassen
Shot "02-vollbild-spiel-f3.png"

Keys "{F4}" 1500                # Scheine aus - zum Vergleich
Shot "02b-vollbild-spiel-ohne-schein.png"
Keys "{F4}" 600                 # Scheine wieder an

Keys " " 600                    # Pause
Shot "03-vollbild-pause.png"

Keys "{ESC}" 800                # Menue
Keys "{F11}" 2500               # Fenstermodus
$p.Refresh(); $h = $p.MainWindowHandle; $script:Game = $h
Rect $h "Fenster-Rechteck"
Shot "04-fenster-menue.png"

Keys "{F11}" 2500               # zurueck ins Vollbild
$p.Refresh(); $h = $p.MainWindowHandle; $script:Game = $h
Rect $h "Vollbild-Rechteck danach"
Shot "05-vollbild-menue-danach.png"
Keys "{F3}" 300                 # Messanzeige aus

$settings = Join-Path $env:APPDATA "SnakeSpiel\settings.json"
if (Test-Path $settings) {
    Write-Output "settings.json:"
    Get-Content $settings | ForEach-Object { "    " + $_ }
}

Stop-Process -Id $p.Id -Force
Write-Output "Snake beendet."
exit 0
