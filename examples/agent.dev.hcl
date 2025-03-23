# Copyright (c) HashiCorp, Inc.
# SPDX-License-Identifier: MPL-2.0

log_level = "TRACE"

plugin "nomad_iis" {
  args = [
    "--management-api-port=5004",
    "--management-api-key=12345",
    "--management-api-jwt-secret=VETkEPWkaVTxWf7J4Mm20KJWOx2cK4S7VvoP3ybjh6fr9P9PXvyhlY8HV2Jgxm2O"
  ]
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