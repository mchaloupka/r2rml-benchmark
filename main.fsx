#load "shell.fsx"
#load "database.fsx"
#load "endpoints.fsx"
#load "benchmark.fsx"

open Shell
open Database
open Endpoints
open Benchmark

createAndEmptyDirectory outputDir

// Product counts
[ 10; 100; ]                  
|> List.iter (
  runBenchmark 
    // Databases to use
    [ 
      MsSql;
      MySql
    ]     
    // Endpoints to benchmark
    [
      eviEndpoint;
      ontopEndpoint
    ]      
    // Client counts
    [ 32; 64 ]            
)

generateSummary ()