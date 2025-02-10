$consulVersion = "{{ .Env.CONSUL_VERSION }}"
$consulDownloadUrl = "https://releases.hashicorp.com/consul/${consulVersion}/consul_${consulVersion}_windows_amd64.zip"
$consulZipFileLocation = "consul_${consulVersion}_windows_amd64.zip"

Write-Host "Downloading consul binary..."

Invoke-WebRequest $consulDownloadUrl -OutFile $consulZipFileLocation -UseBasicParsing
Unblock-File $consulZipFileLocation

Expand-Archive $consulZipFileLocation -DestinationPath C:\consul -Force

Remove-Item $consulZipFileLocation | Out-Null



$nomadVersion = "{{ .Env.NOMAD_VERSION }}"
$nomadDownloadUrl = "https://releases.hashicorp.com/nomad/${nomadVersion}/nomad_${nomadVersion}_windows_amd64.zip"
$nomadZipFileLocation = "nomad_${nomadVersion}_windows_amd64.zip"

Write-Host "Downloading nomad binary..."

Invoke-WebRequest $nomadDownloadUrl -OutFile $nomadZipFileLocation -UseBasicParsing
Unblock-File $nomadZipFileLocation

Expand-Archive $nomadZipFileLocation -DestinationPath C:\nomad -Force

Remove-Item $nomadZipFileLocation | Out-Null


$nomadIisVersion = "{{ .Env.NOMAD_IIS_VERSION }}"
$nomadIisDownloadUrl = "https://github.com/sevensolutions/nomad-iis/releases/download/v${nomadIisVersion}/nomad_iis.zip"
$nomadIisZipFileLocation = "nomad_iis.zip"

New-Item -Path C:\nomad\plugins -ItemType Directory | Out-Null

Write-Host "Downloading nomad_iis plugin..."

Invoke-WebRequest $nomadIisDownloadUrl -OutFile $nomadIisZipFileLocation -UseBasicParsing
Unblock-File $nomadIisZipFileLocation

Expand-Archive $nomadIisZipFileLocation -DestinationPath C:\nomad\plugins -Force

Remove-Item $nomadIisZipFileLocation | Out-Null

# Register Binaries in PATH
$paths = "C:\consul;C:\nomad;"
[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::Machine) + ";$paths", [EnvironmentVariableTarget]::Machine)
