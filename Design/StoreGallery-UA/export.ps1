$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$galleryRoot = $PSScriptRoot
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $galleryRoot 'masters') -Filter '*.png' | Sort-Object Name)
if ($sourceFiles.Count -ne 6) { throw 'Expected six source cards.' }

function EdgeColor([Drawing.Bitmap]$image, [int]$y) {
    [long]$r=0; [long]$g=0; [long]$b=0; [int]$count=0
    for($x=0; $x -lt $image.Width; $x+=4) {
        $pixel=$image.GetPixel($x,$y); $r+=$pixel.R; $g+=$pixel.G; $b+=$pixel.B; $count++
    }
    return [Drawing.Color]::FromArgb([int]($r/$count),[int]($g/$count),[int]($b/$count))
}

function ExportCard([Drawing.Bitmap]$image,[int]$width,[int]$height,[string]$destination) {
    $canvas=New-Object Drawing.Bitmap($width,$height,([Drawing.Imaging.PixelFormat]::Format24bppRgb))
    $graphics=[Drawing.Graphics]::FromImage($canvas)
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
    $scale=[Math]::Min($width/$image.Width,$height/$image.Height)
    $dw=[int][Math]::Round($image.Width*$scale); $dh=[int][Math]::Round($image.Height*$scale)
    $dx=[int][Math]::Floor(($width-$dw)/2); $dy=[int][Math]::Floor(($height-$dh)/2)
    $top=New-Object Drawing.SolidBrush((EdgeColor $image 0))
    $bottom=New-Object Drawing.SolidBrush((EdgeColor $image ($image.Height-1)))
    $graphics.FillRectangle($top,0,0,$width,[int]($height/2))
    $graphics.FillRectangle($bottom,0,[int]($height/2),$width,$height)
    $attributes=New-Object Drawing.Imaging.ImageAttributes
    $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $graphics.DrawImage($image,([Drawing.Rectangle]::new($dx,$dy,$dw,$dh)),0,0,$image.Width,$image.Height,[Drawing.GraphicsUnit]::Pixel,$attributes)
    $canvas.Save($destination,[Drawing.Imaging.ImageFormat]::Png)
    $attributes.Dispose(); $top.Dispose(); $bottom.Dispose(); $graphics.Dispose(); $canvas.Dispose()
}

foreach($file in $sourceFiles) {
    $sourceImage=[Drawing.Bitmap]::FromFile($file.FullName)
    ExportCard $sourceImage 1080 1920 (Join-Path $galleryRoot ('google-play/'+$file.Name))
    ExportCard $sourceImage 1320 2868 (Join-Path $galleryRoot ('app-store-iphone/'+$file.Name))
    $sourceImage.Dispose()
}

$sheet=New-Object Drawing.Bitmap(1500,1830,([Drawing.Imaging.PixelFormat]::Format24bppRgb))
$sg=[Drawing.Graphics]::FromImage($sheet)
$sg.Clear([Drawing.ColorTranslator]::FromHtml('#F3EFE3'))
$sg.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$ink=New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#243E35'))
$font=New-Object Drawing.Font('Segoe UI',28,[Drawing.FontStyle]::Bold)
$small=New-Object Drawing.Font('Segoe UI',14)
$sg.DrawString('Тихий кемпінг · галерея українською',$font,$ink,36,20)
$sg.DrawString('6 художніх промоконцептів · порядок показу 01–06',$small,$ink,40,74)
for($i=0;$i -lt $sourceFiles.Count;$i++) {
    $im=[Drawing.Image]::FromFile($sourceFiles[$i].FullName)
    $x=36+($i%3)*490; $y=118+[int][Math]::Floor($i/3)*850
    $sg.DrawImage($im,$x,$y,450,800)
    $sg.DrawString(('{0:D2}' -f ($i+1)),$small,$ink,$x,($y+806))
    $im.Dispose()
}
$sheet.Save((Join-Path $galleryRoot 'gallery-overview.jpg'),[Drawing.Imaging.ImageFormat]::Jpeg)
$font.Dispose();$small.Dispose();$ink.Dispose();$sg.Dispose();$sheet.Dispose()

$failed=@()
foreach($format in @(@('google-play',1080,1920),@('app-store-iphone',1320,2868))) {
    $files=@(Get-ChildItem -LiteralPath (Join-Path $galleryRoot $format[0]) -Filter '*.png')
    foreach($file in $files) {
        $im=[Drawing.Image]::FromFile($file.FullName)
        if($im.Width -ne $format[1] -or $im.Height -ne $format[2] -or $im.PixelFormat -ne [Drawing.Imaging.PixelFormat]::Format24bppRgb) {$failed+=$file.Name}
        $im.Dispose()
    }
    Write-Output ($format[0]+': '+$files.Count+' PNG, '+$format[1]+'x'+$format[2]+', RGB without alpha')
}
if($failed.Count -gt 0){throw ('Export validation failed: '+($failed -join ', '))}
Write-Output 'All 12 exports validated; overview saved.'
