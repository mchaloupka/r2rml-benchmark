# r2rml-benchmark
Scripting to perform a benchmark of virtual SPARQL endpoints based on R2RML mappings.

## Prerequisities
* Docker
* .NET Core SDK 3.1 or newer

Has been currently tested only on Windows. Should be easy to add support for other platforms, please create a pull request if you want to add support.

## Running the benchmark
To run the benchmark, run the following commands:
```
dotnet fsi main.fsx
```

## Filling database / running endpoints
It is possible also to run also parts of the scripts, using the F# interactive console.

First, start the interactive console:
```
dotnet fsi
```

To start using the commands, you need to load the scripts
```
#load "benchmark.fsx";;
```

After that, generate the dataset (replace the number with desired prod count)
```
Benchmark.generateData 20;;
```

With the generated dataset, you can easily start the database in a docker network
```
open Database
open Docker
createNetwork benchmarkNetwork
startDatabaseContainer Databases.MsSql
;;
```

Now, with everything available, it is possible to start the endpoint
```
open Endpoints
eviEndpoint.start Databases.MsSql
;;
```

And to run a benchmark on it (replace the number with desired client count)
```
Benchmark.runSingleBenchmark "log-file-suffix" 1 eviEndpoint;;
```

In the end, you have to also stop the docker containers and remove network:
```
Docker.stopAndRemoveContainer eviEndpoint.dockerName
Docker.stopAndRemoveContainer databaseDockerName
removeNetwork benchmarkNetwork
;;
```

To close the interactive session, just write:
```
#quit;;
```