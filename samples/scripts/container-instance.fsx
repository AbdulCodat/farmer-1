#r @"./libs/Newtonsoft.Json.dll"
#r @"../../src/Farmer/bin/Debug/netstandard2.0/Farmer.dll"

open Farmer
open Farmer.Builders

let containerGroupUser = userAssignedIdentity {
    name "aciUser"
}

let template = arm {
    location Location.WestEurope
    add_resources [
        containerGroupUser
        containerGroup {
            name "isaac-container-group"
            operating_system Linux
            restart_policy ContainerGroup.AlwaysRestart
            add_identity containerGroupUser
            add_instances [
                containerInstance {
                    name "nginx"
                    image "nginx:1.17.6-alpine"

                    add_public_ports [ 80us; 443us ]
                    add_internal_ports [ 123us ]

                    memory 0.5<Gb>
                    cpu_cores 1
                }
            ]
        }
    ]
}

template.ToFile "generated-template"
template.Deploy "my-resource-group"