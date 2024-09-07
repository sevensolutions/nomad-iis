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
  config {
    enabled = true,
	fingerprint_interval = "10s"
  }
}