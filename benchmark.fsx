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
    (sprintf "bash -c \"./generate -pc %d -s sql -s mssql ; cp /bsbm/rdb2rdf/mapping.ttl /benchmark/mapping\"" prodCount)

let runSingleBenchmark outputSuffix clientCount endpoint = 
  inDocker "bsbm-generate" "mchaloupka/bsbm-r2rml:latest"
  |> withMount tdDir "/bsbm/td_data"
  |> withMount outputDir "/benchmark"
  |> commandInNewContainer
    (sprintf "bash -c \"./testdriver -mt %d -runs 100 -w 30 http://host.docker.internal:%d%s ; mv benchmark_result.xml /benchmark/result%s.xml ; mv run.log /benchmark/run%s.log\"" clientCount endpoint.port endpoint.endpointUrl outputSuffix outputSuffix)

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

  generateData prodCount
  
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
   
              runSingleBenchmark outputSuffix clientCount endpoint

            finally
              stopAndRemoveContainer endpoint.dockerName
      finally
        stopAndRemoveContainer databaseDockerName
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