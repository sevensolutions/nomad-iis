New-Item -Path ..\nomad\server -ItemType Directory | Out-Null

$dataDir = Resolve-Path -Path ..\nomad\server
$configFile = Resolve-Path -Path .\server.hcl

Write-Host "Starting nomad server..."

..\nomad\nomad.exe agent -config="$configFile" -data-dir="$dataDir"
