
# Variables
$global:os=""

function whichWindows {
  $version=(Get-WMIObject win32_operatingsystem).name
  if ($version) {
    switch -Regex ($version) {
        '(Server 2016)' {
            $global:os="2016"
            printWindowsVersion
        }
        '(Server 2019)' {
            $global:os="2019"
            printWindowsVersion
        }
        '(Server 2022)' {
            $global:os="2022"
            printWindowsVersion
        }
        '(Microsoft Windows Server Standard|Microsoft Windows Server Datacenter)'{
            $ws_version=(Get-WmiObject win32_operatingsystem).buildnumber
                switch -Regex ($ws_version) {
                    '16299' {
                        $global:os="1709"
                        printWindowsVersion
                    }
                    '17134' {
                        $global:os="1803"
                        printWindowsVersion
                    }
                    '17763' {
                        $global:os="1809"
                        printWindowsVersion
                    }
                    '18362' {
                        $global:os="1903"
                        printWindowsVersion
                    }
                    '18363' {
                        $global:os="1909"
                        printWindowsVersion
                    }
                    '19041' {
                        $global:os="2004"
                        printWindowsVersion
                    }
                    '19042' {
                        $global:os="20H2"
                        printWindowsVersion
                    }
                }
        }
        '(Windows 10)' {
            Write-Output '[INFO] - Windows 10 found'
            $global:os="10"
            printWindowsVersion
        }
        default {
            Write-Output "unknown"
            printWindowsVersion
        }
    }
  }
  else {
    throw "Buildnumber empty, cannot continue."
  }
}

function printWindowsVersion {
    if ($global:os) {
        Write-Output "[INFO] - Windows Server "$global:os" found."
    }
    else {
        Write-Output "[INFO] - Unknown version of Windows Server found."
        exit(1)
    }
}

# - Mandatory generic stuff
Write-Output "[START] - Start of Post-Setup script"

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Import-Module ServerManager

# let's check which windows
whichWindows

# 1709/1803/1809/1903/2019/2022
if ($global:os -notlike '2016') {
    Enable-NetFirewallRule -DisplayGroup "Windows Defender Firewall Remote Management" -Verbose
}
# 2016
if ($global:os -eq '2016') {
    Enable-NetFirewallRule -DisplayGroup "Windows Firewall Remote Management" -Verbose
}

# Features and Firewall rules common for all Windows Servers
try {
    Install-WindowsFeature NET-Framework-45-Core,Telnet-Client,RSAT-Role-Tools -IncludeManagementTools
    Install-WindowsFeature SNMP-Service,SNMP-WMI-Provider -IncludeManagementTools

    Enable-NetFirewallRule -DisplayGroup "Remote Desktop" -Verbose
    Enable-NetFirewallRule -DisplayGroup "File and Printer Sharing" -Verbose
    Enable-NetFirewallRule -DisplayGroup "Remote Service Management" -Verbose
    Enable-NetFirewallRule -DisplayGroup "Performance Logs and Alerts" -Verbose
    Enable-NetFirewallRule -DisplayGroup "Windows Management Instrumentation (WMI)" -Verbose
    Enable-NetFirewallRule -DisplayGroup "Remote Service Management" -Verbose
    Enable-NetFirewallRule -DisplayName "File and Printer Sharing (Echo Request - ICMPv4-In)" -Verbose
}
catch {
    Write-Output "[ERROR] - setting firewall went wrong"
}

# Terminal services and sysprep registry entries
try {
    Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server'-name "fDenyTSConnections" -Value 0 -Verbose -Force
    Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp' -name "UserAuthentication" -Value 0 -Verbose -Force
    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'HideFileExt' -Value 0 -Verbose -Force
    Set-ItemProperty -Path 'HKLM:\SYSTEM\Setup\Status\SysprepStatus' -Name 'GeneralizationState' -Value 7 -Verbose -Force
}
catch {
    Write-Output "[ERROR] - setting registry went wrong"
}

if ($global:os -eq '2019') {
    try {
        # Workaround for SYSTEM account adding keys to RSA folder
        $keyFolder="$Env:ALLUSERSPROFILE\Microsoft\Crypto\RSA\MachineKeys"
        $keyUsers=@("SYSTEM")
        
        foreach ($keyUser in $keyUsers) {
            $acl = Get-Acl "$keyFolder"
            $argument = New-Object System.Security.AccessControl.FileSystemAccessRule("$keyuser", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
            $acl.SetAccessRule($argument)
            Set-Acl "$keyFolder" $Acl
        }

        Write-Output "[INFO] - Workaround for PrivateKeys permission completed succesfully"
    }

    catch {
        Write-Output "[WARN] - Workaround for PrivateKeys permission failed"
    }
}

# Remove 260 Character Path Limit
if (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem') {
    Write-Host "Removing 260 Character Path Limit"
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -name "LongPathsEnabled" -Value 1 -Verbose -Force
}

try {
    Set-TimeZone -Id "Central European Standard Time" -Verbose
}
catch {
    Write-Output "Phase 2 [INFO] - set timezone went wrong"
    exit(1)
}
try {
    Write-Output "Phase 2 [INFO] - Setting high performance power plan"
    powercfg.exe /s 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c
}
catch {
    Write-Output "Phase 2 [ERROR] - Set powercfg went wrong"
    exit(1)
}

Write-Output "[END] - End of Post-Setup script"
exit(0)
