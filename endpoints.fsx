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
  port: int;
  dockerName: string;
  endpointUrl: string;
  supportedDatabases: Databases list;
  start: Databases -> unit;
}

let eviEndpoint = {
  name="evi";
  port=5000;
  dockerName="evi-endpoint";
  endpointUrl="/api/sparql";
  supportedDatabases=[MsSql];
  start=
    function
      | MsSql ->
        inDocker "evi-endpoint" "mchaloupka/slp.evi:latest"
        |> withMount mappingDir "/benchmark/mapping"
        |> withEnv "EVI_STORAGE__MAPPINGFILEPATH" "/benchmark/mapping/mapping.ttl"
        |> withEnv "EVI_STORAGE__CONNECTIONSTRING" "Server=host.docker.internal,1433;Database=benchmark;User Id=sa;Password=p@ssw0rd"
        |> withPort 5000 80
        |> startContainerDetached
      | _ -> raise (new NotSupportedException())
}

// The following should start ontop docker:
// docker run --rm -it -v {...}:/opt/ontop/jdbc -v {...}:/static -e ONTOP_MAPPING_FILE=/static/mapping.ttl -e ONTOP_PROPERTIES_FILE=/static/ontop.mysql.properties -p 8080:8080 ontop/ontop-endpoint
// Currently it does not work (see https://github.com/ontop/ontop/issues/324)
