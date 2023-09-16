$NomadVersion = "1.6.2"
$NomadDownloadUrl = "https://releases.hashicorp.com/nomad/${NomadVersion}/nomad_${NomadVersion}_windows_amd64.zip"
$NomadZipFileLocation = ".\nomad\nomad_${NomadVersion}_windows_amd64.zip"

New-Item -Path .\nomad -ItemType Directory | Out-Null

Write-Host "Downloading nomad binary..."

Invoke-WebRequest $NomadDownloadUrl -OutFile $NomadZipFileLocation

Expand-Archive $NomadZipFileLocation -DestinationPath .\nomad -Force

Remove-Item $NomadZipFileLocation | Out-Null
