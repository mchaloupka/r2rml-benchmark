module Docker

open System
#load "shell.fsx"
open Shell

type DockerArgument = { 
    name: string; 
    imageName: string;
    mounts: Map<string, string>;
    envVariables: Map<string, string>;
    ports: Map<int, int>;
}

let inDocker name imageName = { 
    name=name; 
    imageName=imageName; 
    mounts=Map.empty; 
    envVariables=Map.empty; 
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

let private toCommand dockerArgument =
    let mountCommand =
      dockerArgument.mounts 
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-v \"%s\":\"%s\"" k v) 
      |> String.concat " "

    let envCommand =
      dockerArgument.envVariables
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-e \"%s\"=\"%s\"" k v)
      |> String.concat " "

    let portsCommand =
      dockerArgument.ports
      |> Map.toList
      |> List.map (fun (k, v) -> sprintf "-p %d:%d" k v)
      |> String.concat " "

    [
      "--name"
      dockerArgument.name
      mountCommand
      envCommand
      portsCommand
      dockerArgument.imageName
    ] 
    |> List.filter (String.IsNullOrEmpty >> not) 
    |> String.concat " "

let startContainerDetached argument =
  printfn "Starting container %s" argument.name
  exec "docker" (sprintf "run -d %s" (argument |> toCommand))

let execInContainer name command =
  exec "docker" (sprintf "exec %s %s" name command)

let commandInNewContainer command argument =
  printfn "Starting command '%s' in container '%s'" command argument.name
  exec "docker" (sprintf "run --rm %s %s" (argument |> toCommand) command)

let stopAndRemoveContainer name =
  printfn "Stopping container %s" name
  exec "docker" (sprintf "stop %s" name)
  printfn "Removing container %s" name
  exec "docker" (sprintf "container rm %s" name)