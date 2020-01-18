module Shell

open System

let exec procName args =
  printfn "Shell: %s %s" procName args
  let proc = new Diagnostics.Process()
  proc.StartInfo <- Diagnostics.ProcessStartInfo()
  proc.StartInfo.FileName <- procName
  proc.StartInfo.Arguments <- args
  proc.Start() |> ignore
  proc.WaitForExit()

  if proc.ExitCode <> 0 then 
    let error = sprintf "Error code: %d" proc.ExitCode
    printfn "%s" error
    raise (Exception(error))
  else
    printfn "OK"

let datasetDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "dataset")
let mySqlDatasetDir = System.IO.Path.Combine(datasetDir, "mysql")
let msSqlDatasetDir = System.IO.Path.Combine(datasetDir, "mssql")
let tdDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "td_data")
let mappingDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "mapping")
let outputDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "output")
let staticDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "static")
let jdbcDir = System.IO.Path.Combine(staticDir, "jdbc")

let createAndEmptyDirectory path =
  let directory = IO.Directory.CreateDirectory(path)
  directory.GetFiles() |> Seq.iter (fun x -> x.Delete())
  directory.GetDirectories() |> Seq.iter (fun x -> x.Delete(true))