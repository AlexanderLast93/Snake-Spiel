# Startet Snake.exe und legt waehrend des Intros eine Reihe Bildschirmfotos ab.
# Es wird KEINE Taste gedrueckt - jede Taste wuerde das Intro ueberspringen.
# Wird von intro-pruefen.cmd aufgerufen; Bilder landen in "Claude outputs\intro".

$ErrorActionPreference = "Continue"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Intro {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

[Win32Intro]::SetProcessDPIAware() | Out-Null

$root = $PSScriptRoot
$exe  = Join-Path $root "bin\Debug\net10.0-windows\Snake.exe"
$out  = Join-Path $root "Claude outputs\intro"

if (Test-Path $out) { Remove-Item (Join-Path $out "*.png") -ErrorAction SilentlyContinue }
else { New-Item -ItemType Directory -Path $out | Out-Null }

if (-not (Test-Path $exe)) {
    Write-Output "FEHLER: $exe fehlt - Build fehlgeschlagen?"
    exit 2
}

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Output ("Bildschirm: {0}x{1}" -f $bounds.Width, $bounds.Height)

# Auf 1280 Pixel Breite verkleinern - fuer die Beurteilung reicht das und die
# Bilder bleiben klein genug zum Uebertragen.
$scale = 1280.0 / $bounds.Width
$shotW = [int]($bounds.Width * $scale)
$shotH = [int]($bounds.Height * $scale)

$full  = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$gFull = [System.Drawing.Graphics]::FromImage($full)

function Shot([string]$name, [long]$ms) {
    $gFull.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $small = New-Object System.Drawing.Bitmap $shotW, $shotH
    $g = [System.Drawing.Graphics]::FromImage($small)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($full, 0, 0, $shotW, $shotH)
    $small.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $small.Dispose()
    Write-Output ("  {0} bei {1} ms" -f $name, $ms)
}

$watch = [System.Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru
Write-Output "Snake gestartet."

# Fenster nach vorn holen, sobald es da ist - ohne Taste.
Start-Sleep -Milliseconds 700
$p.Refresh()
if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
    [Win32Intro]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
}

# 24 Bilder im Abstand von 300 ms decken Start, Intro und Uebergang ins Menue ab.
for ($i = 1; $i -le 24; $i++) {
    $target = 300 * $i
    $wait = $target - $watch.ElapsedMilliseconds
    if ($wait -gt 0) { Start-Sleep -Milliseconds $wait }
    Shot ("{0:d2}.png" -f $i) $watch.ElapsedMilliseconds
}

# Noch zwei Bilder, wenn sich alles beruhigt hat
Start-Sleep -Milliseconds 1500
Shot "25-menue.png" $watch.ElapsedMilliseconds
Start-Sleep -Milliseconds 1500
Shot "26-menue.png" $watch.ElapsedMilliseconds

$gFull.Dispose(); $full.Dispose()
Stop-Process -Id $p.Id -Force
Write-Output "Snake beendet."
exit 0
