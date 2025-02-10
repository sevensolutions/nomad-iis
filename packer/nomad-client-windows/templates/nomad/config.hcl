# Full configuration options can be found at https://www.nomadproject.io/docs/configuration

data_dir  = "C:\\nomad\\data"
plugin_dir = "C:\\nomad\\plugins"
bind_addr = "0.0.0.0"

server {
  enabled = false
}

client {
  enabled = true
  servers = ["10.0.30.110", "10.0.30.111", "10.0.30.112"] # Our nomad server
}

plugin "raw_exec" {
  config {
    enabled = true
  }
}

plugin "nomad_iis" {
  config {
    enabled = true
  }
}