open System

let exec procName args =
  let proc = new Diagnostics.Process()
  proc.StartInfo <- Diagnostics.ProcessStartInfo()
  proc.StartInfo.FileName <- procName
  proc.StartInfo.Arguments <- args
  proc.Start() |> ignore
  proc.WaitForExit()

let startContainer name imageName command =
  printfn "Starting container %s" name
  exec "docker" (sprintf "run -d --name %s %s %s" name imageName command)

let removeContainer name =
  printfn "Removing container %s" name
  exec "docker" (sprintf "container rm %s" name)

let runBenchmark prodCount =
  printfn " --- Running benchmark with prod count of %d ---" prodCount
  startContainer "bsbm" "mchaloupka/bsbm-r2rml:latest" ""
  try
    ()
  finally
    removeContainer "bsbm"
  
runBenchmark 20