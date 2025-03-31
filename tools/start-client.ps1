New-Item -Path ..\nomad\client -ItemType Directory | Out-Null

$dataDir = Resolve-Path -Path ..\nomad\client
$pluginDir = Resolve-Path -Path ..\src\NomadIIS\bin\Debug\net9.0
$configFile = Resolve-Path -Path .\client.hcl

Write-Host "Starting nomad client..."

..\nomad\nomad.exe agent -config="$configFile" -data-dir="$dataDir" -plugin-dir="$pluginDir"
