# Copyright (c) HashiCorp, Inc.
# SPDX-License-Identifier: MPL-2.0

log_level = "TRACE"

bind_addr = "0.0.0.0"

server {
  enabled = true
  bootstrap_expect = 1
}

client {
  enabled = true
  network_interface = "Ethernet"
}

plugin "nomad_iis" {
  args = ["--management-api-port=5004", "--management-api-key=12345"]
  config {
    enabled = true,
    fingerprint_interval = "10s"
    allowed_target_websites = [ "Default Web Site" ]

    procdump {
      binary_path = "C:\\procdump.exe"
      accept_eula = true
    }
  }
}
