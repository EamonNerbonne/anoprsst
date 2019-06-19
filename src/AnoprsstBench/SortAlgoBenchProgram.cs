using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Anoprsst;
using Anoprsst.BuiltinOrderings;
using Anoprsst.Uncommon;
using ExpressionToCodeLib;
using IncrementalMeanVarianceAccumulator;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
namespace AnoprsstBench
{
    static class SortAlgoBenchProgram
    {
        const double quality = 1_000_000_000_000.0;
        const int maxIters = 200_000;

        static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            var targetSizes = new[] { 1 << 5, 1 << 7, 1 << 10, 1 << 13, 1 << 16, 1 << 19, 1 << 22 /**/ };
            Console.WriteLine("Benchmarking on "
                + Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
                + "; "
                + Unsafe.SizeOf<IntPtr>() * 8
                + "-bit"
                + "; "
                + Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
                + " with quality constant "
                + quality
                + "maxIters: "
                + maxIters);
            Console.WriteLine("With arrays lengths approximating: " + string.Join(", ", targetSizes));
            Console.WriteLine();

            var all = targetSizes.SelectMany(BenchSize).ToArray();

            Console.WriteLine();
            foreach (var byTypeAndMethod in all.GroupBy(o => o.type + " using " + o.method)) {
                Console.WriteLine($"{byTypeAndMethod.Key}: {byTypeAndMethod.Average(o => o.nsPerArrayItem):f1}ns/item");
            }

            Console.WriteLine();
            foreach (var byType in all.GroupBy(o => o.type)) {
                Console.WriteLine($"{byType.Key}: {byType.Average(o => o.nsPerArrayItem):f1}ns/item");
            }

            Console.WriteLine();
            foreach (var byMethod in all.GroupBy(o => o.method)) {
                Console.WriteLine($"{byMethod.Key}: {byMethod.Average(o => o.nsPerArrayItem):f1}ns/item");
            }

            Console.WriteLine();
            Console.WriteLine($"OVERALL: {all.Average(o => o.nsPerArrayItem):f1}ns/item");
        }

