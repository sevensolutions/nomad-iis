# Full configuration options can be found at https://www.consul.io/docs/agent/options.html

server = false

datacenter = "dc1"

data_dir = "C:\\consul\\data"

client_addr = "0.0.0.0"

bind_addr = "0.0.0.0"
#advertise_addr = "10.0.30.x"

ports {
  dns = 53
}

encrypt = "+A34+uLaeEZHiVVOTNLqcWJRGNHOfoMKt3ztPGFIZdE="

retry_join = ["10.0.30.110", "10.0.30.111", "10.0.30.112"] # Our consul server

acl {
  tokens {
    default = "68e865f7-6f82-047d-7840-48f18c680a0c"
    #agent  = "5443db30-758e-e620-f43d-946de85f0a3b"
  }
}

tls {
  defaults {
    ca_file = "C:\\consul\\agent-certs\\ca.crt"
    cert_file = "C:\\consul\\agent-certs\\agent.crt"
    key_file = "C:\\consul\\agent-certs\\agent.key"

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
