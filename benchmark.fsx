open System
open Microsoft.FSharp.Collections

let exec procName args =
  printfn "Shell: %s %s" procName args
  let proc = new Diagnostics.Process()
  proc.StartInfo <- Diagnostics.ProcessStartInfo()
  proc.StartInfo.FileName <- procName
  proc.StartInfo.Arguments <- args
  proc.Start() |> ignore
  proc.WaitForExit()

type Databases =
  | MsSql
  | MySql

type DockerMount = { source: string; destination: string; } with
  member this.ToCommand = sprintf "%s:%s" this.source this.destination

type DockerArgument =
  val name: string
  val imageName: string
  val mounts: DockerMount list
  val env: Map<string,string>

  new(name: string, imageName: string) = DockerArgument(name, imageName, [], Map.empty)
  new(name: string, imageName: string, mounts: DockerMount list, env: Map<string,string>) =
    { name = name; imageName = imageName; mounts = mounts; env = env }

  member this.ToCommand =
    let mountCommand =
      this.mounts 
      |> List.map (fun x -> sprintf "-v %s" x.ToCommand) 
      |> String.concat " "

    let envCommand =
      this.env
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-e %s=%s" k v)
      |> String.concat " "

    [
      "--name"
      this.name
      mountCommand
      envCommand
      this.imageName
    ] |> String.concat " "
   
  member this.WithMount mount =
    DockerArgument(this.name, this.imageName, mount :: this.mounts, this.env)

  member this.WithEnv key value =
    DockerArgument(this.name, this.imageName, this.mounts, this.env.Add(key, value))

let startContainerDetached (argument: DockerArgument) =
  printfn "Starting container %s" argument.name
  exec "docker" (sprintf "run -d %s" argument.ToCommand)

let execInContainer name command =
  exec "docker" (sprintf "exec %s %s" name command)

let commandInNewContainer (argument: DockerArgument) command =
  printfn "Startin command '%s' in container '%s'" command argument.name
  exec "docker" (sprintf "run --rm %s %s" argument.ToCommand command)

let stopAndRemoveContainer name =
  printfn "Stopping container %s" name
  exec "docker" (sprintf "stop %s" name)
  printfn "Removing container %s" name
  exec "docker" (sprintf "container rm %s" name)

let datasetDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "dataset")
let mySqlDatasetDir = System.IO.Path.Combine(datasetDir, "mysql")
let msSqlDatasetDir = System.IO.Path.Combine(datasetDir, "mssql")
let tdDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "td_data")
let mappingDir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "mapping")

let createAndEmptyDirectory path =
  let directory = IO.Directory.CreateDirectory(path)
  directory.GetFiles() |> Seq.iter (fun x -> x.Delete())
  directory.GetDirectories() |> Seq.iter (fun x -> x.Delete(true))

let withMount source destination (argument: DockerArgument) =
  argument
    .WithMount({ source = source; destination = destination })

let startDatabaseContainer = function
  | MsSql ->
    startContainerDetached
      (DockerArgument("database", "mcr.microsoft.com/mssql/server:2017-latest")
        |> withMount msSqlDatasetDir "/benchmark/dataset"
        |> fun x -> x.WithEnv "ACCEPT_EULA" "Y"
        |> fun x -> x.WithEnv "SA_PASSWORD" "p@ssw0rd")

    Threading.Thread.Sleep(15000) // Enough time to start MSSQL server

    IO.DirectoryInfo(msSqlDatasetDir).GetFiles()
      |> Seq.filter (fun x -> x.Extension = ".sql")
      |> Seq.iter (fun x ->
        execInContainer
          "database"
          (sprintf "/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P p@ssw0rd -i /benchmark/dataset/%s\"" x.Name)
      )
      
  | MySql ->
    startContainerDetached
      (DockerArgument("database", "mysql:latest")
        |> withMount mySqlDatasetDir "/benchmark/dataset"
        |> fun x -> x.WithEnv "MYSQL_ROOT_PASSWORD" "psw")

    Threading.Thread.Sleep(15000) // Enough time to start MySQL server

    IO.DirectoryInfo(mySqlDatasetDir).GetFiles()
      |> Seq.filter (fun x -> x.Extension = ".sql")
      |> Seq.iter (fun x ->
        execInContainer
          "database"
          (sprintf "mysql -u root -ppsw -e \"source /benchmark/dataset/%s\"" x.Name)
      )

let runBenchmark databases prodCount =
  printfn " --- Running benchmark with prod count of %d ---" prodCount

  [datasetDir; mySqlDatasetDir; msSqlDatasetDir; tdDir; mappingDir] |> List.iter createAndEmptyDirectory

  commandInNewContainer
    (DockerArgument("bsbm-generate", "mchaloupka/bsbm-r2rml:latest")
      |> withMount tdDir "/bsbm/td_data"
      |> withMount mySqlDatasetDir "/bsbm/dataset"
      |> withMount msSqlDatasetDir "/bsbm/dataset-1"
      |> withMount mappingDir "/benchmark/mapping")
    (sprintf "bash -c \"./generate -pc %d -s sql -s mssql ; cp /bsbm/rdb2rdf/mapping.ttl /benchmark/mapping\"" prodCount)
  
  databases |> List.iter (fun database ->
    try
      startDatabaseContainer database

      // TODO: Start endpoint, perform benchmark
    finally
      stopAndRemoveContainer "database"
  )
  
[ 20 ]
|> List.iter (runBenchmark [ MsSql; MySql ])