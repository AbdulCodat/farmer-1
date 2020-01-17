[<AutoOpen>]
module Farmer.ArmBuilder

open Farmer.Resources
open Farmer.Models

/// Represents all configuration information to generate an ARM template.
type ArmConfig =
    { Parameters : string Set
      Outputs : (string * string) list
      Location : Location
      Resources : obj list }

type Deployment = 
    { Location : Location
      Template : ArmTemplate }

type ArmBuilder() =
    member __.Yield _ =
        { Parameters = Set.empty
          Outputs = List.empty
          Resources = List.empty
          Location = WestEurope }

    member __.Run (state:ArmConfig) =
        let output =
            { Parameters = state.Parameters |> Set.toList
              Outputs = state.Outputs
              Resources = [
                  for resource in state.Resources do
                      match resource with
                      | :? StorageAccountConfig as config ->
                          StorageAccount (Converters.storage state.Location config)
                      | :? WebAppConfig as config ->
                          let outputs = Converters.webApp state.Location config
                          WebApp outputs.WebApp
                          ServerFarm outputs.ServerFarm
                          match outputs.Ai with (Some ai) -> AppInsights ai | None -> ()
                      | :? FunctionsConfig as config ->
                          let outputs = config |> Converters.functions state.Location
                          WebApp outputs.WebApp
                          ServerFarm outputs.ServerFarm
                          match outputs.Ai with (Some ai) -> AppInsights ai | None -> ()
                          match outputs.Storage with (Some storage) -> StorageAccount storage | None -> ()
                      | :? ContainerGroupConfig as config ->
                          ContainerGroup (Converters.containerGroup state.Location config)
                      | :? CosmosDbConfig as config ->
                          let outputs = config |> Converters.cosmosDb state.Location
                          CosmosAccount outputs.Account
                          CosmosSqlDb outputs.SqlDb
                          yield! outputs.Containers |> List.map CosmosContainer
                      | :? SqlAzureConfig as config ->
                          SqlServer (Converters.sql state.Location config)
                      | :? VmConfig as config ->
                          let output = Converters.vm state.Location config
                          Vm output.Vm
                          Vnet output.Vnet
                          Ip output.Ip
                          Nic output.Nic
                          match output.Storage with Some storage -> StorageAccount storage | None -> ()
                      | :? SearchConfig as search ->
                          AzureSearch (Converters.search state.Location search)
                      | :? AppInsightsConfig as aiConfig ->
                          AppInsights (Converters.appInsights state.Location aiConfig)
                      | :? KeyVaultConfig as keyVaultConfig ->
                          let output = Converters.keyVault state.Location keyVaultConfig
                          KeyVault output.KeyVault
                          for secret in output.Secrets do
                            KeyVaultSecret secret
                      | :? CdnConfig as cdnConfig ->
                          CdnProfile (Converters.cdnProfile state.Location cdnConfig)
                      | resource ->
                          failwithf "Sorry, I don't know how to handle this resource of type '%s'." (resource.GetType().FullName) ]
                  |> List.groupBy(fun r -> r.ResourceName)
                  |> List.choose(fun (resourceName, instances) ->
                         match instances with
                         | [] ->
                            None
                         | [ resource ] ->
                            Some resource
                         | resource :: _ ->
                            printfn "Warning: %d resources were found with the same name of '%s'. The first one will be used." instances.Length resourceName.Value
                            Some resource)
                  }
        { Location = state.Location; Template = output }

    /// Creates an output value that will be returned by the ARM template.
    [<CustomOperation "output">]
    member __.Output (state, outputName, outputValue) : ArmConfig = { state with Outputs = (outputName, outputValue) :: state.Outputs }
    member this.Output (state:ArmConfig, outputName:string, (ResourceName outputValue)) = this.Output(state, outputName, outputValue)
    member this.Output (state:ArmConfig, outputName:string, (ArmExpression outputValue)) = this.Output(state, outputName, outputValue)
    member this.Output (state:ArmConfig, outputName:string, outputValue:string option) =
        match outputValue with
        | Some outputValue -> this.Output(state, outputName, outputValue)
        | None -> state
    member this.Output (state:ArmConfig, outputName:string, outputValue:ArmExpression option) =
        match outputValue with
        | Some outputValue -> this.Output(state, outputName, outputValue)
        | None -> state

    /// Sets the default location of all resources.
    [<CustomOperation "location">]
    member __.Location (state, location) : ArmConfig = { state with Location = location }

    /// Adds a resource to the template.
    [<CustomOperation "add_resource">]
    member __.AddResource(state, resource) : ArmConfig =
        { state with Resources = box resource :: state.Resources }

    /// Adds a collection of resources to the template.
    [<CustomOperation "add_resources">]
    member this.AddResources(state, resources) =
        (state, resources)
        ||> Seq.fold(fun state resource -> this.AddResource(state, resource))

let arm = ArmBuilder()