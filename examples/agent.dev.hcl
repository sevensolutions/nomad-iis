# Copyright (c) HashiCorp, Inc.
# SPDX-License-Identifier: MPL-2.0

log_level = "TRACE"

plugin "nomad_iis" {
  config {
    enabled = true,
	fingerprint_interval = "10s"
	allowed_target_websites = [ "Default Web Site" ]
  }
}