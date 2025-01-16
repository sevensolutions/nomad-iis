Write-Output "[START] - Cleaning/zeroing/compacting"

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Clean WU downloads
try {
    Stop-Service -Name wuauserv -Force -Verbose -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 10
    Stop-Service -Name cryptsvc -Force -Verbose -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 10
    
    if (Test-Path -Path "$env:systemroot\SoftwareDistribution\Download" ) {
        Write-Output "[INFO]: Cleaning Updates..."
        Remove-Item "$env:systemroot\SoftwareDistribution\Download\*" -Recurse -Force -Verbose -ErrorAction SilentlyContinue
    }

    if (Test-Path -Path "$env:systemroot\Prefetch") {
        Write-Output "[INFO]: Cleaning Prefetch..."
        Remove-Item "$env:systemroot\Prefetch\*.*" -Recurse -Force -Verbose -ErrorAction SilentlyContinue
    }
}
catch {
    Write-Output "[WARN]: Cleaning failed."
}

# Disable Windows Error Reporting
Write-Host "Disable Windows Error Reporting..."
Disable-WindowsErrorReporting -ErrorAction SilentlyContinue
# remove logs
wevtutil el | Foreach-Object {wevtutil cl "$_"}

# ResetBase/thin WinSxS
dism /online /cleanup-image /StartComponentCleanup /ResetBase
dism /online /cleanup-Image /SPSuperseded

# Remove leftovers from deploy
if ((Test-Path -Path "$env:systemroot\Temp") -and ("$env:systemroot")) {
    Write-Output "[INFO]: Cleaning TEMP"
    Remove-Item $env:systemroot\Temp\* -Exclude "packer-*","script-*" -Recurse -Force -ErrorAction SilentlyContinue
}

# Optimize Disk
Write-Output "[INFO] Defragging..."
if (Get-Command Optimize-Volume -ErrorAction SilentlyContinue) {
    Optimize-Volume -DriveLetter C -Defrag -verbose
} else {
    Defrag.exe c: /H
}

# TODOPEI: Can't we use sdelete? or cipher?
# https://serverfault.com/questions/165070/how-to-zero-fill-a-virtual-disks-free-space-on-windows-for-better-compression
# Write-Output "[INFO] Zeroing out empty space..."
#  $startDTM = (Get-Date)
#  $FilePath="c:\zero.tmp"
#  $Volume = Get-WmiObject win32_logicaldisk -filter "DeviceID='C:'"
#  $ArraySize= 4096kb
#  $SpaceToLeave= $Volume.Size * 0.001
#  $FileSize= $Volume.FreeSpace - $SpacetoLeave
#  $ZeroArray= new-object byte[]($ArraySize)

#  $Stream= [io.File]::OpenWrite($FilePath)
#  try {
#     $CurFileSize = 0
#      while($CurFileSize -lt $FileSize) {
#          $Stream.Write($ZeroArray,0, $ZeroArray.Length)
#          $CurFileSize +=$ZeroArray.Length
#      }
#  }
#  finally {
#      if($Stream) {
#          $Stream.Close()
#      }
#  }
#  Remove-Item $FilePath -Recurse -Force -ErrorAction SilentlyContinue
#  $endDTM = (Get-Date)
#  Write-Output "Phase 5d [INFO] Zeroing took: $(($endDTM-$startDTM).totalseconds) seconds"

Write-Output "[END]"
exit 0