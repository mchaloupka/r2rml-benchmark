module Docker

open System
#load "shell.fsx"
open Shell

type DockerArgument = { 
    Name: string
    ImageName: string
    Network: string option
    Mounts: Map<string, string>
    EnvVariables: Map<string, string>
    Ports: Map<int, int>
}

let inDocker name imageName = { 
    Name = name
    ImageName = imageName
    Network = None
    Mounts = Map.empty
    EnvVariables = Map.empty
    Ports = Map.empty 
}

let withMount source destination dockerArgument = {
    dockerArgument 
    with Mounts = dockerArgument.Mounts.Add(source, destination)
}

let withEnv name value dockerArgument = {
    dockerArgument
    with EnvVariables = dockerArgument.EnvVariables.Add(name, value)
}

let withPort source destination dockerArgument = {
    dockerArgument 
    with Ports = dockerArgument.Ports.Add(source, destination)
}

let withNetwork networkName dockerArgument = {
  dockerArgument
  with Network = Some(networkName)
}

let private toCommand dockerArgument =
    let mountCommand =
      dockerArgument.Mounts 
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-v \"%s:%s\"" k v) 
      |> String.concat " "

    let envCommand =
      dockerArgument.EnvVariables
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-e \"%s=%s\"" k v)
      |> String.concat " "

    let portsCommand =
      dockerArgument.Ports
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-p %d:%d" k v)
      |> String.concat " "

    let networkCommand =
      match dockerArgument.Network with
      | Some x -> sprintf "--net=%s" x
      | _ -> ""

    [
      "--name"
      dockerArgument.Name
      mountCommand
      envCommand
      portsCommand
      networkCommand
      dockerArgument.ImageName
    ] 
    |> List.filter (String.IsNullOrEmpty >> not) 
    |> String.concat " "

let startContainerDetached argument =
  logfn "Starting container %s" argument.Name
  exec "docker" (sprintf "run -d %s" (argument |> toCommand))

let execInContainer name command =
  exec "docker" (sprintf "exec %s %s" name command)

let commandInNewContainer command argument =
  logfn "Starting command '%s' in container '%s'" command argument.Name
  exec "docker" (sprintf "run --rm %s %s" (argument |> toCommand) command)

let outputOfCommandInNewContainer command argument =
  logfn "Starting command '%s' in container '%s'" command argument.Name
  execToGetOutput "docker" (sprintf "run --rm %s %s" (argument |> toCommand) command)

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