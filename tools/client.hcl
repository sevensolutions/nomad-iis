# Copyright (c) HashiCorp, Inc.
# SPDX-License-Identifier: MPL-2.0

log_level = "TRACE"

bind_addr = "0.0.0.0"

server {
  enabled = false
}

ports {
  http = 4746
  rpc  = 4747
  serf = 4748
}

client {
  enabled = true
  network_interface = "Ethernet"
  servers = [ "127.0.0.1" ]
}

consul {
  client_auto_join = false
}

plugin "nomad_iis" {
  args = [
    "--management-api-port=5004",
    "--management-api-key=12345",
    "--management-api-jwt-secret=VETkEPWkaVTxWf7J4Mm20KJWOx2cK4S7VvoP3ybjh6fr9P9PXvyhlY8HV2Jgxm2O"
  ]

  config {
    enabled = true,
	fingerprint_interval = "10s"
  }
}