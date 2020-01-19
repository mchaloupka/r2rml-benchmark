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
[ 10; 100; 1000; 10000; 100000; 200000; 500000; 1000000 ]                  
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
    [ 1; 2; 4; 8; 16; 32; 64 ]            
)

generateSummary ()