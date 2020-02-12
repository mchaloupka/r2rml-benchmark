module Benchmark

#load "shell.fsx"
#load "docker.fsx"
#load "database.fsx"
#load "endpoints.fsx"

open System.IO
open System.Text.RegularExpressions
open System.Xml

open Shell
open Docker
open Database
open Endpoints

let generateData prodCount =
  inDocker "bsbm-generate" "mchaloupka/bsbm-r2rml:latest"
  |> withMount tdDir "/bsbm/td_data"
  |> withMount mySqlDatasetDir "/bsbm/dataset"
  |> withMount msSqlDatasetDir "/bsbm/dataset-1"
  |> withMount mappingDir "/benchmark/mapping"
  |> commandInNewContainer
    (sprintf "bash -c \"./generate -pc %d -s sql -s mssql && cp /bsbm/rdb2rdf/mapping.ttl /benchmark/mapping\"" prodCount)

let runSingleBenchmark outputSuffix clientCount endpoint includeLog =
  let persistLogCommand = if includeLog then (sprintf " && mv run.log /benchmark/run%s.log\"" outputSuffix) else ""

  let runBenchmark () =
    inDocker "bsbm-testdriver" "mchaloupka/bsbm-r2rml:latest"
    |> withMount tdDir "/bsbm/td_data"
    |> withMount outputDir "/benchmark"
    |> withNetwork benchmarkNetwork
    |> commandInNewContainer
      (sprintf "bash -c \"./testdriver -mt %d -runs 192 -w 32 http://%s:%d%s && mv benchmark_result.xml /benchmark/result%s.xml %s" clientCount endpoint.dockerName endpoint.innerPort endpoint.endpointUrl outputSuffix persistLogCommand)

  try
    runBenchmark ()
  with
  | ex ->
    printfn "Benchmark execution failed with: %A" ex
    printfn "Will retry"
    System.Threading.Thread.Sleep(30000)
    runBenchmark ()

let runDbBenchmark outputSuffix database includeLog =
  let persistLogCommand = if includeLog then (sprintf " && mv run.log /benchmark/run%s.log\"" outputSuffix) else ""
  let commandPart = 
    match database with
    | MsSql -> "-ucf usecases/explore/mssql.txt -dbdriver com.microsoft.sqlserver.jdbc.SQLServerDriver -sql \'jdbc:sqlserver://database:1433;databaseName=benchmark;user=sa;password=p@ssw0rd\'"
    | MySql -> "-ucf usecases/explore/sql.txt -dbdriver com.mysql.jdbc.Driver -sql jdbc:mysql://root:psw@database:3306/benchmark"

  let runBenchmark () =
    inDocker "bsbm-testdriver" "mchaloupka/bsbm-r2rml:latest"
    |> withMount tdDir "/bsbm/td_data"
    |> withMount outputDir "/benchmark"
    |> withMount jdbcDir "/bsbm/jdbc"
    |> withNetwork benchmarkNetwork
    |> commandInNewContainer
      (sprintf "bash -c \"cp ./jdbc/* ./lib && ./testdriver -runs 192 -w 32 %s && mv benchmark_result.xml /benchmark/result%s.xml %s" commandPart outputSuffix persistLogCommand)

  try
    runBenchmark ()
  with
  | ex ->
    printfn "Benchmark execution failed with: %A" ex
    printfn "Will retry"
    System.Threading.Thread.Sleep(30000)
    runBenchmark ()

type BenchmarkConfiguration = {
  productCounts: int list
  clientCounts: int list
  databases: Databases list
  endpoints: Endpoint list
  includeLogs: bool
  benchmarkDatabase: bool
}

let runBenchmark configuration prodCount =
  printfn " --- Running benchmark with prod count of %d ---" prodCount

  [
    datasetDir
    mySqlDatasetDir
    msSqlDatasetDir
    tdDir
    mappingDir
  ] 
  |> List.iter createAndEmptyDirectory

  generateData prodCount
  
  configuration.databases |> List.iter (fun database ->
    let supportingEndpoints =
      configuration.endpoints 
      |> List.filter (fun x -> 
        x.supportedDatabases 
        |> List.contains database)

    if not supportingEndpoints.IsEmpty then
      try
        createNetwork benchmarkNetwork

        try
          startDatabaseContainer database

          if configuration.benchmarkDatabase then
            let outputSuffix = sprintf "-%s-%d-db-1" (database |> dbName) prodCount
            runDbBenchmark outputSuffix database configuration.includeLogs

          for clientCount in configuration.clientCounts do
            for endpoint in supportingEndpoints do
              try
                endpoint.start database
    
                let outputSuffix = sprintf "-%s-%d-%s-%d" (database |> dbName) prodCount endpoint.name clientCount
     
                runSingleBenchmark outputSuffix clientCount endpoint configuration.includeLogs

              finally
                stopAndRemoveContainer endpoint.dockerName
        finally
          stopAndRemoveContainer databaseDockerName
      finally
        removeNetwork benchmarkNetwork        
  )

