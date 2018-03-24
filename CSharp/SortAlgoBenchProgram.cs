using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ExpressionToCodeLib;
using IncrementalMeanVarianceAccumulator;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
namespace SortAlgoBench
{
    static class SortAlgoBenchProgram
    {
        const int MaxArraySize = 1 << 15 << 3;

        static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            UInt64OrderingAlgorithms.BencherFor(RandomizeUInt64()).BenchVariousAlgos();
            Int32OrderingAlgorithms.BencherFor(RandomizeInt32()).BenchVariousAlgos();
            //UInt32OrderingAlgorithms.BencherFor(RandomizeUInt32()).BenchVariousAlgos();
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

        static ulong[] RandomizeUInt64()
        {
            var arr = new ulong[MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = ((ulong)(uint)r.Next() << 32) + (uint)r.Next();
            return arr;
        }

        static int[] RandomizeInt32()
        {
            var arr = new int[MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = r.Next();
            return arr;
        }

        static uint[] RandomizeUInt32()
        {
            var arr = new uint[MaxArraySize];
            var r = new Random(37);
            for (var j = 0; j < arr.Length; j++)
                arr[j] = (uint)r.Next();
            return arr;
        }
    }

    abstract class UInt64OrderingAlgorithms : OrderedAlgorithms<ulong, UInt64OrderingAlgorithms.UInt64Ordering>
    {
        public struct UInt64Ordering : IOrdering<ulong>
        {
            public bool LessThan(ulong a, ulong b) => a < b;
        }
    }

    abstract class UInt32OrderingAlgorithms : OrderedAlgorithms<uint, UInt32OrderingAlgorithms.UInt32Order>
    {
        public struct UInt32Order : IOrdering<uint>
        {
            public bool LessThan(uint a, uint b) => a < b;
        }
    }

    abstract class Int32OrderingAlgorithms : OrderedAlgorithms<int, Int32OrderingAlgorithms.Int32Order>
    {
        public struct Int32Order : IOrdering<int>
        {
            public bool LessThan(int a, int b) => a < b;
        }
    }

    sealed class SortAlgorithmBench<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        public SortAlgorithmBench(T[] uint64SourceData)
        {
            this.uint64SourceData = uint64SourceData;
            uint64Array = new T[uint64SourceData.Length >> 3];
        }

        readonly T[] uint64Array;
        readonly T[] uint64SourceData;

        int RefreshData(Random random)
        {
            var len = random.Next(uint64Array.Length + 1);
            var offset = random.Next(uint64SourceData.Length - len + 1);
            Array.Copy(uint64SourceData, offset, uint64Array, 0, len);
            return len;
        }

        public void BenchVariousAlgos()
        {
            BenchSort((arr, len) => Array.Sort(arr, 0, len));
            BenchSort((arr, len) => OrderedAlgorithms<T, TOrder>.QuickSort(arr, len));
            BenchSort((arr, len) => OrderedAlgorithms<T, TOrder>.TopDownMergeSort(arr, len));
            return;
            BenchSort((arr, len) => OrderedAlgorithms<T, TOrder>.BottomUpMergeSort(arr, len));
            BenchSort((arr, len) => OrderedAlgorithms<T, TOrder>.BottomUpMergeSort2(arr, len));
            BenchSort((arr, len) => OrderedAlgorithms<T, TOrder>.AltTopDownMergeSort(arr, len));
        }

        public void BenchSort(Expression<Action<T[], int>> expr)
        {
            var action = expr.Compile();
            var txt = ExpressionToCode.GetNameIn(expr.Body) + "|" + typeof(T).ToCSharpFriendlyTypeName();
            Validate(action, txt); //also a warmup
            var time = MeanVarianceAccumulator.Empty;
            var sizes = new List<int>();
            for (var i = 0; i < 100; i++) {
                var random = new Random(42);
                var sw = new Stopwatch();
                for (var k = 0; k < 25; k++) {
                    var len = RefreshData(random);
                    sw.Start();
                    action(uint64Array, len);
                    sw.Stop();
                    if (i == 0)
                        sizes.Add(len);
                }

                time = time.Add(sw.Elapsed.TotalMilliseconds);
            }

            var meanLen = sizes.Average();
            var kbTotal = sizes.Sum() * Marshal.SizeOf(typeof(T)) / 1024.0;
            Console.WriteLine($"{txt}: {SortAlgoBenchProgram.MSE(time)} (ms) for {sizes.Count} arrays of on average {meanLen:f1} items (total {kbTotal:f1}kb)");
        }

        public void Validate(Action<T[], int> action, string txt)
        {
            var random = new Random(42);
            var sw = new Stopwatch();
            for (var k = 0; k < 10; k++) {
                var len = RefreshData(random);
                long checkSum = 0;
                for (var j = 0; j < len; j++) {
                    var l = uint64Array[j];
                    checkSum = checkSum + l.GetHashCode();
                }

                sw.Start();
                action(uint64Array, len);
                sw.Stop();
                for (var j = 0; j < len; j++) {
                    var l = uint64Array[j];
                    checkSum = checkSum - l.GetHashCode();
                }

                if (checkSum != 0)
                    Console.WriteLine(txt + " has differing elements before and after sort");
                for (var j = 1; j < len; j++)
                    if (default(TOrder).LessThan(uint64Array[j], uint64Array[j - 1])) {
                        Console.WriteLine(txt + " did not sort.");
                        break;
                    }
            }
        }
    }

