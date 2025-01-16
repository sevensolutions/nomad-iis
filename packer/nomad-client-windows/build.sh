#!/bin/sh

packer build -on-error=abort -var-file=.proxmox.pkrvars.hcl -var-file=variables.pkrvars.hcl nomad-client-windows.pkr.hcl
