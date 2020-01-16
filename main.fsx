#load "shell.fsx"
#load "database.fsx"
#load "endpoints.fsx"
#load "benchmark.fsx"

open Shell
open Database
open Endpoints
open Benchmark

createAndEmptyDirectory outputDir

[ 20; ]                  // Product counts
|> List.iter (
  runBenchmark 
    [ MsSql; MySql ]     // Databases to use
    [ eviEndpoint ]      // Endpoints to benchmark
    [ 1; 32 ]            // Client counts
)

// TODO: Generate summary