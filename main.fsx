#load "benchmark.fsx"

open Benchmark

// You can use one of the following configurations:
// * defaultBenchmarkConfiguration - the full benchmark
// * quickTestBenchmarkConfiguration - a benchmark that uses only the lowest product and client count from the full benchmark
// * upperBoundaryBenchmarkConfiguration - a benchmark that uses only the highest product and client count from the full benchmark
// Or you can customize them to perform alternative benchmark
quickTestBenchmarkConfiguration
|> performBenchmark
