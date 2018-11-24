using System;
using ExpressionToCodeLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using IncrementalMeanVarianceAccumulator;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
namespace SortAlgoBench
{
    static class SortAlgoBenchProgram
    {
        static void Main()
        {
            const double quality = 200_000_000_000.0;
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            var targetSizes = new[] { 1 << 5, 1 << 7, 1 << 10, 1 << 13, 1 << 16, 1 << 19, 1 << 22 }.Reverse();
            var all = targetSizes.SelectMany(targetSize => BenchSize(targetSize, quality)).ToArray();

            Console.WriteLine();
            foreach (var byType in all.GroupBy(o => o.type))
                Console.WriteLine($"{byType.Key.ToCSharpFriendlyTypeName()}: {byType.Average(o => o.nsPerArrayItem):f1}ns/item");

            Console.WriteLine();
            foreach (var byMethod in all.GroupBy(o => o.method))
                Console.WriteLine($"{byMethod.Key}: {byMethod.Average(o => o.nsPerArrayItem):f1}ns/item");

            Console.WriteLine();
            Console.WriteLine($"OVERALL: {all.Average(o => o.nsPerArrayItem):f1}ns/item");
        }

        static (string method, Type type, double nsPerArrayItem, double nsStdErr)[] BenchSize(int targetSize, double quality)
        {
            var backingArraySize = checked(targetSize * 16);
            var iterations = (int)(6.5 + Math.Pow(quality / Helpers.CostScalingEstimate(targetSize), 0.7));

            var data = Helpers.RandomizeUInt64(backingArraySize);
            Console.WriteLine();

            // ReSharper disable once UnusedParameter.Local - for type inference
            (string method, Type type, double nsPerArrayItem, double nsStdErr)[] BencherFor<TOrder, T>(TOrder order, Func<ulong, T> map, int guesstimatedSizeInBytes)
                where TOrder : struct, IOrdering<T>
            {
                if (guesstimatedSizeInBytes * (long)backingArraySize > uint.MaxValue)
                    return null;
                var beforeMap = GC.GetTotalMemory(true);
                var mappedData = new T[data.Length];
                var afterArray = GC.GetTotalMemory(false);
                for (var i = 0; i < data.Length; i++)
                    mappedData[i] = map(data[i]);
                var afterMap = GC.GetTotalMemory(true);
                GC.KeepAlive(map);
                var estimatedPerObjectCost = (afterMap - beforeMap - 24) / (double)data.Length;
                var estimatedSizeInArray = (afterArray - beforeMap - 24) / (double)data.Length;
                var estimatedSizeInHeap = (afterMap - afterArray) / (double)data.Length;
                Console.WriteLine(
                    $"type {typeof(T).ToCSharpFriendlyTypeName()}: total size {estimatedPerObjectCost:f1} bytes of which value {estimatedSizeInArray:f1} and heap size {estimatedSizeInHeap:f1}");

                Console.WriteLine(
                    $"This implies a working set size of {backingArraySize * estimatedPerObjectCost / 1024.0 / 1024.0:f1}MB, and a per-sort memory usage of on average {targetSize * estimatedPerObjectCost / (1 << 20):f1}MB upto twice that; and merge-sorts will need {targetSize * estimatedSizeInArray / (1 << 20):f1}MB scratch.");

                return new SortAlgorithmBench<T, TOrder>(mappedData, iterations).BenchVariousAlgos().ToArray();
            }

            return new[] {
                BencherFor(default(BigStructOrderingAlgorithms.Order), Helpers.MapToBigStruct, 48),
                BencherFor(default(SampleClassOrderingAlgorithms.Order), Helpers.MapToSampleClass, 32),
                BencherFor(default(SmallStructOrderingAlgorithms.Order), Helpers.MapToSmallStruct, 16),
                BencherFor(default(Int32OrderingAlgorithms.Int32Order), Helpers.MapToInt32, 4),
                BencherFor(default(DoubleOrderingAlgorithms.Order), Helpers.MapToDouble, 8),
                BencherFor(default(UInt64OrderingAlgorithms.UInt64Ordering), Helpers.MapToUInt64, 8),
                BencherFor(default(UInt32OrderingAlgorithms.UInt32Order), Helpers.MapToUInt32, 4),
                BencherFor(default(ComparableOrderingAlgorithms<int>.ComparableOrdering), Helpers.MapToInt32, 4),
            }.Where(a => a != null).SelectMany(r => r).ToArray();
        }
    }

