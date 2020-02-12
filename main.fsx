#load "benchmark.fsx"

open Benchmark

// Replace minimalBenchmarkConfiguration by defaultBenchmarkConfiguration to perform
// the complete benchmark or customize the configuration to perform alternative benchmark
minimalBenchmarkConfiguration
|> performBenchmark
