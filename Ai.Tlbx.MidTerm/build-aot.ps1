#Requires -Version 7
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try
{
    dotnet publish -c Release -r win-x64
}
finally
{
    Pop-Location
}
