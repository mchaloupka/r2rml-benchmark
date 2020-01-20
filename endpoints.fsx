module Endpoints

open System

#load "shell.fsx"
#load "docker.fsx"
#load "database.fsx"

open Shell
open Docker
open Database

type Endpoint = {
  name: string;
  outerPort: int;
  innerPort: int;
  dockerName: string;
  endpointUrl: string;
  supportedDatabases: Databases list;
  start: Databases -> unit;
}

let eviEndpoint = 
  let dockerName = "evi-endpoint"
  let innerPort = 80
  let outerPort = 5000
  
  {
    name="evi";
    innerPort=innerPort;
    outerPort=outerPort;
    dockerName=dockerName;
    endpointUrl="/api/sparql";
    supportedDatabases=[MsSql];
    start=
      function
        | MsSql ->
          inDocker dockerName "mchaloupka/slp.evi:latest"
          |> withMount mappingDir "/benchmark/mapping"
          |> withEnv "EVI_STORAGE__MAPPINGFILEPATH" "/benchmark/mapping/mapping.ttl"
          |> withEnv "EVI_STORAGE__CONNECTIONSTRING" "Server=database,1433;Database=benchmark;User Id=sa;Password=p@ssw0rd"
          |> withPort outerPort innerPort
          |> withNetwork benchmarkNetwork
          |> startContainerDetached

          Threading.Thread.Sleep(5000)
        | _ -> raise (new NotSupportedException())
  }

let ontopEndpoint =
  let dockerName = "ontop-endpoint"
  let innerPort = 8080
  let outerPort = 5051

  let propertiesFile = function
  | MsSql -> "/benchmark/static/ontop.mssql.properties"
  | MySql -> "/benchmark/static/ontop.mysql.properties"

  {
    name="ontop";
    innerPort=innerPort;
    outerPort=outerPort;
    dockerName=dockerName;
    endpointUrl="/sparql";
    supportedDatabases=[MsSql;MySql];
    start=fun database ->
      inDocker dockerName "ontop/ontop-endpoint"
      |> withMount staticDir "/benchmark/static"
      |> withMount jdbcDir "/opt/ontop/jdbc"
      |> withEnv "ONTOP_MAPPING_FILE" "/benchmark/static/mapping-ontop.obda"
      |> withEnv "ONTOP_PROPERTIES_FILE" (propertiesFile database)
      |> withPort innerPort outerPort
      |> withNetwork benchmarkNetwork
      |> startContainerDetached

      Threading.Thread.Sleep(15000)
  }