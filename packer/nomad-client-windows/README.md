# Windows Node Golden Image for Proxmox

This folder contains a [Packer](https://www.packer.io/) definition to build a golden image for a Windows Node, containing the Nomad-IIS plugin.
At the moment this is only targeting [Proxmox](https://www.proxmox.com/en/), because it's the virtualization platform i'am using, but you can adapt it to others like VMware ESX etc.

> [!CAUTION]  
> This Packer definition is provided as-is and should be treated as a template or starting-point. It may be necessary to adapt it to your own environment before executing it. So please review it carefully.

## Getting Started

> [!TIP]  
> Prepare a dedicated Linux VM for this task. It needs to have network access to your Proxmox cluster.

Install Packer on your machine by following the [official documentation](https://developer.hashicorp.com/packer/install).

Clone the repository and run `packer init`. This will download the necessary dependencies.

```bash
git clone https://github.com/sevensolutions/nomad-iis.git

cd nomad-iis/packer/nomad-client-windows

packer init .
```

## Configuration

Create a `.proxmox.pkrvars.hcl` file with the following content and adjust the settings accordingly:

```
proxmox_url = "https://your-proxmox:8006/api2/json"
proxmox_token_id = "..."
proxmox_token_secret = "..."
proxmox_insecure_skip_tls_verify = true
```

Copy `sample.env` to `.env` and adjust the file to your needs.

Copy your consul and nomad certificate files to `./certificates/consul`, `./certificates/nomad`.
The files should be named `ca.crt`, `agent.crt` and `agent.key`.

Review the following files and adjust them to your needs:

- nomad-client-windows.pkr.hcl
- build.sh

## Build the Image

Once you've reviewed and adjusted all the settings, simply run `build.sh` to start the build-process. This process takes 15 to 30 minutes to complete.

```bash
./build.sh
```
