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
[ 20; ]                  
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
    [ 1; 32 ]            
)

generateSummary ()