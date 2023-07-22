$ProtoBaseLocation = ".\src\NomadIIS\plugins"

Write-Host "Updating proto files..."

Invoke-WebRequest "https://raw.githubusercontent.com/hashicorp/go-plugin/main/internal/plugin/grpc_broker.proto" -OutFile "$ProtoBaseLocation\grpc_broker.proto"
Invoke-WebRequest "https://raw.githubusercontent.com/hashicorp/go-plugin/main/internal/plugin/grpc_controller.proto" -OutFile "$ProtoBaseLocation\grpc_controller.proto"
Invoke-WebRequest "https://raw.githubusercontent.com/hashicorp/go-plugin/main/internal/plugin/grpc_stdio.proto" -OutFile "$ProtoBaseLocation\grpc_stdio.proto"

Invoke-WebRequest "https://raw.githubusercontent.com/hashicorp/nomad/main/plugins/base/proto/base.proto" -OutFile "$ProtoBaseLocation\base\base.proto"

Invoke-WebRequest "https://raw.githubusercontent.com/hashicorp/nomad/main/plugins/drivers/proto/driver.proto" -OutFile "$ProtoBaseLocation\drivers\proto\driver.proto"

Invoke-WebRequest "https://raw.githubusercontent.com/hashicorp/nomad/main/plugins/shared/hclspec/hcl_spec.proto" -OutFile "$ProtoBaseLocation\shared\hclspec\hcl_spec.proto"
Invoke-WebRequest "https://raw.githubusercontent.com/hashicorp/nomad/main/plugins/shared/structs/proto/attribute.proto" -OutFile "$ProtoBaseLocation\shared\structs\proto\attribute.proto"
