module Shell

open System

let logfn format =
  Printf.kprintf (printfn "%A: %s" DateTime.Now) format

let exec procName args =
  logfn "Shell: %s %s" procName args
  let proc = new Diagnostics.Process()
  proc.StartInfo <- Diagnostics.ProcessStartInfo()
  proc.StartInfo.FileName <- procName
  proc.StartInfo.Arguments <- args
  proc.StartInfo.UseShellExecute <- false
  proc.StartInfo.RedirectStandardOutput <- true

  let mutable lastOutput = DateTime.Now

  proc.OutputDataReceived.Add(fun e ->
    if e.Data <> null then
      logfn "%s" e.Data
      lastOutput <- DateTime.Now
  )

  proc.Start() |> ignore
  proc.BeginOutputReadLine()

  let timeBetweenChecks = TimeSpan.FromMinutes(10.0)
  let maxTimeBetweenOutput = TimeSpan.FromHours(4.0)

  while not(proc.WaitForExit(timeBetweenChecks.TotalMilliseconds |> int)) do
    let timeAfterLastOutput = DateTime.Now - lastOutput

    if timeAfterLastOutput > maxTimeBetweenOutput then
      logfn "Timeouted after:  %A" timeAfterLastOutput
      proc.Kill()
      raise (TimeoutException())
    else
      logfn "Waiting for next output for: %A" timeAfterLastOutput

  if proc.ExitCode <> 0 then 
      let error = sprintf "Error code: %d" proc.ExitCode
      logfn "%s" error
      raise (Exception(error))
    else
      logfn "OK"

let execToGetOutput procName args =
  logfn "Shell: %s %s" procName args
  let proc = new Diagnostics.Process()
  proc.StartInfo <- Diagnostics.ProcessStartInfo()
  proc.StartInfo.FileName <- procName
  proc.StartInfo.Arguments <- args
  proc.StartInfo.UseShellExecute <- false
  proc.StartInfo.RedirectStandardOutput <- true

  proc.Start() |> ignore

  let timeout = TimeSpan.FromHours(6.0)

  if proc.WaitForExit(timeout.TotalMilliseconds |> int) then
    if proc.ExitCode <> 0 then 
      let error = sprintf "Error code: %d" proc.ExitCode
      logfn "%s" error
      proc.StandardOutput.ReadToEnd ()
    else
      logfn "OK"
      proc.StandardOutput.ReadToEnd ()
  else
    logfn "Timeouted"
    proc.Kill()
    raise (TimeoutException())

let datasetDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "dataset")
let mySqlDatasetDir = System.IO.Path.Combine(datasetDir, "mysql")
let msSqlDatasetDir = System.IO.Path.Combine(datasetDir, "mssql")
let ttlDatasetDir = System.IO.Path.Combine(datasetDir, "ttl")
let tdDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "td_data")
let mappingDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "mapping")
let outputDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "output")
let staticDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "static")
let jdbcDir = System.IO.Path.Combine(staticDir, "jdbc")

let createAndEmptyDirectory path =
  let directory = IO.Directory.CreateDirectory(path)
  directory.GetFiles() |> Seq.iter (fun x -> x.Delete())
  directory.GetDirectories() |> Seq.iter (fun x -> x.Delete(true))
