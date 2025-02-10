# Full configuration options can be found at https://www.consul.io/docs/agent/options.html

server = false

datacenter = "{{ .Env.CONSUL_DATACENTER }}"

data_dir = "C:\\consul\\data"

client_addr = "0.0.0.0"

bind_addr = "0.0.0.0"

ports {
  dns = 53
}

encrypt = "{{ .Env.CONSUL_ENCRYPTION_KEY }}"

retry_join = [{{ .Env.CONSUL_SERVER_IPS}}] # Our consul server

acl {
  tokens {
    default = "{{ .Env.CONSUL_TOKEN }}"
  }
}

tls {
  defaults {
    ca_file = "C:\\certificates\\consul\\ca.crt"
    cert_file = "C:\\certificates\\consul\\agent.crt"
    key_file = "C:\\certificates\\consul\\agent.key"

    #verify_incoming = true
    #verify_outgoing = true
    #verify_server_hostname = true
  }

  grpc {
    # https://github.com/hashicorp/nomad/issues/16854
    verify_incoming = false
  }

  internal_rpc {
    verify_server_hostname = true
  }
}

auto_encrypt = {
  tls = false
}
