[<AutoOpen>]
module Farmer.Arm.ContainerInstance

open Farmer
open Farmer.ContainerGroup
open Farmer.CoreTypes

let containerGroups = ResourceType "Microsoft.ContainerInstance/containerGroups"

type ContainerGroupIpAddress =
    { Type : IpAddressType
      Ports :
        {| Protocol : TransmissionProtocol
           Port : uint16 |} Set }

type ContainerGroup =
    { Name : ResourceName
      Location : Location
      ContainerInstances :
        {| Name : ResourceName
           Image : string
           Ports : uint16 Set
           Cpu : int
           Memory : float<Gb>
           EnvironmentVariables: Map<string, {| Value:string; Secure:bool |}>
        |} list
      OperatingSystem : OS
      RestartPolicy : RestartPolicy
      IpAddress : ContainerGroupIpAddress
      NetworkProfile : ResourceName option }
    member this.NetworkProfilePath =
        this.NetworkProfile
        |> Option.map (fun networkProfile ->
            ArmExpression.resourceId(Network.networkProfiles, networkProfile))

    interface IArmResource with
        member this.ResourceName = this.Name
        member this.JsonModel =
            {| ``type`` = containerGroups.ArmValue
               apiVersion = "2018-10-01"
               name = this.Name.Value
               location = this.Location.ArmValue
               dependsOn = this.NetworkProfilePath |> Option.map(fun r -> r.Eval()) |> Option.toList
               properties =
                   {| containers =
                       this.ContainerInstances
                       |> List.map (fun container ->
                           {| name = container.Name.Value.ToLowerInvariant ()
                              properties =
                               {| image = container.Image
                                  ports = container.Ports |> Set.map (fun port -> {| port = port |})
                                  environmentVariables =
                                      container.EnvironmentVariables
                                      |> Seq.map (fun kvp ->
                                          if kvp.Value.Secure then
                                              {| name = kvp.Key; value=null; secureValue=kvp.Value.Value |}
                                          else
                                              {| name = kvp.Key; value=kvp.Value.Value; secureValue=null |})
                                  resources =
                                   {| requests =
                                       {| cpu = container.Cpu
                                          memoryInGB = container.Memory |}
                                   |}
                               |}
                           |})
                      osType = string this.OperatingSystem
                      restartPolicy =
                        match this.RestartPolicy with
                        | AlwaysRestart -> "Always"
                        | NeverRestart -> "Never"
                        | RestartOnFailure -> "OnFailure"
                      ipAddress =
                        {| ``type`` =
                            match this.IpAddress.Type with
                            | PublicAddress | PublicAddressWithDns _ -> "Public"
                            | PrivateAddress _ | PrivateAddressWithIp _ -> "Private"
                           ports = [
                               for port in this.IpAddress.Ports do
                                {| protocol = string port.Protocol
                                   port = port.Port |}
                           ]
                           ip =
                            match this.IpAddress.Type with
                            | PrivateAddressWithIp ip -> string ip
                            | _ -> null
                           dnsNameLabel =
                            match this.IpAddress.Type with
                            | PublicAddressWithDns dnsLabel -> dnsLabel
                            | _ -> null
                        |}
                      networkProfile =
                        this.NetworkProfilePath
                        |> Option.map(fun path -> box {| id = path.Eval() |})
                        |> Option.toObj
                   |}
            |} :> _