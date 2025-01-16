
# Create Firewall Rule for Nomad dynamic ports
New-NetFirewallRule -DisplayName "Allow Nomad Dynamic Ports 20000-32000" -Action Allow -Direction Inbound -Protocol TCP -LocalPort 20000-32000

# Create Firewall Rule for Prometheus windows_exporter
New-NetFirewallRule -DisplayName "Allow Prometheus on Port 9182" -Action Allow -Direction Inbound -Protocol TCP -LocalPort 9182
