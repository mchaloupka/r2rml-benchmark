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
}

let eviEndpoint = 
  let dockerName = "evi-endpoint"
  let innerPort = 80
  let outerPort = 5000
  
  {
    Name="evi"
    InnerPort=innerPort
    OuterPort=outerPort
    DockerName=dockerName
    EndpointUrl="/api/sparql"
    SupportedDatabases=[MsSql]
    Start=
      function
        | MsSql ->
          inDocker dockerName "mchaloupka/slp.evi:latest"
          |> withMount mappingDir "/benchmark/mapping"
          |> withEnv "EVI_STORAGE__MAPPINGFILEPATH" "/benchmark/mapping/mapping.ttl"
          |> withEnv "EVI_STORAGE__CONNECTIONSTRING" "Server=database,1433;Database=benchmark;User Id=sa;Password=p@ssw0rd"
          |> withPort outerPort innerPort
          |> withNetwork benchmarkNetwork
          |> startContainerDetached

          Threading.Thread.Sleep(20000)
        | _ -> raise (new NotSupportedException())
  }

let ontopEndpoint =
  let dockerName = "ontop-endpoint"
  let innerPort = 8080
  let outerPort = 5051

  let propertiesFile = function
  | WithoutRdb -> NotSupportedException "Unsupported without DB" |> raise
  | MsSql -> "/benchmark/static/ontop.mssql.properties"
  | MySql -> "/benchmark/static/ontop.mysql.properties"

  {
    Name="ontop"
    InnerPort=innerPort
    OuterPort=outerPort
    DockerName=dockerName
    EndpointUrl="/sparql"
    SupportedDatabases=[MsSql; MySql]
    Start=fun database ->
      inDocker dockerName "ontop/ontop-endpoint"
      |> withMount staticDir "/benchmark/static"
      |> withMount jdbcDir "/opt/ontop/jdbc"
      |> withMount mappingDir "/benchmark/mapping"
      |> withEnv "ONTOP_MAPPING_FILE" "/benchmark/mapping/mapping.ttl"
      |> withEnv "ONTOP_PROPERTIES_FILE" (propertiesFile database)
      |> withPort outerPort innerPort
      |> withNetwork benchmarkNetwork
      |> startContainerDetached

      Threading.Thread.Sleep(30000)
  }

let virtuosoEndpoint =
  let dockerName = "virtuoso-endpoint"
  let innerPort = 8890
  let outerPort = 5052

  {
    Name="virtuoso"
    InnerPort=innerPort
    OuterPort=outerPort
    DockerName=dockerName
    EndpointUrl="/sparql"
    SupportedDatabases=[WithoutRdb]
    Start=fun _ ->
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
  }