        static (string method, string type, double nsPerArrayItem, double nsStdErr)[] BenchSize(int targetSize)
        {
            var backingArraySize = checked(targetSize * 16);
            var iterations = (int)Math.Min(maxIters, 6.5 + Math.Pow(quality / Helpers.CostScalingEstimate(targetSize), 0.7));

            var data = Helpers.RandomizeUInt64(backingArraySize);
            Console.WriteLine();

            // ReSharper disable once UnusedParameter.Local - for type inference
            (string method, string type, double nsPerArrayItem, double nsStdErr)[] BencherFor<TOrder, T>(TOrder order, Func<ulong, T> map)
                where TOrder : struct, IOrdering<T>
            {
                if (Unsafe.SizeOf<T>() * (long)backingArraySize > uint.MaxValue) {
                    return null;
                }

                var beforeMap = GC.GetTotalMemory(true);
                var mappedData = new T[data.Length];
                var afterArray = GC.GetTotalMemory(true);
                for (var i = 0; i < mappedData.Length; i++) {
                    mappedData[i] = map(data[i]);
                }

                var afterMap = GC.GetTotalMemory(true);
                GC.KeepAlive(map); //so mem mesurement on previous line isn't disturbed by freeing this object.

                var maximumTargetLength = mappedData.Length >> 3;
                var estimatedPerObjectCost = (afterMap - beforeMap - 24) / (double)mappedData.Length;
                var estimatedSizeInArray = (afterArray - beforeMap - 24) / (double)mappedData.Length;
                var estimatedSizeInHeap = (afterMap - afterArray) / (double)mappedData.Length;

                var slices = SubArrays(mappedData, maximumTargetLength, iterations);
                // ReSharper disable once PossibleMultipleEnumeration
                var meanLen = slices.Average(o => o.Length);
                var typeName = typeof(T).ToCSharpFriendlyTypeName();
                var orderName = typeof(TOrder).ToCSharpFriendlyTypeName();
                var algorithmThesholds = AlgorithmChoiceThresholds<T>.Defaults;
                Console.WriteLine($"Sorting arrays of {typeName} with {meanLen:f1} elements by {orderName} (average over {iterations} benchmarked arrays).");
                Console.WriteLine($"Insertion sorts below {algorithmThesholds.TopDownInsertionSortBatchSize};"
                    + $" faster quicksort median below {algorithmThesholds.QuickSortFastMedianThreshold};"
                    + $" no parallelism below {algorithmThesholds.MinimalParallelQuickSortBatchSize} items.");

                Console.WriteLine($"type {typeName}: total size {estimatedPerObjectCost:f1} bytes of which value {estimatedSizeInArray:f1} and heap size {estimatedSizeInHeap:f1}.  ");

                var perSortMB = targetSize * estimatedPerObjectCost / (1 << 20);
                var benchmarkMB = mappedData.Length * estimatedPerObjectCost / (1 << 20);
                var mergeSortScratchMB = targetSize * estimatedSizeInArray / (1 << 20);
                Console.WriteLine($"Per-sort working set on average {perSortMB:f1}MB (max upto twice that), overall benchmark ~{benchmarkMB:f1}MB; mergesort scratch needs an extra {mergeSortScratchMB:f1}MB.");

                // ReSharper disable once PossibleMultipleEnumeration
                return new SortAlgorithmBench<T, TOrder>(slices, maximumTargetLength, order).BenchVariousAlgos().ToArray();
            }

            return new[] {
                    BencherFor(default(Int32Ordering), Helpers.MapToInt32),
                    BencherFor(default(ComparableOrdering<int>), Helpers.MapToInt32),
                    BencherFor(default(UInt32Ordering), Helpers.MapToUInt32),
                    BencherFor(default(ComparableOrdering<uint>), Helpers.MapToUInt32),
                    BencherFor(default(UInt64Ordering), Helpers.MapToUInt64),
                    BencherFor(default(ComparableOrdering<ulong>), Helpers.MapToUInt64),
                    BencherFor(default(DoubleOrdering), Helpers.MapToDouble),
                    BencherFor(default(ComparableOrdering<double>), Helpers.MapToDouble),
                    BencherFor(default(FloatOrdering), Helpers.MapToFloat),
                    BencherFor(default(ComparableOrdering<float>), Helpers.MapToFloat),
                    BencherFor(default(SmallTupleOrder), Helpers.MapToSmallStruct),
                    BencherFor(default(SampleClassOrder), Helpers.MapToSampleClass),
                    BencherFor(default(BigTupleOrder), Helpers.MapToBigStruct),
                    BencherFor(default(StringOrder), Helpers.MapToString),
                }.Where(a => a != null)
                .SelectMany(r => r)
                .ToArray();
        }

