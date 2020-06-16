﻿open Farmer
open Farmer.Builders

//TODO: Create resources here!

//TODO: Testing out the user experience

let privateNetwork = vnet {
    name "private-vnet"
    add_address_spaces [
        "10.30.0.0/16"
    ]
    add_subnets [
        subnet {
            name "ContainerSubnet"
            prefix "10.30.19.0/24"
            add_delegations [
                "Microsoft.ContainerInstance/containerGroups"
            ]
        }
    ]
}

let aciProfile = networkProfile {
    name "vnet-aci-profile"
    vnet "private-vnet"
    subnet "ContainerSubnet"
    // Typically just one subnet is needed
    // but instead they might want to add many, like this
    (* add_interface_configs [
        interface_config {
            add_ipconfigs [
                ip_config { subnet "ContainerSubnet" }
            ]
        }
    ]*)
}



let myContainer = container {
    name "container1"
    image "aci-hello-world"
    network_profile "vnet-aci-profile"
    // the old member (marked obsolete)
    ip_address (ContainerGroup.PrivateAddressWithIp (System.Net.IPAddress.Parse "10.100.200.3")) [TCP, 80us]
    // prefer one of these
    public_dns "my-container" [TCP, 80us]
    private_ip [TCP, 80us]
    private_static_ip "10.100.200.3" [TCP, 80us]
}

let deployment = arm {
    location Location.NorthEurope
    
    //TODO: Assign resources here using the add_resource keyword
    add_resources [
        myContainer
        aciProfile
        privateNetwork
    ]
}

// Generate the ARM template here...
deployment
|> Writer.quickWrite @"generated-template"

// Or deploy it directly to Azure here... (required Azure CLI installed!)
// deployment
// |> Deploy.execute "my-resource-group" Deploy.NoParameters