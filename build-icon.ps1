param(
    [string]$Source = "D:\Downloads\IconKitchen-Output\web\icon-512.png",
    [string]$OutIco = "D:\Dev\Project\Personal\VDScrollSwitch\app.ico"
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 32, 48, 256)
$src = [System.Drawing.Image]::FromFile($Source)

$pngBytesList = @()
foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($src, 0, 0, $size, $size)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytesList += ,$ms.ToArray()
    $bmp.Dispose()
}
$src.Dispose()

$fs = [System.IO.File]::Open($OutIco, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs

# ICONDIR
$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type = icon
$bw.Write([UInt16]$sizes.Count)

$headerSize = 6
$dirEntrySize = 16
$offset = $headerSize + ($dirEntrySize * $sizes.Count)

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $dataLen = $pngBytesList[$i].Length
    $dim = if ($size -ge 256) { 0 } else { $size }  # 0 means 256 in ICO format
    $bw.Write([Byte]$dim)      # width
    $bw.Write([Byte]$dim)      # height
    $bw.Write([Byte]0)         # color palette
    $bw.Write([Byte]0)         # reserved
    $bw.Write([UInt16]1)       # color planes
    $bw.Write([UInt16]32)      # bits per pixel
    $bw.Write([UInt32]$dataLen)
    $bw.Write([UInt32]$offset)
    $offset += $dataLen
}

foreach ($png in $pngBytesList) {
    $bw.Write($png)
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "Wrote $OutIco"
