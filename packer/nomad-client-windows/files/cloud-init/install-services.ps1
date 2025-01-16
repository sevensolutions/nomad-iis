sc.exe create consul start= delayed-auto binpath= "C:\consul\consul.exe agent -config-file C:\consul\config.hcl"

sc.exe create nomad start= delayed-auto binpath= "C:\nomad\nomad.exe agent -config C:\nomad\config.hcl" depend= consul

# sc start consultpl