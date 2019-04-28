``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.17763.437 (1809/October2018Update/Redstone5)
Intel Core i7-4770K CPU 3.50GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.0.100-preview4-011223
  [Host] : .NET Core 3.0.0-preview4-27615-11 (CoreCLR 4.6.27615.73, CoreFX 4.700.19.21213), 64bit RyuJIT
  Core   : .NET Core 3.0.0-preview4-27615-11 (CoreCLR 4.6.27615.73, CoreFX 4.700.19.21213), 64bit RyuJIT

Job=Core  Runtime=Core  InvocationCount=1  
UnrollFactor=1  

```
|                      Method |  RunDefinition |    Mean |    Error |   StdDev | Rank |
|---------------------------- |--------------- |--------:|---------:|---------:|-----:|
|               Anoprsst_Sort | (1, 100000000) | 1.464 s | 0.0041 s | 0.0039 s |    1 |
|          Anoprsst_QuickSort | (1, 100000000) | 7.049 s | 0.0193 s | 0.0180 s |    2 |
|          Anoprsst_MergeSort | (1, 100000000) | 8.514 s | 0.0345 s | 0.0305 s |    5 |
| Anoprsst_DualPivotQuickSort | (1, 100000000) | 7.236 s | 0.0421 s | 0.0394 s |    3 |
|  Anoprsst_BottomUpMergeSort | (1, 100000000) | 8.614 s | 0.0820 s | 0.0767 s |    5 |
|             SystemArraySort | (1, 100000000) | 7.673 s | 0.0198 s | 0.0155 s |    4 |
|          StackOverflow_Safe | (1, 100000000) | 9.131 s | 0.0125 s | 0.0117 s |    6 |
|        StackOverflow_Unsafe | (1, 100000000) | 8.492 s | 0.0150 s | 0.0140 s |    5 |