    public interface IOrdering<in T>
    {
        bool LessThan(T a, T b);
    }

    abstract class OrderedAlgorithms<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        public static SortAlgorithmBench<T, TOrder> BencherFor(T[] arr) => new SortAlgorithmBench<T, TOrder>(arr);
        protected OrderedAlgorithms() => throw new NotSupportedException("allow subclassing so you can fix type parameters, but not instantiation.");
        static TOrder Ordering => default(TOrder);

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

        public static T[] TopDownMergeSort_Copy(T[] array, int endIdx)
            => CopyingTopDownMergeSort(array, new T[endIdx], endIdx);

        public static void AltTopDownMergeSort(T[] array, int endIdx)
            => AltTopDownMergeSort(array, GetCachedAccumulator(endIdx), endIdx);

        public static void BottomUpMergeSort(T[] array, int endIdx)
            => BottomUpMergeSort(array, GetCachedAccumulator(endIdx), endIdx);

        public static void BottomUpMergeSort2(T[] array, int endIdx)
            => BottomUpMergeSort2(array, GetCachedAccumulator(endIdx), endIdx);

        public static void QuickSort(T[] array)
            => QuickSort(array, 0, array.Length);

        public static void QuickSort(T[] array, int endIdx)
            => QuickSort(array, 0, endIdx);

        public static void QuickSort(T[] array, int firstIdx, int endIdx) { QuickSort_Inclusive(array, firstIdx, endIdx - 1); }

        static void QuickSort_Inclusive(T[] array, int firstIdx, int lastIdx)
        {
            while (true)
                if (lastIdx - firstIdx < TopDownInsertionSortBatchSize - 1) {
                    InsertionSort_InPlace(array, firstIdx, lastIdx + 1);
                    return;
                } else {
                    var pivot = Partition(array, firstIdx, lastIdx);
                    if (pivot - firstIdx > lastIdx - pivot) {
                        QuickSort_Inclusive(array, pivot + 1, lastIdx);
                        lastIdx = pivot; //QuickSort(array, firstIdx, pivot);
                    } else {
                        QuickSort_Inclusive(array, firstIdx, pivot);
                        firstIdx = pivot + 1; //QuickSort(array, pivot + 1, lastIdx);
                    }
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Partition(T[] array, int firstIdx, int lastIdx)
        {
            var pivotValue = array[(firstIdx + lastIdx) >> 1];
            while (true) {
                while (Ordering.LessThan(array[firstIdx], pivotValue))
                    firstIdx++;
                while (Ordering.LessThan(pivotValue, array[lastIdx]))
                    lastIdx--;
                if (lastIdx <= firstIdx)
                    return lastIdx;
                var tmp = array[firstIdx];
                array[firstIdx] = array[lastIdx];
                array[lastIdx] = tmp;
                firstIdx++;
                lastIdx--;
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
                while (writeIdx >= firstIdx && Ordering.LessThan(x, array[writeIdx])) {
                    array[writeIdx + 1] = array[writeIdx];
                    writeIdx--;
                }

                if (writeIdx + 1 != readIdx)
                    array[writeIdx + 1] = x;
                writeIdx = readIdx;
                readIdx = readIdx + 1;
            }
        }

        public static void InsertionSort_Copy(T[] source, int firstIdx, int idxEnd, T[] target)
        {
            var readIdx = firstIdx;
            var writeIdx = firstIdx;

            while (readIdx < idxEnd) {
                var x = source[readIdx];
                //writeIdx == readIdx -1;
                while (writeIdx > firstIdx && Ordering.LessThan(x, target[writeIdx - 1])) {
                    target[writeIdx] = target[writeIdx - 1];
                    writeIdx--;
                }

                target[writeIdx] = x;
                readIdx = writeIdx = readIdx + 1;
            }
        }

        const int TopDownInsertionSortBatchSize = 48;
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
                //CopyArray(src, firstIdx, endIdx, items);
                //InsertionSort_InPlace(items, firstIdx, endIdx);
                InsertionSort_Copy(src, firstIdx, endIdx, items);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            CopyingTopDownSplitMerge(src, scratch, items, firstIdx, middleIdx);
            CopyingTopDownSplitMerge(src, scratch, items, middleIdx, endIdx);
            Merge(scratch, firstIdx, middleIdx, endIdx, items);
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
                if (!Ordering.LessThan(source[j], source[i])) {
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
            while (true)
                if (batchesSortedUpto + BottomUpInsertionSortBatchSize <= n) {
                    InsertionSort_InPlace(target, batchesSortedUpto, batchesSortedUpto + BottomUpInsertionSortBatchSize);
                    batchesSortedUpto += BottomUpInsertionSortBatchSize;
                } else {
                    if (n - batchesSortedUpto >= 2)
                        InsertionSort_InPlace(target, batchesSortedUpto, n);
                    break;
                }

            var A = target;
            var B = scratchSpace;

            for (var width = BottomUpInsertionSortBatchSize; width < n; width = width << 1) {
                var i = 0;
                while (i + width + width <= n) {
                    Merge(A, i, i + width, i + width + width, B);
                    i = i + width + width;
                }

                if (i + width < n)
                    Merge(A, i, i + width, n, B);
                else
                    CopyArray(A, i, n, B);
                var tmp = A;
                A = B;
                B = tmp;
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
                        while (i != l && Ordering.LessThan(t, a[i])) {
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

                var tmp = a; // swap a and b
                a = b;
                b = tmp;
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
}
