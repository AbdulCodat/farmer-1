#r @"..\Farmer\bin\Debug\netstandard2.0\Farmer.dll"

open Farmer
open Farmer.Resources

let myVm = vm {
    name "isaacsVM"
    username "isaac"
    vm_size Size.Standard_A2
    operating_system CommonImages.WindowsServer_2012Datacenter
    os_disk 128 StandardSSD_LRS
    add_ssd_disk 128
    add_slow_disk 512
    diagnostics_support
}

let template = arm {
    location NorthEurope
    add_resource myVm
}

template
|> Deploy.quick "my-resource-group-name"