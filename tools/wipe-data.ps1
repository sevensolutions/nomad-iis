$serverDataDir = Resolve-Path -Path ..\nomad\server
$clientDataDir = Resolve-Path -Path ..\nomad\client

Write-Host "Removing $serverDataDir..."
Remove-Item -Recurse -Force $serverDataDir

Write-Host "Removing $clientDataDir..."
Remove-Item -Recurse -Force $clientDataDir
