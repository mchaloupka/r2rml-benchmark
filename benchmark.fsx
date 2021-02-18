module Benchmark

#load "shell.fsx"
#load "docker.fsx"
#load "database.fsx"
#load "endpoints.fsx"

open System
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
  |> withMount ttlDatasetDir "/bsbm/ttl-dataset"
  |> withMount mappingDir "/benchmark/mapping"
  |> commandInNewContainer
    (sprintf "bash -c \"./generate -pc %d -s sql -s mssql -s ttl && cp /bsbm/rdb2rdf/mapping.ttl /benchmark/mapping && cp /bsbm/dataset-2.ttl /bsbm/ttl-dataset\"" prodCount)

let getRunCount productCount =
  if productCount <= 10000 then 512
  else if productCount < 500000 then 256
  else 128

let runSingleBenchmark runCount outputSuffix clientCount endpoint includeLog =
  let persistLogCommand = if includeLog then (sprintf " && mv run.log /benchmark/run%s.log\"" outputSuffix) else ""

  let runBenchmark () =
    inDocker "bsbm-testdriver" "mchaloupka/bsbm-r2rml:latest"
    |> withMount tdDir "/bsbm/td_data"
    |> withMount outputDir "/benchmark"
    |> withNetwork benchmarkNetwork
    |> commandInNewContainer
      (sprintf "bash -c \"./testdriver -mt %d -runs %d -w 32 http://%s:%d%s && mv benchmark_result.xml /benchmark/result%s.xml %s" clientCount runCount endpoint.DockerName endpoint.InnerPort endpoint.EndpointUrl outputSuffix persistLogCommand)

  try
    runBenchmark ()
  with
  | ex ->
    logfn "Benchmark execution failed with: %A" ex
    logfn "Will retry"
    System.Threading.Thread.Sleep(30000)

    try
      runBenchmark ()
    with
    | ex ->
      logfn "Benchmark execution failed even second time with: %A" ex

let runDbBenchmark runCount outputSuffix database includeLog =
  let persistLogCommand = if includeLog then (sprintf " && mv run.log /benchmark/run%s.log\"" outputSuffix) else ""
  let mayBecommandPart = 
    match database with
    | WithoutRdb -> None
    | MsSql -> Some "-ucf usecases/explore/mssql.txt -dbdriver com.microsoft.sqlserver.jdbc.SQLServerDriver -sql \'jdbc:sqlserver://database:1433;databaseName=benchmark;user=sa;password=p@ssw0rd\'"
    | MySql -> Some "-ucf usecases/explore/sql.txt -dbdriver com.mysql.jdbc.Driver -sql jdbc:mysql://root:psw@database:3306/benchmark"

  let runBenchmark () =
    match mayBecommandPart with
    | Some commandPart ->
      inDocker "bsbm-testdriver" "mchaloupka/bsbm-r2rml:latest"
      |> withMount tdDir "/bsbm/td_data"
      |> withMount outputDir "/benchmark"
      |> withMount jdbcDir "/bsbm/jdbc"
      |> withNetwork benchmarkNetwork
      |> commandInNewContainer
        (sprintf "bash -c \"cp ./jdbc/* ./lib && ./testdriver -runs %d -w 32 %s && mv benchmark_result.xml /benchmark/result%s.xml %s" runCount commandPart outputSuffix persistLogCommand)
    | None -> ()

  try
    runBenchmark ()
  with
  | ex ->
    logfn "Benchmark execution failed with: %A" ex
    logfn "Will retry"
    System.Threading.Thread.Sleep(30000)
    
    try
      runBenchmark ()
    with
    | ex ->
      logfn "Benchmark execution failed even second time with: %A" ex

type BenchmarkConfiguration = {
  ProductCounts: int list
  ClientCounts: int list
  Databases: Databases list
  Endpoints: Endpoint list
  IncludeLogs: bool
  BenchmarkDatabase: bool
}

let createAndClearWorkingDirectories () =
  [
    datasetDir
    mySqlDatasetDir
    msSqlDatasetDir
    ttlDatasetDir
    tdDir
    mappingDir
  ] 
  |> List.iter createAndEmptyDirectory

let runBenchmark configuration prodCount =
  logfn " --- Running benchmark with prod count of %d ---" prodCount

  createAndClearWorkingDirectories ()

  generateData prodCount

  let runCount = getRunCount prodCount

  try
    createNetwork benchmarkNetwork
  
    configuration.Databases |> List.iter (fun database ->
      let supportingEndpoints =
        configuration.Endpoints 
        |> List.filter (fun x -> 
          x.SupportedDatabases 
          |> List.contains database)

      if not supportingEndpoints.IsEmpty then
        try
          try
            startDatabaseContainer database

            if configuration.BenchmarkDatabase then
              let outputSuffix = sprintf "-%s-%d-db-1" (database |> dbName) prodCount
              runDbBenchmark runCount outputSuffix database configuration.IncludeLogs

            for endpoint in supportingEndpoints do
              try
                endpoint.Start database
                for clientCount in configuration.ClientCounts do
                  let outputSuffix = sprintf "-%s-%d-%s-%d" (database |> dbName) prodCount endpoint.Name clientCount
                  runSingleBenchmark runCount outputSuffix clientCount endpoint configuration.IncludeLogs
              finally
                stopAndRemoveContainer endpoint.DockerName
          with
          | ex ->
            logfn "Benchmark failed with the following configuration %A, the exception was: %A" configuration ex
        finally
          match database with
          | WithoutRdb -> ()
          | _ -> stopAndRemoveContainer databaseDockerName
    )
  finally
    removeNetwork benchmarkNetwork

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
  ProductCounts = [ 10; 100; 1000; 10000; 100000; 200000; 500000 ]
  ClientCounts = [ 1; 2; 4; 8; 16; 32 ]
  Databases = [ WithoutRdb; MsSql; MySql ]
  Endpoints = [ eviEndpoint; ontopEndpoint; virtuosoEndpoint ]
  IncludeLogs = false
  BenchmarkDatabase = true
}

let quickTestBenchmarkConfiguration = {
  defaultBenchmarkConfiguration with
    ProductCounts = defaultBenchmarkConfiguration.ProductCounts |> List.head |> List.singleton
    ClientCounts = defaultBenchmarkConfiguration.ClientCounts |> List.head |> List.singleton
    IncludeLogs = true
}

let upperBoundaryBenchmarkConfiguration = {
  quickTestBenchmarkConfiguration with
    ProductCounts = defaultBenchmarkConfiguration.ProductCounts |> List.last |> List.singleton
    ClientCounts = defaultBenchmarkConfiguration.ClientCounts |> List.last |> List.singleton
}

let writeVersions configuration =
  let endpointVersions =
    configuration.Endpoints
    |> List.map (fun endpoint -> endpoint.Name, endpoint.GetVersion ())
  
  let databaseVersions =
    if configuration.BenchmarkDatabase then
      configuration.Databases
      |> List.choose (fun database ->
        database
        |> Database.tryGetVersion
        |> Option.map (fun version -> database |> Database.dbName, version)
      )
    else
      List.empty

  File.WriteAllLines(
    Path.Combine(outputDir, "version.csv"),
    ((endpointVersions @ databaseVersions) |> List.map (fun (x, y) -> sprintf "%s,%s" x y))
  )

let performBenchmark configuration =
  createAndEmptyDirectory outputDir
  configuration.ProductCounts |> List.iter (runBenchmark configuration)
  generateSummary ()
  configuration |> writeVersions

