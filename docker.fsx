module Docker

open System
#load "shell.fsx"
open Shell

type DockerArgument = { 
    name: string
    imageName: string
    network: string option
    mounts: Map<string, string>
    envVariables: Map<string, string>
    ports: Map<int, int>
}

let inDocker name imageName = { 
    name=name
    imageName=imageName
    network=None
    mounts=Map.empty
    envVariables=Map.empty
    ports=Map.empty 
}

let withMount source destination dockerArgument = {
    dockerArgument 
    with mounts=dockerArgument.mounts.Add(source, destination)
}

let withEnv name value dockerArgument = {
    dockerArgument
    with envVariables=dockerArgument.envVariables.Add(name, value)
}

let withPort source destination dockerArgument = {
    dockerArgument 
    with ports=dockerArgument.ports.Add(source, destination)
}

let withNetwork networkName dockerArgument = {
  dockerArgument
  with network=Some(networkName)
}

let private toCommand dockerArgument =
    let mountCommand =
      dockerArgument.mounts 
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-v \"%s:%s\"" k v) 
      |> String.concat " "

    let envCommand =
      dockerArgument.envVariables
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-e \"%s=%s\"" k v)
      |> String.concat " "

    let portsCommand =
      dockerArgument.ports
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-p %d:%d" k v)
      |> String.concat " "

    let networkCommand =
      match dockerArgument.network with
      | Some x -> sprintf "--net=%s" x
      | _ -> ""

    [
      "--name"
      dockerArgument.name
      mountCommand
      envCommand
      portsCommand
      networkCommand
      dockerArgument.imageName
    ] 
    |> List.filter (String.IsNullOrEmpty >> not) 
    |> String.concat " "

let startContainerDetached argument =
  logfn "Starting container %s" argument.name
  exec "docker" (sprintf "run -d %s" (argument |> toCommand))

let execInContainer name command =
  exec "docker" (sprintf "exec %s %s" name command)

let commandInNewContainer command argument =
  logfn "Starting command '%s' in container '%s'" command argument.name
  exec "docker" (sprintf "run --rm -v %s %s" (argument |> toCommand) command)

let stopAndRemoveContainer name =
  try
    logfn "Stopping container %s" name
    exec "docker" (sprintf "stop %s" name)
    logfn "Removing container %s" name
    exec "docker" (sprintf "container rm -vf %s" name)
  with
  | ex ->
    logfn "Failed to remove container %A because of %A" name ex

let createNetwork name =
  logfn "Creating network %s" name
  exec "docker" (sprintf "network create %s" name)

let removeNetwork name =
  logfn "Removing network %s" name
  exec "docker" (sprintf "network rm %s" name)

let benchmarkNetwork = "benchmark-net"