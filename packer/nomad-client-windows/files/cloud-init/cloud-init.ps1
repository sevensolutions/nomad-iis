# This is my workaround to setup the hostname, because Windows has no cloud-init.

# Get all file system drives
$drives = Get-PSDrive -PSProvider 'FileSystem'

# Loop through each drive to find the cloud-init drive
foreach ($drive in $drives) {
  $filePath = $drive.Root + "OPENSTACK\LATEST\USER_DATA"

  if (Test-Path $filePath) {
    break;
  }
}

if (($filePath) -And (Test-Path $filePath)) {
  Write-Host "Found cloud-init drive: $drive"

  $userData = Get-Content -Path $filePath

  $hostname = $userData | Select-String '(?smi)^hostname: ?(.*)$' -AllMatches | %{ $_.Matches } | %{ $_.Groups[1] } | %{ $_.Value }

  if ($hostname) {
    Write-Host "Current Hostname: $env:computername"
    Write-Host "Expected Hostname: $hostname"

    if ($env:computername -ine $hostname) {
      Write-Host "Updating Hostname..."
      Rename-Computer -NewName "$hostname" -Restart
    }
  }
}