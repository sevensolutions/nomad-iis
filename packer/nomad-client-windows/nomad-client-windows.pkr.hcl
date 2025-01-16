# ------------------------------------------------------------------------------
# Packer Plugins
# ------------------------------------------------------------------------------

packer {
    required_plugins {
        proxmox = {
            version = ">= 1.2.2"
            source = "github.com/hashicorp/proxmox"
        }
        windows-update = {
            version = "0.15.0"
            source = "github.com/rgl/windows-update"
        }
    }
}

# ------------------------------------------------------------------------------
# Variable Declarations
# ------------------------------------------------------------------------------

variable "proxmox_url" {
    type = string
}
variable "proxmox_token_id" {
    type = string
}
variable "proxmox_token_secret" {
    type = string
    sensitive  = true
}
variable "proxmox_insecure_skip_tls_verify" {
    type = bool
}

variable "vm_cpu_cores" {
    type = number
    default = 4
}
variable "vm_memory" {
    type = number
    default = 4096
}
variable "vm_disk_size" {
    type = string
    default = "40G"
}
variable "vm_vlan_tag" {
    type = string
}
variable "vm_windows_iso_file" {
    type = string
}
variable "windows_admin_username" {
    type = string
    default = "Administrator"
}
variable "windows_admin_password" {
    type = string
    default = "password"
}

# ------------------------------------------------------------------------------
# Source Definition
# ------------------------------------------------------------------------------

source "proxmox-iso" "nomad-client-windows" {
    # Proxmox Connection Settings
    proxmox_url = "${var.proxmox_url}"
    username = "${var.proxmox_token_id}"
    token = "${var.proxmox_token_secret}"
    insecure_skip_tls_verify = "${var.proxmox_insecure_skip_tls_verify}"

    # VM General Settings
    node = "pve"
    vm_name = "packer-nomad-client-windows"
    template_description = "HashiCorp Nomad Windows Client"

    # VM OS Settings
    os = "win11"
    bios = "ovmf"
    machine = "q35" # Dont know if we need this but it's q35 on our existing windows machine
    iso_file = "${var.vm_windows_iso_file}"
    iso_storage_pool = "local"
    unmount_iso = true

    # VM System Settings
    qemu_agent = true

    # VM Cloud-Init Settings
    cloud_init = true
    cloud_init_storage_pool = "local-lvm"

    # VM Hard Disk Settings
    scsi_controller = "virtio-scsi-single"

    disks {
        disk_size = "${var.vm_disk_size}"
        format = "raw"
        storage_pool = "local-lvm"
        type = "virtio"
    }

    efi_config {
        efi_storage_pool = "local-lvm"
        efi_type = "4m"
        pre_enrolled_keys = true
    }

    # VM CPU Settings
    cores = "${var.vm_cpu_cores}"
    
    # VM Memory Settings
    memory = "${var.vm_memory}"

    # VM Network Settings
    network_adapters {
        model = "virtio"
        bridge = "vmbr0"
        firewall = false
        vlan_tag = "${var.vm_vlan_tag}"
        mac_address = "repeatable"
    }

    # Packer Boot Command
    boot_wait = "5s"
    boot_command = ["<space><wait3s><space><wait3s><space><wait3s><space><wait3s><space><wait3s><space><wait3s><space><wait3s><space><wait3s><space>"]
    
    # Additional ISOs for setup scripts and drivers
    additional_iso_files {
        unmount = true
        device = "sata4"
        iso_storage_pool = "local"
        cd_files = [
            "files/setup/Autounattend.xml",
            "files/setup/bootstrap.ps1"
        ]
    }
    additional_iso_files {
        unmount = true
        device = "sata5"
        iso_storage_pool = "local"
        iso_file = "local:iso/virtio-win-0.1.229.iso"
    }

    # Packer Provisioning Access
    communicator = "winrm"
    winrm_username = "${var.windows_admin_username}"
    winrm_password = "${var.windows_admin_password}"
}

# ------------------------------------------------------------------------------
# Build Definition
# ------------------------------------------------------------------------------

build {
    sources = ["source.proxmox-iso.nomad-client-windows"]
    
    provisioner "powershell" {
        script = "./files/scripts/post-setup.ps1"
    }

    provisioner "windows-restart" {
        restart_timeout = "15m"
    }

    provisioner "powershell" {
        script = "./files/scripts/install-iis.ps1"
    }

    # provisioner "windows-restart" {
    #     restart_timeout = "15m"
    # }

    /*
    provisioner "windows-update" {
        search_criteria = "IsInstalled=0"
        update_limit = 10
    }

    provisioner "windows-restart" {
        restart_timeout = "15m"
    }

    provisioner "windows-update" {
        search_criteria = "IsInstalled=0"
        update_limit = 10
    }

    provisioner "windows-restart" {
        restart_timeout = "15m"
    }*/

    # https://developer.hashicorp.com/packer/docs/provisioners/file#directory-uploads
    provisioner "file" {
        source = "files/consul"
        destination = "C:\\"
    }
    provisioner "file" {
        source = "files/nomad"
        destination = "C:\\"
    }

    provisioner "file" {
        source = "files/cloud-init/install-services.ps1"
        destination = "C:\\install-services.ps1"
    }

    provisioner "powershell" {
        script = "./files/scripts/install-apps.ps1"
    }

    provisioner "powershell" {
        script = "./files/scripts/setup-firewall.ps1"
    }

    provisioner "file" {
        source      = "./files/cloud-init/cloud-init.ps1"
        destination = "C:\\Windows\\System32\\cloud-init.ps1"
    }

    provisioner "powershell" {
        script = "./files/scripts/cleanup.ps1"
    }

    provisioner "powershell" {
        inline = [
            "schtasks /create /tn \"cloud-init\" /sc onstart /rl highest /ru system /tr \"powershell.exe -file C:\\Windows\\System32\\cloud-init.ps1\""
        ]
    }

    provisioner "file" {
        source      = "./files/sysprep/unattend.xml"
        destination = "C:\\Windows\\System32\\Sysprep\\unattend.xml"
    }

    provisioner "powershell" {
        inline = [
            "C:\\Windows\\System32\\Sysprep\\sysprep.exe /oobe /generalize /quit /quiet /unattend:C:\\Windows\\System32\\Sysprep\\unattend.xml"
        ]
    }
}
