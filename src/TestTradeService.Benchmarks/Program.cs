using BenchmarkDotNet.Running;

namespace TestTradeService.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
        BenchmarkRunner.Run<DataPipelineThroughputBenchmark>();
    }
}
