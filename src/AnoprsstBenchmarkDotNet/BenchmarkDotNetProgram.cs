using System;
using Anoprsst;
using Anoprsst.Uncommon;
using AnoprsstBench;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace AnoprsstBenchmarkDotNet
{
    //[ClrJob]
    [CoreJob]
    [RankColumn]
    //[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions, HardwareCounter.LlcMisses)]
    //[MemoryDiagnoser]
    public class SortingBenchmarks
    {
        int[][] origdata;
        int[][] runs;

        [ParamsSource(nameof(RunDefinitions))] public (int count, int length) RunDefinition;

        public static (int count, int length)[] RunDefinitions => new[] {
            //(1_000_000, 100), (32_000, 3_200), (1000, 100_000), (32, 3_200_000), 
            (1, 100_000_000),
        };

        [GlobalSetup]
        public void Setup()
        {
            runs = new int[RunDefinition.count][];
            origdata = new int[RunDefinition.count][];
            for (var runI = 0; runI < runs.Length; runI++) {
                origdata[runI] = new int[RunDefinition.length];
                runs[runI] = new int[RunDefinition.length];
            }

            var r = new Random(42);
            foreach (var data in origdata)
                for (var i = 0; i < data.Length; i++)
                    data[i] = r.Next();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            for (var j = 0; j < origdata.Length; j++) {
                origdata[j].CopyTo(runs[j], 0);
            }
        }

        [Benchmark]
        public void Anoprsst_Sort()
        {
            foreach (var data in runs)
                data.AsSpan().WithIComparableOrder().Sort();
        }

        [Benchmark]
        public void Anoprsst_QuickSort()
        {
            foreach (var data in runs)
                data.AsSpan().WithIComparableOrder().QuickSort();
        }

        [Benchmark]
        public void Anoprsst_MergeSort()
        {
            foreach (var data in runs)
                data.AsSpan().WithIComparableOrder().MergeSort();
        }

        [Benchmark]
        public void Anoprsst_DualPivotQuickSort()
        {
            foreach (var data in runs)
                data.AsSpan().WithIComparableOrder().DualPivotQuickSort();
        }

        [Benchmark]
        public void Anoprsst_BottomUpMergeSort()
        {
            foreach (var data in runs)
                data.AsSpan().WithIComparableOrder().BottomUpMergeSort();
        }

        [Benchmark]
        public void SystemArraySort()
        {
            foreach (var data in runs)
                Array.Sort(data);
        }

        [Benchmark]
        public void StackOverflow_Safe()
        {
            foreach (var data in runs)
                FromStackOverflow3719719.QuickSort(data, 0, data.Length);
        }

        [Benchmark]
        public void StackOverflow_Unsafe()
        {
            foreach (var data in runs)
                FromStackOverflow3719719.UnsafeQuickSort(data, 0, data.Length);
        }
    }

    public static class BenchmarkDotNetProgram
    {
        public static void Main() => BenchmarkRunner.Run<SortingBenchmarks>();
    }
}