    public sealed class SortAlgorithmBench<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        public IEnumerable<(string method, Type type, double nsPerArrayItem, double nsStdErr)> BenchVariousAlgos()
        {
            var meanLen = SubArrays().Average(o => o.len);
            Console.WriteLine($"Sorting arrays of {typeof(T).ToCSharpFriendlyTypeName()} with {meanLen:f1} elements (average over {Iterations} benchmarked arrays).");
            yield return BenchSort(SystemArraySort);
            yield return BenchSort(DualPivotQuickSort);
            yield return BenchSort(ParallelQuickSort);
            yield return BenchSort(QuickSort);
            yield return BenchSort(BottomUpMergeSort);
            yield return BenchSort(TopDownMergeSort);
            yield return BenchSort(AltTopDownMergeSort);

            Console.WriteLine();
        }

        static void ParallelQuickSort(T[] arr, int len) { OrderedAlgorithms<T, TOrder>.ParallelQuickSort(arr.AsSpan(0, len)); }
        static void AltTopDownMergeSort(T[] arr, int len) { OrderedAlgorithms<T, TOrder>.AltTopDownMergeSort(arr.AsSpan(0, len)); }
        static void TopDownMergeSort(T[] arr, int len) { OrderedAlgorithms<T, TOrder>.TopDownMergeSort(arr.AsSpan(0, len)); }
        static void DualPivotQuickSort(T[] arr, int len) { OrderedAlgorithms<T, TOrder>.DualPivotQuickSort(arr.AsSpan(0, len)); }
        static void BottomUpMergeSort(T[] arr, int len) { OrderedAlgorithms<T, TOrder>.BottomUpMergeSort(arr.AsSpan(0, len)); }
        static void QuickSort(T[] arr, int len) { OrderedAlgorithms<T, TOrder>.QuickSort(arr.AsSpan(0, len)); }
        static void ArraySort_Primitive(T[] arr, int len) { Array.Sort(arr, 0, len); }
        static void ArraySort_OrderComparer(T[] arr, int len) { Array.Sort(arr, 0, len, Helpers.ComparerFor<T, TOrder>()); }
        static readonly Action<T[], int> SystemArraySort = typeof(T).IsPrimitive ? (Action<T[], int>)ArraySort_Primitive : ArraySort_OrderComparer;

        public SortAlgorithmBench(T[] sourceData, int iterations)
        {
            SourceData = sourceData;
            Iterations = iterations;
            workspace = new T[sourceData.Length >> 3];
        }

        readonly T[] workspace;
        readonly T[] SourceData;
        readonly int Iterations;

        IEnumerable<(int offset, int len)> SubArrays()
        {
            var random = new Random(42);
            for (var k = 0; k < Iterations; k++) {
                var len = random.Next(workspace.Length + 1);
                var offset = random.Next(SourceData.Length - len + 1);
                yield return (offset, len);
            }
        }

        void RefreshData((int offset, int len) subsegment) { Array.Copy(SourceData, subsegment.offset, workspace, 0, subsegment.len); }

