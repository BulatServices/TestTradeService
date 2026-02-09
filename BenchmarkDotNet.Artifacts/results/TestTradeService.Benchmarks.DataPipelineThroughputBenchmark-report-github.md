```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.6456/22H2/2022Update)
AMD Ryzen 7 5800X3D, 1 CPU, 16 logical and 8 physical cores
.NET SDK 8.0.414
  [Host]     : .NET 8.0.20 (8.0.2025.41914), X64 RyuJIT AVX2
  Job-XZYTCW : .NET 8.0.20 (8.0.2025.41914), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=6  LaunchCount=1  
WarmupCount=2  

```
| Method            | TickCount | Mean     | Error    | StdDev   | Gen0      | Gen1      | Gen2      | Allocated |
|------------------ |---------- |---------:|---------:|---------:|----------:|----------:|----------:|----------:|
| **ProcessTicksAsync** | **100000**    | **106.2 ms** | **10.37 ms** |  **3.70 ms** | **1666.6667** | **1333.3333** |  **333.3333** |  **90.19 MB** |
| **ProcessTicksAsync** | **500000**    | **606.9 ms** | **99.92 ms** | **35.63 ms** | **9000.0000** | **8000.0000** | **1000.0000** | **444.54 MB** |
