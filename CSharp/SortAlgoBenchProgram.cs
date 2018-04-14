using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using IncrementalMeanVarianceAccumulator;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
namespace SortAlgoBench
{
    static class SortAlgoBenchProgram
    {
        public const int MaxArraySize = 1 << 15 << 3;
        public const int TimingTrials = 30;
        public const int IterationsPerTrial = 20;
        public static readonly int ParallelSplitScale = Helpers.ProcScale();

        static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            Int32OrderingAlgorithms.BencherFor(Helpers.RandomizeInt32()).BenchVariousAlgos();
            UInt64OrderingAlgorithms.BencherFor(Helpers.RandomizeUInt64()).BenchVariousAlgos();
            SampleClassOrderingAlgorithms.BencherFor(Helpers.RandomizeSampleClass()).BenchVariousAlgos();
            PairOrderingAlgorithms.BencherFor(Helpers.RandomizePairs()).BenchVariousAlgos();
            ComparableOrderingAlgorithms<int>.BencherFor(Helpers.RandomizeInt32()).BenchVariousAlgos();
            ComparableOrderingAlgorithms<SampleClass>.BencherFor(Helpers.RandomizeSampleClass()).BenchVariousAlgos();
            //UInt32OrderingAlgorithms.BencherFor(Helpers.RandomizeUInt32()).BenchVariousAlgos();
        }
    }

    sealed class SortAlgorithmBench<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        public void BenchVariousAlgos()
        {
            Console.WriteLine("Benchmarking array of " + typeof(T).Name + " with ordering " + typeof(TOrder).FullName + " (where relevant)");
            BenchSort(SystemArraySort);
            BenchSort(OrderedAlgorithms<T, TOrder>.QuickSort);
            BenchSort(OrderedAlgorithms<T, TOrder>.BottomUpMergeSort);
            BenchSort(OrderedAlgorithms<T, TOrder>.BottomUpMergeSort2);
            BenchSort(OrderedAlgorithms<T, TOrder>.TopDownMergeSort);
            BenchSort(OrderedAlgorithms<T, TOrder>.AltTopDownMergeSort);
            BenchSort(OrderedAlgorithms<T, TOrder>.ParallelDualPivotQuickSort);
            BenchSort(OrderedAlgorithms<T, TOrder>.ParallelTopDownMergeSort);
            BenchSort(OrderedAlgorithms<T, TOrder>.ParallelQuickSort);
            Console.WriteLine();
        }

        static void SystemArraySort(T[] arr, int len) { Array.Sort(arr, 0, len); }

        public SortAlgorithmBench(T[] sourceData)
        {
            this.sourceData = sourceData;
            workspace = new T[sourceData.Length >> 3];
        }

        readonly T[] workspace;
        readonly T[] sourceData;

        int RefreshData(Random random)
        {
            var len = random.Next(workspace.Length + 1);
            var offset = random.Next(sourceData.Length - len + 1);
            Array.Copy(sourceData, offset, workspace, 0, len);
            return len;
        }

        public void BenchSort(Action<T[], int> action)
        {
            var txt = action.Method.Name + "|" + typeof(T).Name;
            Validate(action, txt); //also a warmup
            var sizes = new List<int>();
            var milliseconds = new List<double>();
            for (var i = 0; i < SortAlgoBenchProgram.TimingTrials; i++) {
                var random = new Random(42);
                var sw = new Stopwatch();
                for (var k = 0; k < SortAlgoBenchProgram.IterationsPerTrial; k++) {
                    var len = RefreshData(random);
                    sw.Start();
                    action(workspace, len);
                    sw.Stop();
                    if (i == 0)
                        sizes.Add(len);
                }

                milliseconds.Add(sw.Elapsed.TotalMilliseconds);
            }

            milliseconds.Sort();

            var msDistrib = MeanVarianceAccumulator.FromSequence(milliseconds.Take(milliseconds.Count >> 1));
            var meanLen = sizes.Average();
            Console.WriteLine($"{txt}: {Helpers.MSE(msDistrib)} (ms) for {sizes.Count} arrays of on average {meanLen:f1} items");
        }

        public void Validate(Action<T[], int> action, string txt)
        {
            var random = new Random(42);
            var sw = new Stopwatch();
            for (var k = 0; k < 10; k++) {
                var len = RefreshData(random);
                long checkSum = 0;
                for (var j = 0; j < len; j++) {
                    var l = workspace[j];
                    checkSum = checkSum + l.GetHashCode();
                }

                if (len > 50) {
                    var sorted = true;
                    for (var j = 1; j < len; j++)
                        if (!default(TOrder).LessThan(workspace[j], workspace[j - 1]))
                            sorted = false;
                    if (sorted)
                        Console.WriteLine("Already sorted??");
                    break;
                }

                sw.Start();
                action(workspace, len);
                sw.Stop();
                for (var j = 0; j < len; j++) {
                    var l = workspace[j];
                    checkSum = checkSum - l.GetHashCode();
                }

                if (checkSum != 0)
                    Console.WriteLine(txt + " has differing elements before and after sort");
                for (var j = 1; j < len; j++)
                    if (default(TOrder).LessThan(workspace[j], workspace[j - 1])) {
                        Console.WriteLine(txt + " did not sort.");
                        break;
                    }
            }
        }
    }

    abstract class ComparableOrderingAlgorithms<T> : OrderedAlgorithms<T, ComparableOrderingAlgorithms<T>.ComparableOrdering>
        where T : IComparable<T>
    {
        public struct ComparableOrdering : IOrdering<T>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(T a, T b) => a.CompareTo(b) < 0;
        }
    }

    abstract class UInt64OrderingAlgorithms : OrderedAlgorithms<ulong, UInt64OrderingAlgorithms.UInt64Ordering>
    {
        public struct UInt64Ordering : IOrdering<ulong>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(ulong a, ulong b) => a < b;
        }
    }

    abstract class UInt32OrderingAlgorithms : OrderedAlgorithms<uint, UInt32OrderingAlgorithms.UInt32Order>
    {
        public struct UInt32Order : IOrdering<uint>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(uint a, uint b) => a < b;
        }
    }

    abstract class Int32OrderingAlgorithms : OrderedAlgorithms<int, Int32OrderingAlgorithms.Int32Order>
    {
        public struct Int32Order : IOrdering<int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(int a, int b) => a < b;
        }
    }

    abstract class PairOrderingAlgorithms : OrderedAlgorithms<(int, int), PairOrderingAlgorithms.PairOrder>
    {
        public struct PairOrder : IOrdering<(int, int)>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan((int, int) a, (int, int) b) => a.Item1 < b.Item1 || a.Item1 == b.Item1 && a.Item2 < b.Item2;
        }
    }

    class SampleClass : IComparable<SampleClass>
    {
        public int Value;
        public int CompareTo(SampleClass other) => Value.CompareTo(other.Value);
    }

    abstract class SampleClassOrderingAlgorithms : OrderedAlgorithms<SampleClass, SampleClassOrderingAlgorithms.Order>
    {
        public struct Order : IOrdering<SampleClass>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LessThan(SampleClass a, SampleClass b) => a.Value < b.Value;
        }
    }

    public interface IOrdering<in T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool LessThan(T a, T b);
    }

    abstract class OrderedAlgorithms<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        public static SortAlgorithmBench<T, TOrder> BencherFor(T[] arr) => new SortAlgorithmBench<T, TOrder>(arr);
        protected OrderedAlgorithms() => throw new NotSupportedException("allow subclassing so you can fix type parameters, but not instantiation.");

        [ThreadStatic]
        static T[] Accumulator;

        static T[] GetCachedAccumulator(int maxSize)
        {
            var outputValues = Accumulator ?? (Accumulator = new T[16]);
            if (outputValues.Length < maxSize) {
                var newSize = (int)Math.Max(maxSize, outputValues.Length * 3L / 2L);
                Accumulator = outputValues = new T[newSize];
            }

            return outputValues;
        }

        public static void TopDownMergeSort(T[] array, int endIdx)
            => TopDownSplitMerge_toItems(array, 0, endIdx, GetCachedAccumulator(endIdx));

        public static void ParallelTopDownMergeSort(T[] array, int endIdx)
            => TopDownSplitMerge_toItems_Par(array, 0, endIdx, GetCachedAccumulator(endIdx));

        public static T[] TopDownMergeSort_Copy(T[] array, int endIdx)
            => CopyingTopDownMergeSort(array, new T[endIdx], endIdx);

        public static void AltTopDownMergeSort(T[] array, int endIdx)
            => AltTopDownMergeSort(array, GetCachedAccumulator(endIdx), endIdx);

        public static void BottomUpMergeSort(T[] array, int endIdx)
            => BottomUpMergeSort(array, GetCachedAccumulator(endIdx), endIdx);

        public static void BottomUpMergeSort2(T[] array, int endIdx)
            => BottomUpMergeSort2(array, GetCachedAccumulator(endIdx), endIdx);

        public static void QuickSort(T[] array) => QuickSort_Inclusive(array, 0, array.Length - 1);

        public static void QuickSort(T[] array, int endIdx) => //*
            QuickSort_Inclusive_Unsafe(ref array[0], 0, endIdx - 1); /*/
            QuickSort_Inclusive_Small(array, 0, endIdx - 1);/**/

        public static void QuickSort(T[] array, int firstIdx, int endIdx) { QuickSort_Inclusive(array, firstIdx, endIdx - 1); }
        public static void ParallelQuickSort(T[] array) => QuickSort_Inclusive_Parallel(array, 0, array.Length);
        public static void ParallelQuickSort(T[] array, int endIdx) => QuickSort_Inclusive_Parallel(array, 0, endIdx - 1);
        public static void ParallelQuickSort(T[] array, int firstIdx, int endIdx) { QuickSort_Inclusive_Parallel(array, firstIdx, endIdx - 1); }
        public static void ParallelDualPivotQuickSort(T[] array) => DualPivotQuickSort_Inclusive(array, 0, array.Length - 1);
        public static void ParallelDualPivotQuickSort(T[] array, int endIdx) => DualPivotQuickSort_Inclusive(array, 0, endIdx - 1);
        public static void ParallelDualPivotQuickSort(T[] array, int firstIdx, int endIdx) => DualPivotQuickSort_Inclusive(array, firstIdx, endIdx - 1);

        static void QuickSort_Inclusive_Parallel(T[] array, int firstIdx, int lastIdx)
        {
            var countdownEvent = new CountdownEvent(1);
            QuickSort_Inclusive_ParallelArgs.Impl(
                new QuickSort_Inclusive_ParallelArgs {
                    array = array,
                    firstIdx = firstIdx,
                    lastIdx = lastIdx,
                    countdownEvent = countdownEvent,
                    splitAt = Math.Max(lastIdx - firstIdx >> SortAlgoBenchProgram.ParallelSplitScale, TopDownInsertionSortBatchSize << 1)
                });
            countdownEvent.Wait();
        }

        class QuickSort_Inclusive_ParallelArgs
        {
            public T[] array;
            public int firstIdx;
            public int lastIdx;
            public int splitAt;
            public CountdownEvent countdownEvent;
            static readonly WaitCallback QuickSort_Inclusive_Par2_callback = o => Impl((QuickSort_Inclusive_ParallelArgs)o);

            public static void Impl(QuickSort_Inclusive_ParallelArgs args)
            {
                var firstIdx = args.firstIdx;
                var lastIdx = args.lastIdx;
                var countdownEvent = args.countdownEvent;
                while (lastIdx - firstIdx >= args.splitAt) {
                    var pivot = PartitionMedian5_Unsafe(ref args.array[0], firstIdx, lastIdx);
                    countdownEvent.AddCount(1);
                    ThreadPool.UnsafeQueueUserWorkItem(
                        QuickSort_Inclusive_Par2_callback,
                        new QuickSort_Inclusive_ParallelArgs {
                            array = args.array,
                            firstIdx = pivot + 1,
                            lastIdx = lastIdx,
                            countdownEvent = countdownEvent,
                            splitAt = args.splitAt
                        });
                    lastIdx = pivot; //QuickSort_Inclusive(array, firstIdx, pivot);
                }

                QuickSort_Inclusive_Small_Unsafe(ref args.array[0], firstIdx, lastIdx);
                countdownEvent.Signal();
            }
        }

        static void QuickSort_Inclusive(T[] array, int firstIdx, int lastIdx)
        {
            while (true)
                if (lastIdx - firstIdx < TopDownInsertionSortBatchSize << 9) {
                    QuickSort_Inclusive_Small(array, firstIdx, lastIdx);
                    return;
                } else {
                    var pivot = PartitionMedian5(array, firstIdx, lastIdx);
                    QuickSort_Inclusive(array, pivot + 1, lastIdx);
                    lastIdx = pivot; //QuickSort(array, firstIdx, pivot);
                }
        }

        static void QuickSort_Inclusive_Small(T[] array, int firstIdx, int lastIdx)
        {
            while (true)
                if (lastIdx - firstIdx < TopDownInsertionSortBatchSize) {
                    //InsertionSort_InPlace_Unsafe(ref array[0], firstIdx, lastIdx + 1);
                    InsertionSort_InPlace(array, firstIdx, lastIdx + 1);
                    return;
                } else {
                    var pivot = Partition(array, firstIdx, lastIdx);
                    QuickSort_Inclusive_Small(array, pivot + 1, lastIdx);
                    lastIdx = pivot; //QuickSort(array, firstIdx, pivot);
                }
        }

        //*
        static void QuickSort_Inclusive_Unsafe(ref T ptr, int firstIdx, int lastIdx)
        {
            while (true)
                if (lastIdx - firstIdx < TopDownInsertionSortBatchSize << 9) {
                    QuickSort_Inclusive_Small_Unsafe(ref ptr, firstIdx, lastIdx);
                    return;
                } else {
                    var pivot = PartitionMedian5_Unsafe(ref ptr, firstIdx, lastIdx);
                    QuickSort_Inclusive_Unsafe(ref ptr, pivot + 1, lastIdx);
                    lastIdx = pivot; //QuickSort(array, firstIdx, pivot);
                }
        }

        static void QuickSort_Inclusive_Small_Unsafe(ref T ptr, int firstIdx, int lastIdx)
        {
            while (true)
                if (lastIdx - firstIdx < TopDownInsertionSortBatchSize) {
                    InsertionSort_InPlace_Unsafe(ref ptr, firstIdx, lastIdx + 1);
                    return;
                } else {
                    var pivot = Partition_Unsafe(ref ptr, firstIdx, lastIdx);
                    QuickSort_Inclusive_Small_Unsafe(ref ptr, pivot + 1, lastIdx);
                    lastIdx = pivot; //QuickSort(array, firstIdx, pivot);
                }
        }
        /*/
        static void QuickSort_Inclusive_Unsafe(ref T ptr, int firstIdx, int lastIdx)
        {
            while (lastIdx - firstIdx >= TopDownInsertionSortBatchSize << 9) {
                var pivot = PartitionMedian5_Unsafe(ref ptr, firstIdx, lastIdx);
                QuickSort_Inclusive_Unsafe(ref ptr, pivot + 1, lastIdx);
                lastIdx = pivot; //QuickSort(array, firstIdx, pivot);
            }

            QuickSort_Inclusive_Small_Unsafe(ref ptr, firstIdx, lastIdx);
        }

        static void QuickSort_Inclusive_Small_Unsafe(ref T ptr, int firstIdx, int lastIdx)
        {
            while (lastIdx - firstIdx >= TopDownInsertionSortBatchSize) {
                var pivot = Partition_Unsafe(ref ptr, firstIdx, lastIdx);
                QuickSort_Inclusive_Small_Unsafe(ref ptr, pivot + 1, lastIdx);
                lastIdx = pivot; //QuickSort(array, firstIdx, pivot);
            }

            InsertionSort_InPlace_Unsafe(ref ptr, firstIdx, lastIdx + 1);
        }

        /**/

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Partition_Unsafe(ref T ptr, int firstIdx, int lastIdx)
        {
            var midpoint = (int)(((uint)firstIdx + (uint)lastIdx) >> 1);
            //if (default(TOrder).LessThan(Unsafe.Add(ref ptr, midpoint), Unsafe.Add(ref ptr, firstIdx)))
            //    (Unsafe.Add(ref ptr, midpoint), Unsafe.Add(ref ptr, firstIdx)) = (Unsafe.Add(ref ptr, firstIdx), Unsafe.Add(ref ptr, midpoint));
            //if (default(TOrder).LessThan(Unsafe.Add(ref ptr, lastIdx), Unsafe.Add(ref ptr, firstIdx)))
            //    (Unsafe.Add(ref ptr, lastIdx), Unsafe.Add(ref ptr, firstIdx)) = (Unsafe.Add(ref ptr, firstIdx), Unsafe.Add(ref ptr, lastIdx));
            //if (default(TOrder).LessThan(Unsafe.Add(ref ptr, lastIdx), Unsafe.Add(ref ptr, midpoint)))
            //    (Unsafe.Add(ref ptr, lastIdx), Unsafe.Add(ref ptr, midpoint)) = (Unsafe.Add(ref ptr, midpoint), Unsafe.Add(ref ptr, lastIdx));
            //firstIdx++;
            //lastIdx--;
            var pivotValue = Unsafe.Add(ref ptr, midpoint);
            while (true) {
                while (default(TOrder).LessThan(Unsafe.Add(ref ptr, firstIdx), pivotValue))
                    firstIdx++;
                while (default(TOrder).LessThan(pivotValue, Unsafe.Add(ref ptr, lastIdx)))
                    lastIdx--;
                if (lastIdx <= firstIdx)
                    return lastIdx;
                (Unsafe.Add(ref ptr, firstIdx), Unsafe.Add(ref ptr, lastIdx)) = (Unsafe.Add(ref ptr, lastIdx), Unsafe.Add(ref ptr, firstIdx));
                firstIdx++;
                lastIdx--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Partition(T[] array, int firstIdx, int lastIdx)
        {
            var midpoint = (int)(((uint)firstIdx + (uint)lastIdx) >> 1);
            var pivotValue = array[midpoint];
            while (true) {
                while (default(TOrder).LessThan(array[firstIdx], pivotValue))
                    firstIdx++;
                while (default(TOrder).LessThan(pivotValue, array[lastIdx]))
                    lastIdx--;
                if (lastIdx <= firstIdx)
                    return lastIdx;
                array.Swap(firstIdx, lastIdx);
                firstIdx++;
                lastIdx--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int PartitionMedian5(T[] array, int firstIdx, int lastIdx)
        {
            var midpoint = (int)(((uint)firstIdx + (uint)lastIdx) >> 1);
            ref var c = ref array[firstIdx + midpoint];
            SortFiveIndexes(ref array[firstIdx], ref array[firstIdx + 1], ref c, ref array[lastIdx - 1], ref array[lastIdx]);

            var pivotValue = c;
            firstIdx += 2;
            lastIdx -= 2;
            while (true) {
                while (default(TOrder).LessThan(array[firstIdx], pivotValue))
                    firstIdx++;
                while (default(TOrder).LessThan(pivotValue, array[lastIdx]))
                    lastIdx--;
                if (lastIdx <= firstIdx)
                    return lastIdx;
                array.Swap(firstIdx, lastIdx);
                firstIdx++;
                lastIdx--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int PartitionMedian5_Unsafe(ref T ptr, int firstIdx, int lastIdx)
        {
            var midpoint = (int)(((uint)firstIdx + (uint)lastIdx) >> 1);
            ref var c = ref Unsafe.Add(ref ptr, firstIdx + midpoint);
            SortFiveIndexes(
                ref Unsafe.Add(ref ptr, firstIdx),
                ref Unsafe.Add(ref ptr, firstIdx + 1),
                ref c,
                ref Unsafe.Add(ref ptr, lastIdx - 1),
                ref Unsafe.Add(ref ptr, lastIdx));

            firstIdx += 2;
            lastIdx -= 2;
            var pivotValue = c;
            while (true) {
                while (default(TOrder).LessThan(Unsafe.Add(ref ptr, firstIdx), pivotValue))
                    firstIdx++;
                while (default(TOrder).LessThan(pivotValue, Unsafe.Add(ref ptr, lastIdx)))
                    lastIdx--;
                if (lastIdx <= firstIdx)
                    return lastIdx;
                (Unsafe.Add(ref ptr, firstIdx), Unsafe.Add(ref ptr, lastIdx)) = (Unsafe.Add(ref ptr, lastIdx), Unsafe.Add(ref ptr, firstIdx));
                firstIdx++;
                lastIdx--;
            }
        }

        static void SortFiveIndexes(ref T a, ref T b, ref T c, ref T d, ref T e)
        {
            if (default(TOrder).LessThan(e, a)) (e, a) = (a, e);
            if (default(TOrder).LessThan(d, b)) (d, b) = (b, d);
            if (default(TOrder).LessThan(c, a)) (c, a) = (a, c);
            if (default(TOrder).LessThan(e, c)) (e, c) = (c, e);
            if (default(TOrder).LessThan(b, a)) (b, a) = (a, b);
            if (default(TOrder).LessThan(d, c)) (d, c) = (c, d);
            if (default(TOrder).LessThan(e, b)) (e, b) = (b, e);
            if (default(TOrder).LessThan(c, b)) (c, b) = (b, c);
            if (default(TOrder).LessThan(e, d)) (e, d) = (d, e);
        }

        static void SortThreeIndexes(ref T a, ref T b, ref T c)
        {
            if (default(TOrder).LessThan(c, a)) (c, a) = (a, c);
            if (default(TOrder).LessThan(b, a)) (b, a) = (a, b);
            if (default(TOrder).LessThan(c, b)) (c, b) = (b, c);
        }

        static void DualPivotQuickSort_Inclusive(T[] array, int firstIdx, int lastIdx)
        {
            if (lastIdx - firstIdx < 400) {
                QuickSort_Inclusive(array, firstIdx, lastIdx);
                //InsertionSort_InPlace(array, firstIdx, lastIdx + 1);
            } else {
                // lp means left pivot, and rp means right pivot.
                var (lowPivot, highPivot) = DualPivotPartition(array, firstIdx, lastIdx);
                var a = Task.Run(() => DualPivotQuickSort_Inclusive(array, firstIdx, lowPivot - 1));
                var b = Task.Run(() => DualPivotQuickSort_Inclusive(array, lowPivot + 1, highPivot - 1));
                DualPivotQuickSort_Inclusive(array, highPivot + 1, lastIdx);
                a.Wait();
                b.Wait();
            }
        }

        static (int lowPivot, int highPivot) DualPivotPartition(T[] arr, int firstIdx, int lastIdx)
        {
            if (default(TOrder).LessThan(arr[lastIdx], arr[firstIdx]))
                arr.Swap(firstIdx, lastIdx);

            // p is the left pivot, and q is the right pivot.
            var lowPivot = firstIdx + 1;
            var highPivot = lastIdx - 1;
            var betweenPivots = firstIdx + 1;
            var lowPivotValue = arr[firstIdx];
            var highPivotValue = arr[lastIdx];
            while (betweenPivots <= highPivot) {
                if (default(TOrder).LessThan(arr[betweenPivots], lowPivotValue)) {
                    arr.Swap(betweenPivots, lowPivot);
                    lowPivot++;
                } else if (!default(TOrder).LessThan(arr[betweenPivots], highPivotValue)) {
                    while (default(TOrder).LessThan(highPivotValue, arr[highPivot]) && betweenPivots < highPivot)
                        highPivot--;
                    arr.Swap(betweenPivots, highPivot);
                    highPivot--;
                    if (default(TOrder).LessThan(arr[betweenPivots], lowPivotValue)) {
                        arr.Swap(betweenPivots, lowPivot);
                        lowPivot++;
                    }
                }

                betweenPivots++;
            }

            lowPivot--;
            highPivot++;

            // bring pivots to their appropriate positions.
            arr.Swap(firstIdx, lowPivot);
            arr.Swap(lastIdx, highPivot);

            return (lowPivot, highPivot);
        }

        static void BitonicSort(int logn, T[] array, int firstIdx)
        {
            var endIdx = firstIdx + (1 << logn);
            var mask = (1 << logn) - 1;

            for (var i = 0; i < logn; i++)
            for (var j = 0; j <= i; j++) {
                var bitMask = 1 << (i - j);

                for (var idx = firstIdx; idx < endIdx; idx++) {
                    var up = (((idx & mask) >> i) & 2) == 0;

                    if ((idx & bitMask) == 0 && default(TOrder).LessThan(array[idx | bitMask], array[idx]) == up) {
                        var t = array[idx];
                        array[idx] = array[idx | bitMask];
                        array[idx | bitMask] = t;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertionSort_InPlace(T[] array, int firstIdx, int idxEnd)
        {
            var writeIdx = firstIdx;
            var readIdx = writeIdx + 1;
            while (readIdx < idxEnd) {
                var x = array[readIdx];
                //writeIdx == readIdx -1;
                while (writeIdx >= firstIdx && default(TOrder).LessThan(x, array[writeIdx])) {
                    array[writeIdx + 1] = array[writeIdx];
                    writeIdx--;
                }

                if (writeIdx + 1 != readIdx)
                    array[writeIdx + 1] = x;
                writeIdx = readIdx;
                readIdx = readIdx + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe void InsertionSort_InPlace_Unsafe(ref T ptr, int firstIdx, int idxEnd)
        {
            var writeIdx = firstIdx;
            var readIdx = writeIdx + 1;
            while (readIdx < idxEnd) {
                var x = Unsafe.Add(ref ptr, readIdx);
                //writeIdx == readIdx -1;
                while (writeIdx >= firstIdx && default(TOrder).LessThan(x, Unsafe.Add(ref ptr, writeIdx))) {
                    Unsafe.Add(ref ptr, writeIdx + 1) = Unsafe.Add(ref ptr, writeIdx);
                    writeIdx--;
                }

                if (writeIdx + 1 != readIdx)
                    Unsafe.Add(ref ptr, writeIdx + 1) = x;
                writeIdx = readIdx;
                readIdx = readIdx + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SelectionSort_InPlace(T[] a, int firstIdx, int idxEnd)
        {
            var lastIdx = idxEnd - 1;
            for (var j = firstIdx; j < lastIdx; j++) {
                var iMin = j;
                var minVal = a[iMin];
                for (var i = j + 1; i < idxEnd; i++)
                    if (default(TOrder).LessThan(a[i], minVal)) {
                        iMin = i;
                        minVal = a[i];
                    }

                if (iMin != j) {
                    a[iMin] = a[j];
                    a[j] = minVal;
                }
            }
        }

        public static void InsertionSort_Copy(T[] source, int firstIdx, int idxEnd, T[] target)
        {
            var readIdx = firstIdx;
            var writeIdx = firstIdx;

            while (readIdx < idxEnd) {
                var x = source[readIdx];
                //writeIdx == readIdx -1;
                while (writeIdx > firstIdx && default(TOrder).LessThan(x, target[writeIdx - 1])) {
                    target[writeIdx] = target[writeIdx - 1];
                    writeIdx--;
                }

                target[writeIdx] = x;
                readIdx = writeIdx = readIdx + 1;
            }
        }

        const int TopDownInsertionSortBatchSize = 44;
        const int BottomUpInsertionSortBatchSize = 32;

        static void AltTopDownMergeSort(T[] items, T[] scratch, int n)
        {
            CopyArray(items, 0, n, scratch);
            AltTopDownSplitMerge(items, 0, n, scratch);
        }

        static void AltTopDownSplitMerge(T[] items, int firstIdx, int endIdx, T[] scratch)
        {
            if (endIdx - firstIdx < TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace(items, firstIdx, endIdx);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            AltTopDownSplitMerge(scratch, firstIdx, middleIdx, items);
            AltTopDownSplitMerge(scratch, middleIdx, endIdx, items);
            Merge(scratch, firstIdx, middleIdx, endIdx, items);
        }

        static T[] CopyingTopDownMergeSort(T[] items, T[] scratch, int n)
        {
            var retval = new T[n];
            CopyingTopDownSplitMerge(items, retval, scratch, 0, n);
            return retval;
        }

        static void CopyingTopDownSplitMerge(T[] src, T[] items, T[] scratch, int firstIdx, int endIdx)
        {
            if (endIdx - firstIdx < TopDownInsertionSortBatchSize) {
                InsertionSort_Copy(src, firstIdx, endIdx, items);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            CopyingTopDownSplitMerge(src, scratch, items, firstIdx, middleIdx);
            CopyingTopDownSplitMerge(src, scratch, items, middleIdx, endIdx);
            Merge(scratch, firstIdx, middleIdx, endIdx, items);
        }

        static void TopDownSplitMerge_toItems_Par(T[] items, int firstIdx, int endIdx, T[] scratch)
        {
            if (endIdx - firstIdx < 400) {
                TopDownSplitMerge_toItems(items, firstIdx, endIdx, scratch);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            var t = Task.Run(() => TopDownSplitMerge_toScratch_Par(items, firstIdx, middleIdx, scratch));
            TopDownSplitMerge_toScratch_Par(items, middleIdx, endIdx, scratch);
            t.Wait();
            Merge(scratch, firstIdx, middleIdx, endIdx, items);
        }

        static void TopDownSplitMerge_toScratch_Par(T[] items, int firstIdx, int endIdx, T[] scratch)
        {
            if (endIdx - firstIdx < 400) {
                TopDownSplitMerge_toScratch(items, firstIdx, endIdx, scratch);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            var t = Task.Run(() => TopDownSplitMerge_toItems_Par(items, firstIdx, middleIdx, scratch));
            TopDownSplitMerge_toItems_Par(items, middleIdx, endIdx, scratch);
            t.Wait();
            Merge(items, firstIdx, middleIdx, endIdx, scratch);
        }

        static void TopDownSplitMerge_toItems(T[] items, int firstIdx, int endIdx, T[] scratch)
        {
            if (endIdx - firstIdx < TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace(items, firstIdx, endIdx);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            TopDownSplitMerge_toScratch(items, firstIdx, middleIdx, scratch);
            TopDownSplitMerge_toScratch(items, middleIdx, endIdx, scratch);
            Merge(scratch, firstIdx, middleIdx, endIdx, items);
        }

        static void TopDownSplitMerge_toScratch(T[] items, int firstIdx, int endIdx, T[] scratch)
        {
            if (endIdx - firstIdx < TopDownInsertionSortBatchSize) {
                InsertionSort_Copy(items, firstIdx, endIdx, scratch);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            TopDownSplitMerge_toItems(items, firstIdx, middleIdx, scratch);
            TopDownSplitMerge_toItems(items, middleIdx, endIdx, scratch);
            Merge(items, firstIdx, middleIdx, endIdx, scratch);
        }

        static void Merge(T[] source, int firstIdx, int middleIdx, int endIdx, T[] target)
        {
            int i = firstIdx, j = middleIdx, k = firstIdx;
            while (true)
                if (!default(TOrder).LessThan(source[j], source[i])) {
                    target[k++] = source[i++];
                    if (i == middleIdx) {
                        while (j < endIdx)
                            target[k++] = source[j++];
                        return;
                    }
                } else {
                    target[k++] = source[j++];
                    if (j == endIdx) {
                        while (i < middleIdx)
                            target[k++] = source[i++];
                        return;
                    }
                }
        }

        static void BottomUpMergeSort(T[] target, T[] scratchSpace, int n)
        {
            var batchesSortedUpto = 0;
            const int batchSize = BottomUpInsertionSortBatchSize;

            while (true)
                if (batchesSortedUpto + batchSize <= n) {
                    InsertionSort_InPlace(target, batchesSortedUpto, batchesSortedUpto + batchSize);
                    batchesSortedUpto += batchSize;
                } else {
                    if (n - batchesSortedUpto >= 2)
                        InsertionSort_InPlace(target, batchesSortedUpto, n);
                    break;
                }

            var A = target;
            var B = scratchSpace;

            for (var width = batchSize; width < n; width = width << 1) {
                var i = 0;
                while (i + width + width <= n) {
                    Merge(A, i, i + width, i + width + width, B);
                    i = i + width + width;
                }

                if (i + width < n)
                    Merge(A, i, i + width, n, B);
                else
                    CopyArray(A, i, n, B);
                (A, B) = (B, A);
            }

            if (target != A)
                CopyArray(A, 0, n, target);
        }

        public static void BottomUpMergeSort2(T[] a, T[] b, int n)
        {
            var s = (GetPassCount(n) & 1) != 0 ? 32 : 64;
            { // insertion sort
                int r;
                for (var l = 0; l < n; l = r) {
                    r = l + s;
                    if (r > n) r = n;
                    l--;
                    int j;
                    for (j = l + 2; j < r; j++) {
                        var t = a[j];
                        var i = j - 1;
                        while (i != l && default(TOrder).LessThan(t, a[i])) {
                            a[i + 1] = a[i];
                            i--;
                        }

                        a[i + 1] = t;
                    }
                }
            }

            while (s < n) { // while not done
                var ee = 0; // reset end index
                while (ee < n) { // merge pairs of runs
                    var ll = ee;
                    var rr = ll + s;
                    if (rr >= n) { // if only left run
                        rr = n; //   copy left run
                        while (ll < rr) {
                            b[ll] = a[ll];
                            ll++;
                        }

                        break; //   end of pass
                    }

                    ee = rr + s; // ee = end of right run
                    if (ee > n)
                        ee = n;
                    Merge(a, ll, rr, ee, b);
                }

                (a, b) = (b, a);
                s <<= 1; // double the run size
            }
        }

        static int GetPassCount(int n) // return # passes
        {
            var i = 0;
            for (var s = 1; s < n; s <<= 1)
                i++;
            return i;
        }

        public static void CopyArray(T[] source, int firstIdx, int endIdx, T[] target)
        {
            for (var k = firstIdx; k < endIdx; k++)
                target[k] = source[k];
        }
    }

    static class Helpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(this T[] arr, int a, int b) { (arr[a], arr[b]) = (arr[b], arr[a]); }

        public static int ProcScale()
        {
            var splitIters = 4;
            var threads = Environment.ProcessorCount;
            while (threads > 0) {
                threads = threads >> 1;
                splitIters++;
            }

            return splitIters;
        }

        public static string MSE(MeanVarianceAccumulator acc)
            => MSE(acc.Mean, StdErr(acc));

        static double StdErr(MeanVarianceAccumulator acc)
            => acc.SampleStandardDeviation / Math.Sqrt(acc.WeightSum);

        public static string MSE(double mean, double stderr)
        {
            var significantDigits = Math.Log10(Math.Abs(mean / stderr));
            var digitsToShow = Math.Max(2, (int)(significantDigits + 1.9));
            var fmtString = "g" + digitsToShow;
            return mean.ToString(fmtString) + "~" + stderr.ToString("g2");
        }

        public static ulong[] RandomizeUInt64()
        {
            var arr = new ulong[SortAlgoBenchProgram.MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = ((ulong)(uint)r.Next() << 32) + (uint)r.Next();
            return arr;
        }

        public static (int, int)[] RandomizePairs()
        {
            var arr = new (int, int)[SortAlgoBenchProgram.MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = (r.Next(), r.Next());
            return arr;
        }

        public static int[] RandomizeInt32()
        {
            var arr = new int[SortAlgoBenchProgram.MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = r.Next();
            return arr;
        }

        public static SampleClass[] RandomizeSampleClass()
        {
            var arr = new SampleClass[SortAlgoBenchProgram.MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = new SampleClass { Value = r.Next() };
            return arr;
        }

        public static uint[] RandomizeUInt32()
        {
            var arr = new uint[SortAlgoBenchProgram.MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = (uint)r.Next();
            return arr;
        }
    }
}
