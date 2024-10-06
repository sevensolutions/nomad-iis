# Copyright (c) HashiCorp, Inc.
# SPDX-License-Identifier: MPL-2.0

log_level = "TRACE"

# Example JWT Token:
# eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6InN0YXRpYyJ9.eyJpc3MiOiJOb21hZElJUyIsImF1ZCI6Ik1hbmFnZW1lbnRBcGkiLCJzdWIiOiIxMjM0NTY3ODkwIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjk5OTk5OTk5OTksImpvYiI6WyIqIl0sImFsbG9jSWQiOlsiKiJdfQ.8MZL54z4pBw9pFk3jP4Yqy7kuKLgjeEXdaEdWI6GmgM

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