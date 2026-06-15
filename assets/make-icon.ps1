# Generates the StatStudio app icon: a teal->indigo gradient rounded tile with white
# ascending bars (a bar chart) and a gold trend line with dots rising over them --
# the statistics/analysis metaphor. Outputs src\StatStudio.Wpf\app.ico (+ a preview PNG).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Gradient tile (teal -> indigo).
    $m = $s * 0.06
    $tile = New-RoundRect $m $m ($s - 2*$m) ($s - 2*$m) ($s * 0.22)
    $rect = New-Object System.Drawing.RectangleF(0, 0, $s, $s)
    $c1 = [System.Drawing.Color]::FromArgb(255, 20, 184, 166)   # teal
    $c2 = [System.Drawing.Color]::FromArgb(255, 99, 102, 241)   # indigo
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 60.0)
    $g.FillPath($brush, $tile)

    # Glossy top highlight.
    $hl = New-RoundRect $m $m ($s - 2*$m) ($s * 0.46) ($s * 0.22)
    $hlBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(34, 255, 255, 255))
    $g.FillPath($hlBrush, $hl)

    # Ascending white bars (bar chart).
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 255, 255, 255))
    $baseY = 0.74 * $s
    $bx = @(0.26, 0.42, 0.58, 0.74)
    $bh = @(0.18, 0.30, 0.24, 0.40)
    $bw = 0.11 * $s
    for ($i = 0; $i -lt 4; $i++) {
        $x = $bx[$i] * $s - $bw / 2
        $h = $bh[$i] * $s
        $bar = New-RoundRect $x ($baseY - $h) $bw $h ($bw * 0.18)
        $g.FillPath($white, $bar)
        $bar.Dispose()
    }

    # Gold trend line with dots rising across the tops.
    $gold = [System.Drawing.Color]::FromArgb(255, 250, 204, 21)
    $pen = New-Object System.Drawing.Pen($gold, [float]($s * 0.022))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $ty = @(0.50, 0.40, 0.44, 0.30)
    $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    for ($i = 0; $i -lt 4; $i++) {
        $pts.Add((New-Object System.Drawing.PointF([float]($bx[$i] * $s), [float]($ty[$i] * $s))))
    }
    $g.DrawLines($pen, $pts.ToArray())
    $goldBrush = New-Object System.Drawing.SolidBrush($gold)
    $r = $s * 0.035
    foreach ($pt in $pts) { $g.FillEllipse($goldBrush, $pt.X - $r, $pt.Y - $r, 2*$r, 2*$r) }

    $pen.Dispose(); $goldBrush.Dispose(); $white.Dispose(); $brush.Dispose(); $hlBrush.Dispose()
    $tile.Dispose(); $hl.Dispose(); $g.Dispose()
    return $bmp
}

$sizes = @(256, 128, 64, 48, 32, 24, 16)
$pngs = [System.Collections.Generic.List[byte[]]]::new()
$bmps = [System.Collections.Generic.List[object]]::new()
foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs.Add($ms.ToArray())
    $bmps.Add($bmp)
    $ms.Dispose()
    Write-Output ("  {0,3}px -> {1} bytes" -f $sz, $pngs[$pngs.Count - 1].Length)
}

# Pack the PNGs into a multi-resolution .ico.
$fs = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)   # ICONDIR
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = $sizes[$i]; if ($dim -ge 256) { $dim = 0 }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
for ($i = 0; $i -lt $sizes.Count; $i++) { $bw.Write($pngs[$i]) }
$bw.Flush()

$outDir = (Resolve-Path (Join-Path $PSScriptRoot "..\src\StatStudio.Wpf")).Path
$out = Join-Path $outDir "app.ico"
[System.IO.File]::WriteAllBytes($out, $fs.ToArray())
$bw.Dispose(); $fs.Dispose()

$bmps[0].Save((Join-Path $PSScriptRoot "icon-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
foreach ($b in $bmps) { $b.Dispose() }
Write-Output "Wrote $out ($([System.IO.File]::ReadAllBytes($out).Length) bytes, $($sizes.Count) sizes)"
