module Endpoints

open System

#load "shell.fsx"
#load "docker.fsx"
#load "database.fsx"

open Shell
open Docker
open Database

type Endpoint = {
  Name: string
  OuterPort: int
  InnerPort: int
  DockerName: string
  EndpointUrl: string
  SupportedDatabases: Databases list
  Start: Databases -> unit
  GetVersion: unit -> string
}

let eviEndpoint = 
  let dockerName = "evi-endpoint"
  let innerPort = 8080
  let outerPort = 5000
  let imageName = "mchaloupka/slp.evi:latest"
  
  {
    Name = "evi"
    InnerPort = innerPort
    OuterPort = outerPort
    DockerName = dockerName
    EndpointUrl = "/api/sparql"
    SupportedDatabases = [ MsSql; MySql ]
    Start = fun database ->
      let databaseType, connectionString =
        match database with
        | MsSql -> "MsSql", "Server=database,1433;Database=benchmark;User Id=sa;Password=p@ssw0rd"
        | MySql -> "MySql", "Server=database;Port=3306;User ID=root;Password=psw;Database=benchmark"
        | _ -> failwithf "Not supported database: %A" database

      inDocker dockerName imageName
      |> withMount mappingDir "/benchmark/mapping"
      |> withEnv "EVI_STORAGE__DATABASETYPE" databaseType
      |> withEnv "EVI_STORAGE__MAPPINGFILEPATH" "/benchmark/mapping/mapping.ttl"
      |> withEnv "EVI_STORAGE__CONNECTIONSTRING" connectionString
      |> withPort outerPort innerPort
      |> withNetwork benchmarkNetwork
      |> startContainerDetached

      Threading.Thread.Sleep(20000)
    GetVersion = fun () ->
      inDocker dockerName imageName
      |> outputOfCommandInNewContainer "--version"
      |> fun x -> x.Trim()
  }

let ontopEndpoint =
  let dockerName = "ontop-endpoint"
  let innerPort = 8080
  let outerPort = 5051
  let imageName = "ontop/ontop"

  let propertiesFile = function
  | WithoutRdb -> NotSupportedException "Unsupported without DB" |> raise
  | MsSql -> "/benchmark/static/ontop.mssql.properties"
  | MySql -> "/benchmark/static/ontop.mysql.properties"

  {
    Name = "ontop"
    InnerPort = innerPort
    OuterPort = outerPort
    DockerName = dockerName
    EndpointUrl = "/sparql"
    SupportedDatabases = [ MsSql; MySql ]
    Start = fun database ->
      inDocker dockerName imageName
      |> withMount staticDir "/benchmark/static"
      |> withMount jdbcDir "/opt/ontop/jdbc"
      |> withMount mappingDir "/benchmark/mapping"
      |> withEnv "ONTOP_MAPPING_FILE" "/benchmark/mapping/mapping.ttl"
      |> withEnv "ONTOP_PROPERTIES_FILE" (propertiesFile database)
      |> withPort outerPort innerPort
      |> withNetwork benchmarkNetwork
      |> startContainerDetached

      Threading.Thread.Sleep(30000)
    GetVersion = fun () ->
      inDocker dockerName imageName
      |> withEntryPoint "java"
      |> outputOfCommandInNewContainer "-cp ./lib/* it.unibz.inf.ontop.cli.Ontop --version"
      |> fun x -> x.Trim()
  }

let virtuosoEndpoint =
  let dockerName = "virtuoso-endpoint"
  let innerPort = 8890
  let outerPort = 5052
  let imageName = "openlink/virtuoso-opensource-7:latest"

  {
    Name = "virtuoso"
    InnerPort = innerPort
    OuterPort = outerPort
    DockerName = dockerName
    EndpointUrl = "/sparql"
    SupportedDatabases = WithoutRdb |> List.singleton
    Start = fun _ ->
      inDocker dockerName "openlink/virtuoso-opensource-7:latest"
      |> withMount ttlDatasetDir "/benchmark/ttl"
      |> withEnv "DBA_PASSWORD" "psw"
      |> withEnv "VIRT_Parameters_DirsAllowed" "/benchmark" 
      |> withPort outerPort innerPort
      |> withNetwork benchmarkNetwork
      |> startContainerDetached

      Threading.Thread.Sleep(20000)

      execInContainer
        dockerName
        "isql 1111 dba psw \"EXEC=DB.DBA.TTLP_MT(file_to_string_output (\'/benchmark/ttl/dataset-2.ttl\'),\'\',\'http://bsbm.org\', 0);\""
    GetVersion = fun () ->
      inDocker dockerName imageName
      |> outputOfCommandInNewContainer "version"
      |> fun x -> x.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
      |> Array.skip 1
      |> Array.map (fun x -> x.Trim ())
      |> fun x -> String.Join(" :: ", x)
  }
