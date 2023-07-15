# Copyright (c) HashiCorp, Inc.
# SPDX-License-Identifier: MPL-2.0

log_level = "TRACE"

plugin "nomad_iis" {
  config {
    enabled = true,
	stats_interval = "1s"
	fingerprint_interval = "10s"
  }
}