        public (string method, Type type, double nsPerArrayItem, double nsStdErr) BenchSort(Action<T[], int> action)
        {
            var method = action.Method.Name;
            var sizes = new List<int>();
            var nsPerCost = new List<double>();
            var sw = new Stopwatch();
            var swOverhead = Stopwatch.StartNew();
            double totalActualMilliseconds = 0;
            foreach (var subsegment in SubArrays()) {
                RefreshData(subsegment);
                var len = subsegment.len;
                long checkSum = 0;
                for (var j = 0; j < len; j++) {
                    var l = workspace[j];
                    checkSum = checkSum + l.GetHashCode();
                }

#if false
//ensure not entirely sorted beforehand
                if (len > 50) {
                    var sorted = true;
                    for (var j = 1; j < len; j++)
                        if (!default(TOrder).LessThan(workspace[j], workspace[j - 1]))
                            sorted = false;
                    if (sorted) {
                        Console.WriteLine("Already sorted??");
                        break;
                    }
                }
    #endif

                sw.Restart();
                action(workspace, len);
                sw.Stop();
                var singleRunElapsedMilliseconds = sw.Elapsed.TotalMilliseconds;
                totalActualMilliseconds += singleRunElapsedMilliseconds;
                nsPerCost.Add(singleRunElapsedMilliseconds * 1000_000 / Helpers.CostScalingEstimate(len));
                sizes.Add(len);

                for (var j = 0; j < len; j++) {
                    var l = workspace[j];
                    checkSum = checkSum - l.GetHashCode();
                }

                if (checkSum != 0)
                    Console.WriteLine(method + " has differing elements before and after sort");
                for (var j = 1; j < len; j++)
                    if (default(TOrder).LessThan(workspace[j], workspace[j - 1])) {
                        Console.WriteLine(method + " did not sort.");
                        break;
                    }
            }

            nsPerCost.Sort();
            var meanLen = sizes.Average();
            var msDistrib = MeanVarianceAccumulator.FromSequence(nsPerCost.Skip(1).Take(nsPerCost.Count - 2));
            var rescaleFromNsPerCostToNsPerItem = Helpers.CostScalingEstimate(meanLen) / meanLen;
            var nsPerItem = msDistrib.Mean * rescaleFromNsPerCostToNsPerItem;
            var nsStdErr = Helpers.StdErr(msDistrib) * rescaleFromNsPerCostToNsPerItem;
            var medianNsPerItem = (nsPerCost[nsPerCost.Count >> 1] + nsPerCost[nsPerCost.Count + 1 >> 1]) / 2.0 * rescaleFromNsPerCostToNsPerItem;
            Console.WriteLine(
                $"{method.PadLeft(23)}: mean {Helpers.MSE(nsPerItem, nsStdErr).PadRight(11)} ns/item; median {medianNsPerItem:f1}; overhead: {100 * (1 - totalActualMilliseconds / swOverhead.Elapsed.TotalMilliseconds):f1}%");
            return (action.Method.Name.StartsWith("ArraySort_") ? "ArraySort" : method, typeof(T), nsPerItem, nsStdErr);
        }
    }

    public abstract class ComparableOrderingAlgorithms<T> : OrderedAlgorithms<T, ComparableOrderingAlgorithms<T>.ComparableOrdering>
        where T : IComparable<T>
    {
        public struct ComparableOrdering : IOrdering<T>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(T a, T b) => a.CompareTo(b) < 0;
        }
    }

    public abstract class UInt64OrderingAlgorithms : OrderedAlgorithms<ulong, UInt64OrderingAlgorithms.UInt64Ordering>
    {
        public struct UInt64Ordering : IOrdering<ulong>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(ulong a, ulong b) => a < b;
        }
    }

    public abstract class UInt32OrderingAlgorithms : OrderedAlgorithms<uint, UInt32OrderingAlgorithms.UInt32Order>
    {
        public struct UInt32Order : IOrdering<uint>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(uint a, uint b) => a < b;
        }
    }

    public abstract class Int32OrderingAlgorithms : OrderedAlgorithms<int, Int32OrderingAlgorithms.Int32Order>
    {
        public struct Int32Order : IOrdering<int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(int a, int b) => a < b;
        }
    }

    public abstract class DoubleOrderingAlgorithms : OrderedAlgorithms<double, DoubleOrderingAlgorithms.Order>
    {
        public struct Order : IOrdering<double>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(double a, double b) => a < b || !(a >= b);
        }
    }

    public abstract class BigStructOrderingAlgorithms : OrderedAlgorithms<(int, long, DateTime, string, Guid), BigStructOrderingAlgorithms.Order>
    {
        public struct Order : IOrdering<(int, long, DateTime, string, Guid)>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan((int, long, DateTime, string, Guid) a, (int, long, DateTime, string, Guid) b) => a.Item1 < b.Item1 || a.Item1 == b.Item1 && a.Item2 < b.Item2;
        }
    }

    public abstract class SmallStructOrderingAlgorithms : OrderedAlgorithms<(int, int, int), SmallStructOrderingAlgorithms.Order>
    {
        public struct Order : IOrdering<(int, int, int)>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan((int, int, int) a, (int, int, int) b) => a.Item1 < b.Item1 || a.Item1 == b.Item1 && a.Item2 < b.Item2;
        }
    }

    public class SampleClass : IComparable<SampleClass>
    {
        public int Value;
        public int CompareTo(SampleClass other) => Value.CompareTo(other.Value);
    }

    public abstract class SampleClassOrderingAlgorithms : OrderedAlgorithms<SampleClass, SampleClassOrderingAlgorithms.Order>
    {
        public struct Order : IOrdering<SampleClass>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(SampleClass a, SampleClass b) => a.Value < b.Value;
        }
    }
}
