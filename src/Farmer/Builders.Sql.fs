[<AutoOpen>]
module Farmer.Resources.SqlAzure

open Farmer

type Edition = Free | Basic | Standard of string | Premium of string

module Sku =
    let ``Free`` = Free
    let ``Basic`` = Basic
    let ``S0`` = Standard "S0"
    let ``S1`` = Standard "S1"
    let ``S2`` = Standard "S2"
    let ``S3`` = Standard "S3"
    let ``S4`` = Standard "S4"
    let ``S6`` = Standard "S6"
    let ``S7`` = Standard "S7"
    let ``S9`` = Standard "S9"
    let ``S12`` =Standard "S12"
    let ``P1`` = Premium "P1"
    let ``P2`` = Premium "P2"
    let ``P4`` = Premium "P4"
    let ``P6`` = Premium "P6"
    let ``P11`` = Premium "P11"
    let ``P15`` = Premium "P15"

type SqlAzureConfig =
    { ServerName : ResourceName
      AdministratorCredentials : {| UserName : string; Password : SecureParameter |}
      DbName : ResourceName
      DbEdition : Edition
      DbCollation : string
      Encryption : FeatureFlag
      FirewallRules : {| Name : string; Start : System.Net.IPAddress; End : System.Net.IPAddress |} list }
    /// Gets the ARM expression path to the FQDN of this VM.
    member this.FullyQualifiedDomainName =
        sprintf "reference(concat('Microsoft.Sql/servers/', variables('%s'))).fullyQualifiedDomainName" this.ServerName.Value
        |> ArmExpression
    /// Gets a basic .NET connection string using the administrator username / password.
    member this.ConnectionString =
        concat
            [ literal
                (sprintf "Server=tcp:%s.database.windows.net,1433;Initial Catalog=%s;Persist Security Info=False;User ID=%s;Password="
                    this.ServerName.Value
                    this.DbName.Value
                    this.AdministratorCredentials.UserName)
              this.AdministratorCredentials.Password.AsArmRef
              literal ";MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" ]

type SqlBuilder() =
    let makeIp = System.Net.IPAddress.Parse
    member __.Yield _ =
        { ServerName = ResourceName ""
          AdministratorCredentials = {| UserName = ""; Password = SecureParameter "" |}
          DbName = ResourceName ""
          DbEdition = Free
          DbCollation = "SQL_Latin1_General_CP1_CI_AS"
          Encryption = Disabled
          FirewallRules = [] }
    member __.Run(state) =
        if System.String.IsNullOrWhiteSpace state.AdministratorCredentials.UserName then failwith "You must specific an admin_username."
        else
            { state with
                ServerName = state.ServerName |> Helpers.santitiseDb |> ResourceName
                DbName = state.DbName |> Helpers.santitiseDb |> ResourceName
                AdministratorCredentials =
                    {| state.AdministratorCredentials with
                        Password = SecureParameter (sprintf "password-for-%s" state.ServerName.Value) |} }
    [<CustomOperation "server_name">]
    /// Sets the name of the SQL server.
    member __.ServerName(state:SqlAzureConfig, serverName) = { state with ServerName = serverName }
    member this.ServerName(state:SqlAzureConfig, serverName:string) = this.ServerName(state, ResourceName serverName)
    /// Sets the name of the database.
    [<CustomOperation "db_name">]
    member __.Name(state:SqlAzureConfig, name) = { state with DbName = name }
    member this.Name(state:SqlAzureConfig, name:string) = this.Name(state, ResourceName name)
    /// Sets the sku of the database.
    [<CustomOperation "sku">]
    member __.DatabaseEdition(state:SqlAzureConfig, edition:Edition) = { state with DbEdition = edition }
    /// Sets the collation of the database.
    [<CustomOperation "collation">]
    member __.Collation(state:SqlAzureConfig, collation:string) = { state with DbCollation = collation }
    /// Enables encryption of the database.
    [<CustomOperation "use_encryption">]
    member __.Encryption(state:SqlAzureConfig) = { state with Encryption = Enabled }
    /// Adds a custom firewall rule given a name, start and end IP address range.
    [<CustomOperation "add_firewall_rule">]
    member __.AddFirewallWall(state:SqlAzureConfig, name, startRange, endRange) =
        { state with
            FirewallRules =
                {| Name = name
                   Start = makeIp startRange
                   End = makeIp endRange |}
                :: state.FirewallRules }
    /// Adds a firewall rule that enables access to other Azure services.
    [<CustomOperation "enable_azure_firewall">]
    member this.UseAzureFirewall(state:SqlAzureConfig) =
        this.AddFirewallWall(state, "AllowAllMicrosoftAzureIps", "0.0.0.0", "0.0.0.0")
    /// Sets the admin username of the server (note: the password is supplied as a securestring parameter to the generated ARM template).
    [<CustomOperation "admin_username">]
    member __.AdminUsername(state:SqlAzureConfig, username) =
        { state with
            AdministratorCredentials =
                {| state.AdministratorCredentials with
                    UserName = username |} }

open WebApp
type WebAppBuilder with
    member this.DependsOn(state:WebAppConfig, sqlDb:SqlAzureConfig) =
        this.DependsOn(state, sqlDb.ServerName)
type FunctionsBuilder with
    member this.DependsOn(state:FunctionsConfig, sqlDb:SqlAzureConfig) =
        this.DependsOn(state, sqlDb.ServerName)

module Converters =
    open Farmer.Models
    let sql location (sql:SqlAzureConfig) =
        { ServerName = sql.ServerName
          Location = location
          Credentials =
            {| Username = sql.AdministratorCredentials.UserName
               Password = sql.AdministratorCredentials.Password |}
          DbName = sql.DbName
          DbEdition =
            match sql.DbEdition with
            | Edition.Basic -> "Basic"
            | Edition.Free -> "Free"
            | Edition.Standard _ -> "Standard"
            | Edition.Premium _ -> "Premium"
          DbObjective =
            match sql.DbEdition with
            | Edition.Basic -> "Basic"
            | Edition.Free -> "Free"
            | Edition.Standard s -> s
            | Edition.Premium p -> p
          DbCollation = sql.DbCollation
          TransparentDataEncryption = sql.Encryption
          FirewallRules = sql.FirewallRules
        }

let sql = SqlBuilder()

