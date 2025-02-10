#!/bin/sh

mkdir -p ./tools

# Download gomplate
if [ ! -f ./tools/gomplate ]; then
  wget -O ./tools/gomplate https://github.com/hairyhenderson/gomplate/releases/download/v4.3.0/gomplate_linux-amd64
  chmod +x ./tools/gomplate
fi

# Render template files
mkdir -p ./rendered

[ ! -f .env ] || export $(grep -v '^#' .env | xargs)

./tools/gomplate --input-dir ./templates --output-dir ./rendered

# Run packer build
#packer build -on-error=abort -var-file=.proxmox.pkrvars.hcl -var-file=./rendered/variables.pkrvars.hcl nomad-client-windows.pkr.hcl