        public static IEnumerable<Memory<T>> SubArrays<T>(T[] sourceData, int maximumTargetLength, int iterations)
        {
            var random = new Random(42);
            for (var k = 0; k < iterations; k++) {
                var len = random.Next(maximumTargetLength + 1);
                var offset = random.Next(sourceData.Length - len + 1);
                yield return sourceData.AsMemory(offset, len);
            }
        }
    }

    public sealed class SortAlgorithmBench<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        public IEnumerable<(string method, string type, double nsPerArrayItem, double nsStdErr)> BenchVariousAlgos()
        {
            if (typeof(TOrder) == typeof(Int32Ordering)) {
                yield return BenchSort((Action<T[], int>)(Action<int[], int>)SafeFromSO);
                yield return BenchSort((Action<T[], int>)(Action<int[], int>)UnsafeFromSO);
            }

            yield return BenchSort(SystemArraySort);
            yield return BenchSort(ParallelQuickSort);
            yield return BenchSort(QuickSort);
            yield return BenchSort(TopDownMergeSort);
            yield return BenchSort(BottomUpMergeSort);
            yield return BenchSort(DualPivotQuickSort);
            yield return BenchSort(AltTopDownMergeSort);

            Console.WriteLine();
        }

        void SafeFromSO(int[] arr, int len)
            => FromStackOverflow3719719.QuickSort(arr, 0, len);

        void UnsafeFromSO(int[] arr, int len)
            => FromStackOverflow3719719.UnsafeQuickSort(arr, 0, len);

        void ParallelQuickSort(T[] arr, int len)
            => arr.AsSpan(0, len).WithOrder(order).ParallelQuickSort();

        void AltTopDownMergeSort(T[] arr, int len)
            => arr.AsSpan(0, len).WithOrder(order).AltTopDownMergeSort();

        void TopDownMergeSort(T[] arr, int len)
            => arr.AsSpan(0, len).WithOrder(order).MergeSort();

        void DualPivotQuickSort(T[] arr, int len)
            => arr.AsSpan(0, len).WithOrder(order).DualPivotQuickSort();

        void BottomUpMergeSort(T[] arr, int len)
            => arr.AsSpan(0, len).WithOrder(order).BottomUpMergeSort();

        void QuickSort(T[] arr, int len)
            => arr.AsSpan(0, len).WithOrder(order).QuickSort();

        static void PrimitiveArraySort(T[] arr, int len)
            => Array.Sort(arr, 0, len);

        void IComparableArraySort(T[] arr, int len)
            => Array.Sort(arr, 0, len, new OrderComparer<T, TOrder>(order));

        static void StringArraySort(string[] arr, int len)
            => Array.Sort(arr, 0, len, StringComparer.Ordinal);

        Action<T[], int> SystemArraySort
            => typeof(T) == typeof(string) && typeof(TOrder) == typeof(StringOrder) ? (Action<T[], int>)(Action<string[], int>)StringArraySort
                : typeof(T).IsPrimitive && typeof(TOrder).Namespace == typeof(Int32Ordering).Namespace && !typeof(TOrder).IsGenericType ? PrimitiveArraySort
                : (Action<T[], int>)IComparableArraySort;

        public SortAlgorithmBench(IEnumerable<Memory<T>> slices, int maximumTargetLength, TOrder order)
        {
            workspace = new T[maximumTargetLength];
            Slices = slices;
            this.order = order;
        }

        readonly T[] workspace;
        readonly IEnumerable<Memory<T>> Slices;
        readonly TOrder order;

        public (string method, string type, double nsPerArrayItem, double nsStdErr) BenchSort(Action<T[], int> action)
        {
            var method = action.Method.Name;
            var sizes = new List<int>();
            var nsPerCost = new List<double>();
            var sw = new Stopwatch();
            var swOverhead = Stopwatch.StartNew();
            double totalActualMilliseconds = 0;

            foreach (var subsegment in Slices) {
                subsegment.Span.CopyTo(workspace);
                var len = subsegment.Length;
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
                        if (!order.LessThan(workspace[j], workspace[j - 1]))
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

                if (checkSum != 0) {
                    Console.WriteLine(method + " has differing elements before and after sort");
                }

                for (var j = 1; j < len; j++) {
                    if (order.LessThan(workspace[j], workspace[j - 1])) {
                        Console.WriteLine(method + " did not sort.");
                        break;
                    }
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
            return (action.Method.Name.StartsWith("ArraySort_", StringComparison.Ordinal) ? "ArraySort" : method, typeof(T).ToCSharpFriendlyTypeName() + "/" + typeof(TOrder).ToCSharpFriendlyTypeName(), nsPerItem, nsStdErr);
        }
    }

    public struct BigTupleOrder : IOrdering<(int, long, DateTime, string, Guid)>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan((int, long, DateTime, string, Guid) a, (int, long, DateTime, string, Guid) b)
            => a.Item1 < b.Item1 || a.Item1 == b.Item1 && a.Item2 < b.Item2;
    }

    public struct SmallTupleOrder : IOrdering<(int, int, int)>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan((int, int, int) a, (int, int, int) b)
            => a.Item1 < b.Item1 || a.Item1 == b.Item1 && a.Item2 < b.Item2;
    }

    public sealed class SampleClass : IComparable<SampleClass>
    {
        public int Value;

        public int CompareTo(SampleClass other)
            => Value.CompareTo(other.Value);
    }

    public struct SampleClassOrder : IOrdering<SampleClass>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(SampleClass a, SampleClass b)
            => a.Value < b.Value;
    }

    public struct StringOrder : IOrdering<string>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(string a, string b)
            => StringComparer.Ordinal.Compare(a, b) < 0;
    }
}