let (|Regex|_|) pattern input =
  let m = Regex.Match(input, pattern)
  if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
  else None

let generateSummary () =
  let fileSplit (fileInfo: FileInfo) =
    match fileInfo.Name with
    | Regex @"result-([^-]+)-([^-]+)-([^-]+)-([^-]+).xml" [ db; productCount; endpoint; clientCount ] ->
      Some((fileInfo, db, productCount, endpoint, clientCount))
    | _ -> None

  let files =
    DirectoryInfo(outputDir).GetFiles()
    |> Seq.choose fileSplit

  let loadQmph (fileInfo: FileInfo) =
    let doc = XmlDocument()
    doc.Load fileInfo.FullName

    let qmphNode =
      doc.SelectNodes "/bsbm/querymix/qmph/text()"
      |> Seq.cast<XmlNode>
      |> Seq.map (fun n -> n.Value)
      |> Seq.head

    let queryQmphNodes =
      doc.SelectNodes "/bsbm/queries/query/qps/text()"
      |> Seq.cast<XmlNode>
      |> Seq.map (fun n ->
        let value = n.Value
        let queryNode = n.ParentNode.ParentNode
        let queryNr = 
          queryNode.Attributes
          |> Seq.cast<XmlAttribute> 
          |> Seq.filter (fun x -> x.LocalName = "nr")
          |> Seq.map (fun x -> x.Value)
          |> Seq.head

        queryNr, value
      )
      |> Map.ofSeq

    qmphNode, queryQmphNodes

  let queryNrs =
    seq { 1..12 } 
    |> Seq.map (fun x -> x.ToString()) 
    |> Seq.toList

  let summaryRows = 
    files 
    |> Seq.map (fun (fileInfo, db, productCount, endpoint, clientCount) ->
      let (qmph, queryQmph) = loadQmph fileInfo

      let queriesRecord =
        queryNrs
        |> List.map (fun nr -> 
          match queryQmph.TryGetValue nr with 
          | true, value -> value
          | false, _ -> ""
        )

      let rowRecords = 
        [
          db
          productCount
          endpoint
          clientCount
          qmph
        ] @ queriesRecord

      rowRecords |> String.concat ",")
    |> Seq.toList
  
  let summaryHeader =
    [
      "Database"
      "ProductCount"
      "Endpoint"
      "ClientCount"
      "TotalQmph"
    ] @ (queryNrs |> List.map (sprintf "Qps-%s"))
    |> String.concat ","

  File.WriteAllLines(
    Path.Combine(outputDir, "summary.csv"),
    (summaryHeader :: summaryRows)
  )

let defaultBenchmarkConfiguration = {
  productCounts=[ 10; 100; 1000; 10000; 100000; 200000; 500000; 1000000 ]
  clientCounts=[ 1; 2; 4; 8; 16; 32 ]
  databases=[ MsSql; MySql ]
  endpoints=[ eviEndpoint; ontopEndpoint ]
  includeLogs=false
  benchmarkDatabase=true
}

let quickTestBenchmarkConfiguration = {
  defaultBenchmarkConfiguration with
    productCounts=[defaultBenchmarkConfiguration.productCounts.Head]
    clientCounts=[defaultBenchmarkConfiguration.clientCounts.Head]
    includeLogs=true
}

let upperBoundaryBenchmarkConfiguration = {
  quickTestBenchmarkConfiguration with
    productCounts=[(defaultBenchmarkConfiguration.productCounts |> List.last)]
    clientCounts=[(defaultBenchmarkConfiguration.clientCounts |> List.last)]
}

let performBenchmark configuration =
  createAndEmptyDirectory outputDir
  configuration.productCounts |> List.iter (runBenchmark configuration)
  generateSummary ()

