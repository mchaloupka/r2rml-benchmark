#load "benchmark.fsx"

open Benchmark

let args = 
    fsi.CommandLineArgs 
    |> Array.tail
    |> Array.toList

// You can use one of the following configurations:
// * defaultBenchmarkConfiguration - the full benchmark
// * quickTestBenchmarkConfiguration - a benchmark that uses only the lowest product and highest client count from the full benchmark
// * upperBoundaryBenchmarkConfiguration - a benchmark that uses only the highest product and client count from the full benchmark
// Or you can customize them to perform alternative benchmark
match args with
| [ "quick" ] -> quickTestBenchmarkConfiguration
| [ "upper" ] -> upperBoundaryBenchmarkConfiguration
| [] -> defaultBenchmarkConfiguration
| _ -> failwithf "Unknown arguments: %A" args
|> performBenchmark
