#Requires -Version 7
<#
.SYNOPSIS
    Pre-compress web assets with Brotli for embedding in release builds.
#>
param(
    [string]$WwwRoot = "wwwroot"
)

$extensions = @('*.js', '*.css', '*.html', '*.txt', '*.json', '*.map', '*.svg')

Get-ChildItem -Path $WwwRoot -Recurse -Include $extensions | ForEach-Object {
    $src = $_.FullName
    $dst = "$src.br"

    $srcStream = [System.IO.File]::OpenRead($src)
    $dstStream = [System.IO.File]::Create($dst)
    $brotli = [System.IO.Compression.BrotliStream]::new($dstStream, [System.IO.Compression.CompressionLevel]::SmallestSize)

    $srcStream.CopyTo($brotli)

    $brotli.Dispose()
    $dstStream.Dispose()
    $srcStream.Dispose()

    $srcSize = (Get-Item $src).Length
    $dstSize = (Get-Item $dst).Length
    $ratio = [math]::Round((1 - $dstSize / $srcSize) * 100)

    Write-Host "  $($_.Name) -> $($_.Name).br ($srcSize -> $dstSize bytes, $ratio% reduction)"
}
