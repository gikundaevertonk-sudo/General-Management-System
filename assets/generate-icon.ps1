# Generates assets/gms.ico - the application icon for GMS.Desktop, the NSIS
# installer and the web favicon.
#
# The .ico is committed, so this only needs running when the mark itself changes.
# Colours are taken from GMS.Desktop/App/UiKit.vb so the icon matches the running
# app: the navy is the sidebar, the blue is Accent, and the bar down the left edge
# is the same device the dashboard stat cards use.

Add-Type -AssemblyName System.Drawing

$Navy   = [System.Drawing.Color]::FromArgb(17, 24, 39)
$Accent = [System.Drawing.Color]::FromArgb(37, 99, 235)

function New-RoundedPath {
    param([int]$Size, [int]$Radius, [single]$Inset = 0)

    $d = $Radius * 2
    $lo = $Inset
    $hi = $Size - $d - 1 - $Inset
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($lo, $lo, $d, $d, 180, 90)
    $path.AddArc($hi, $lo, $d, $d, 270, 90)
    $path.AddArc($hi, $hi, $d, $d, 0, 90)
    $path.AddArc($lo, $hi, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $radius = [Math]::Max(2, [int]($Size * 0.18))
    $path = New-RoundedPath -Size $Size -Radius $radius

    $back = New-Object System.Drawing.SolidBrush($Navy)
    $g.FillPath($back, $path)

    # Accent bar down the left edge, clipped to the rounded corners.
    $g.SetClip($path)
    $barWidth = [Math]::Max(1, [int]($Size * 0.10))
    $bar = New-Object System.Drawing.SolidBrush($Accent)
    $g.FillRectangle($bar, 0, 0, $barWidth, $Size)
    $g.ResetClip()

    # The navy is near-black, so on a dark taskbar the tile would have no edge at
    # all. This outline is what keeps the silhouette readable there.
    $strokeWidth = [Math]::Max(1.0, [single]($Size * 0.07))
    $outline = New-RoundedPath -Size $Size -Radius $radius -Inset ($strokeWidth / 2)
    $pen = New-Object System.Drawing.Pen($Accent, $strokeWidth)
    $g.DrawPath($pen, $outline)
    $pen.Dispose(); $outline.Dispose()

    # Three letters are a smudge below 48px, so the small sizes carry just the G.
    $text = if ($Size -ge 48) { "GMS" } else { "G" }
    $emSize = if ($Size -ge 48) { $Size * 0.30 } else { $Size * 0.52 }

    $font = New-Object System.Drawing.Font("Segoe UI Semibold", $emSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment     = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center

    # Shifted right by half the bar so the lettering optically centres in the
    # space left over, rather than in the tile.
    $box = New-Object System.Drawing.RectangleF($barWidth, 0, ($Size - $barWidth), $Size)
    $text_brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.DrawString($text, $font, $text_brush, $box, $format)

    $font.Dispose(); $format.Dispose(); $text_brush.Dispose()
    $back.Dispose(); $bar.Dispose(); $path.Dispose(); $g.Dispose()
    return $bmp
}

function ConvertTo-IconDib {
    param([System.Drawing.Bitmap]$Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    # BITMAPINFOHEADER. Height is doubled because the format expects the colour
    # image and the AND mask stacked, even when the alpha channel makes the mask
    # redundant.
    $bw.Write([UInt32]40)
    $bw.Write([Int32]$w)
    $bw.Write([Int32]($h * 2))
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]0)              # BI_RGB
    $bw.Write([UInt32]($w * $h * 4))
    0..3 | ForEach-Object { $bw.Write([UInt32]0) }

    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                             [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $row = New-Object byte[] ($w * 4)
        # DIBs run bottom-up.
        for ($y = $h - 1; $y -ge 0; $y--) {
            $line = [IntPtr]::Add($data.Scan0, $y * $data.Stride)
            [System.Runtime.InteropServices.Marshal]::Copy($line, $row, 0, $row.Length)
            $bw.Write($row)
        }
    }
    finally {
        $Bitmap.UnlockBits($data)
    }

    # AND mask: zeroed, so transparency comes from the alpha channel alone. Rows
    # are 1bpp padded out to a 4-byte boundary.
    $maskRow = [Math]::Floor(($w + 31) / 32) * 4
    $bw.Write((New-Object byte[] ($maskRow * $h)))

    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose(); $ms.Dispose()
    return $bytes
}

function Write-Ico {
    param([int[]]$Sizes, [string]$Path)

    $frames = @()

    # Only 256 is stored as PNG. GDI+ (and so System.Drawing.Icon, which is what
    # Form.Icon goes through at run time) cannot decode a PNG-compressed frame, and
    # fails on the whole file; the shell copes but the app would not. Every size the
    # app actually draws is therefore a plain DIB, as the Windows system icons are.
    foreach ($size in $Sizes) {
        $bmp = New-IconBitmap -Size $size
        if ($size -ge 256) {
            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames += , $ms.ToArray()
            $ms.Dispose()
        }
        else {
            $frames += , (ConvertTo-IconDib -Bitmap $bmp)
        }
        $bmp.Dispose()
    }

    $out = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($out)

    $w.Write([UInt16]0)               # reserved
    $w.Write([UInt16]1)               # 1 = icon
    $w.Write([UInt16]$Sizes.Count)

    $offset = 6 + (16 * $Sizes.Count)
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        $size = $Sizes[$i]
        $len = $frames[$i].Length
        # 256 is written as 0: the field is a single byte.
        $dim = if ($size -ge 256) { 0 } else { $size }

        $w.Write([byte]$dim)
        $w.Write([byte]$dim)
        $w.Write([byte]0)             # palette entries
        $w.Write([byte]0)             # reserved
        $w.Write([UInt16]1)           # colour planes
        $w.Write([UInt16]32)          # bits per pixel
        $w.Write([UInt32]$len)
        $w.Write([UInt32]$offset)
        $offset += $len
    }

    # Cast on the way out: PowerShell unrolls an array returned from a function, so
    # these arrive as Object[] and BinaryWriter would pick the wrong overload.
    foreach ($frame in $frames) { $w.Write([byte[]]$frame) }
    $w.Flush()

    [System.IO.File]::WriteAllBytes($Path, $out.ToArray())
    $w.Dispose(); $out.Dispose()

    Write-Host ("Wrote {0} ({1} KB, {2} sizes)" -f $Path, [Math]::Round((Get-Item $Path).Length / 1KB, 1), $Sizes.Count)
}

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent $here

# The shell wants the large sizes for Explorer's big-icon views; the browser never
# asks for more than 48 and re-fetches the favicon constantly, so it gets its own
# trimmed file rather than a 100KB copy of the desktop one.
Write-Ico -Sizes @(16, 32, 48, 64, 128, 256) -Path (Join-Path $here "gms.ico")
Write-Ico -Sizes @(16, 32, 48) -Path (Join-Path $repo "GMS.Web\wwwroot\favicon.ico")
