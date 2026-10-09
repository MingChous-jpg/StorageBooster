# 生成 mod 预览图 preview.png（256x256），依赖 Windows 自带的 System.Drawing。
# 执行方式（绕过 PowerShell 5.1 读无 BOM 脚本会把 UTF-8 中文按 ANSI 解析的问题）：
#   $code = [IO.File]::ReadAllText("<this file>", [Text.Encoding]::UTF8); Invoke-Expression $code

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$out = Join-Path $PSScriptRoot "preview.png"
if ($PSScriptRoot -eq "") { $out = "preview.png" }

$SIZE = 256
$bmp = New-Object System.Drawing.Bitmap $SIZE, $SIZE
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$bg    = [System.Drawing.Color]::FromArgb(21, 24, 29)
$card  = [System.Drawing.Color]::FromArgb(30, 35, 43)
$old   = [System.Drawing.Color]::FromArgb(44, 50, 59)
$new   = [System.Drawing.Color]::FromArgb(224, 163, 68)
$white = [System.Drawing.Color]::FromArgb(255, 255, 255)
$sub   = [System.Drawing.Color]::FromArgb(139, 149, 163)

$g.Clear($bg)

function RoundRect([System.Drawing.Graphics] $gr, [int]$x, [int]$y, [int]$w, [int]$h, [int]$r, [System.Drawing.Brush]$brush) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $gr.FillPath($brush, $path)
    $path.Dispose()
}

# 卡片
RoundRect $g 10 10 ($SIZE - 20) ($SIZE - 20) 18 (New-Object System.Drawing.SolidBrush($card))

# 标题
# 用 SimHei：simhei.ttf 是纯 TTF。System.Drawing 在 .NET Framework 下读 .ttc 字体集
# 有已知缺陷（能解析族名但绘制出豆腐块），必须避开 msyh.ttc。
$titleFont = New-Object System.Drawing.Font("SimHei", 29, [System.Drawing.FontStyle]::Bold)
$title = "鸭窝扩容"
$format = New-Object System.Drawing.StringFormat
$format.Alignment = [System.Drawing.StringAlignment]::Center
$titleRect = New-Object System.Drawing.RectangleF 0, 36, $SIZE, 46
$g.DrawString($title, $titleFont, (New-Object System.Drawing.SolidBrush($white)), $titleRect, $format)

# 仓库网格：前 2 行原容量，后 3 行扩出来的
$cols = 8; $rows = 5; $cell = 22; $gap = 4
$gw = $cols * $cell + ($cols - 1) * $gap
$x0 = [int](($SIZE - $gw) / 2)
$y0 = 88
for ($r = 0; $r -lt $rows; $r++) {
    for ($c = 0; $c -lt $cols; $c++) {
        $x = $x0 + $c * ($cell + $gap)
        $y = $y0 + $r * ($cell + $gap)
        $b = if ($r -lt 2) { New-Object System.Drawing.SolidBrush($old) } else { New-Object System.Drawing.SolidBrush($new) }
        RoundRect $g $x $y $cell $cell 4 $b
        $b.Dispose()
    }
}

# 副标题
$subFont = New-Object System.Drawing.Font("SimHei", 13, [System.Drawing.FontStyle]::Regular)
$subText = "仓库 400 格 · 堆叠 ×2"
$subRect = New-Object System.Drawing.RectangleF 0, 211, $SIZE, 30
$g.DrawString($subText, $subFont, (New-Object System.Drawing.SolidBrush($sub)), $subRect, $format)

$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()

Write-Output ("written: " + $out)
