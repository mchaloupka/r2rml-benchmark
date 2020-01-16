module Benchmark

#load "shell.fsx"
#load "docker.fsx"
#load "database.fsx"
#load "endpoints.fsx"

open Shell
open Docker
open Database
open Endpoints

let runBenchmark databases endpoints clientCounts prodCount =
  printfn " --- Running benchmark with prod count of %d ---" prodCount

  [
    datasetDir;
    mySqlDatasetDir; 
    msSqlDatasetDir; 
    tdDir; 
    mappingDir;
  ] 
  |> List.iter createAndEmptyDirectory

  inDocker "bsbm-generate" "mchaloupka/bsbm-r2rml:latest"
  |> withMount tdDir "/bsbm/td_data"
  |> withMount mySqlDatasetDir "/bsbm/dataset"
  |> withMount msSqlDatasetDir "/bsbm/dataset-1"
  |> withMount mappingDir "/benchmark/mapping"
  |> commandInNewContainer
    (sprintf "bash -c \"./generate -pc %d -s sql -s mssql ; cp /bsbm/rdb2rdf/mapping.ttl /benchmark/mapping\"" prodCount)
  
  databases |> List.iter (fun database ->
    let supportingEndpoints =
      endpoints 
      |> List.filter (fun x -> 
        x.supportedDatabases 
        |> List.contains database)

    if not supportingEndpoints.IsEmpty then 
      try
        startDatabaseContainer database

        for clientCount in clientCounts do
          for endpoint in supportingEndpoints do
            try
              endpoint.start database
  
              let outputSuffix = sprintf "-%s-%d-%s-%d" (database |> dbName) prodCount endpoint.name clientCount
   
              inDocker "bsbm-generate" "mchaloupka/bsbm-r2rml:latest"
              |> withMount tdDir "/bsbm/td_data"
              |> withMount outputDir "/benchmark"
              |> commandInNewContainer
                (sprintf "bash -c \"./testdriver -mt %d http://host.docker.internal:%d%s ; mv benchmark_result.xml /benchmark/result%s.xml ; mv run.log /benchmark/run%s.log\"" clientCount endpoint.port endpoint.endpointUrl outputSuffix outputSuffix)

            finally
              stopAndRemoveContainer endpoint.dockerName
      finally
        stopAndRemoveContainer databaseDockerName
  )