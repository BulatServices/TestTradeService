# Benchmark: DataPipeline throughput

Запуск:

```powershell
dotnet run -c Release --project src\TestTradeService.Benchmarks\TestTradeService.Benchmarks.csproj
```

Как считать пропускную способность:

1. В таблице BenchmarkDotNet возьмите `Mean` для строки с нужным `TickCount`.
2. Рассчитайте `TickCount / MeanSeconds`.
3. Если запускали несколько `TickCount`, максимум тиков/сек — это наибольшее значение из рассчитанных.
