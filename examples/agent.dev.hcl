# Copyright (c) HashiCorp, Inc.
# SPDX-License-Identifier: MPL-2.0

log_level = "TRACE"

plugin "nomad_iis" {
  args = ["--management-api-port=5004", "--management-api-key=12345", "--procdump-accept-eula=true"]
  config {
    enabled = true,
	fingerprint_interval = "10s"
	allowed_target_websites = [ "Default Web Site" ]
  }
}