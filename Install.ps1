param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$binary = Join-Path $PSScriptRoot "bin\$Configuration\net48\MEP_Compare_Project_From_JSON_ver1.dll"
if (!(Test-Path -LiteralPath $binary)) { throw 'Build the project first.' }
$addinRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2023'
$installDir = Join-Path $addinRoot 'BIMModelComparison'
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -LiteralPath $binary -Destination $installDir -Force
[xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'BIMModelComparison.addin') -Raw
$manifest.RevitAddIns.AddIn.Assembly = Join-Path $installDir 'MEP_Compare_Project_From_JSON_ver1.dll'
$manifest.Save((Join-Path $addinRoot 'BIMModelComparison.addin'))
Write-Host "Installed to $installDir. Restart Revit 2023."
