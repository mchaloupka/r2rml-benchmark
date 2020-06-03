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

The next commands depends on an available folder structure, the following command creates or empties needed folders.
```
Benchmark.createAndClearWorkingDirectories ();;
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

And to run a benchmark on it, the arguments are:
* How many runs will be executed
* Suffix for the log and output files
* How many parallel clients will be used
* Which endpoint will be benchmarked
* A flag whether the full log should be stored
```
Benchmark.runSingleBenchmark 128 "log-file-suffix" 1 eviEndpoint false;;
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