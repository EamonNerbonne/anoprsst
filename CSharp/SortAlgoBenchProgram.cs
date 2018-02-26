using System;
using System.Diagnostics;
using System.Linq.Expressions;
using ExpressionToCodeLib;
using IncrementalMeanVarianceAccumulator;

namespace SortAlgoBench
{
    static class SortAlgoBenchProgram
    {
        static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            Randomize(uint64SourceData);
            BenchSort(arr => UInt64OrderingAlgorithms.BottomUpMergeSort2(arr));
            BenchSort(arr => UInt64OrderingAlgorithms.BottomUpMergeSort(arr));
            BenchSort(arr => UInt64OrderingAlgorithms.TopDownMergeSort(arr));
            BenchSort(arr => UInt64OrderingAlgorithms.QuickSort(arr));
            BenchSort(arr => Array.Sort(arr));
            BenchSort(arr => UInt64OrderingAlgorithms.AltTopDownMergeSort(arr));
        }

        const int ArrSize = 1 << 24;
        static readonly ulong[] uint64Array = new ulong[ArrSize];
        static readonly ulong[] uint64SourceData = new ulong[ArrSize * 5];
        static readonly Random random = new Random(42);

        static void RefreshData()
            => Array.Copy(uint64SourceData, random.Next(ArrSize << 2), uint64Array, 0, ArrSize);

        static string MSE(MeanVarianceAccumulator acc)
            => MSE(acc.Mean, StdErr(acc));

        static double StdErr(MeanVarianceAccumulator acc)
            => acc.SampleStandardDeviation / Math.Sqrt(acc.WeightSum);

        static string MSE(double mean, double stderr)
        {
            var significantDigits = Math.Log10(Math.Abs(mean / stderr));
            var digitsToShow = Math.Max(2, (int)(significantDigits + 1.9));
            var fmtString = "g" + digitsToShow;
            return mean.ToString(fmtString) + "~" + stderr.ToString("g2");
        }

        static void Randomize(ulong[] arr)
        {
            var r = random;
            for (var j = 0; j < arr.Length; j++)
                arr[j] = ((ulong)(uint)r.Next() << 32) + (uint)r.Next();
        }

        static void BenchSort(Expression<Action<ulong[]>> expr)
        {
            var action = expr.Compile();
            var txt = ExpressionToCode.ToCode(expr.Body);
            action(uint64Array); //warmup
            var justsort = MeanVarianceAccumulator.Empty;
            for (var i = 0; i < 30; i++) {
                RefreshData();
                ulong checkSum = 0;
                foreach (var l in uint64Array)
                    checkSum = checkSum ^ l;
                var sw = Stopwatch.StartNew();
                action(uint64Array);
                justsort = justsort.Add(sw.Elapsed.TotalMilliseconds);
                foreach (var l in uint64Array)
                    checkSum = checkSum ^ l;
                if(checkSum != 0)
                    Console.WriteLine(txt +" has differing elements before and after sort");
                for (var j = 1; j < uint64Array.Length; j++) {
                    if(uint64Array[j-1] >uint64Array[j]) {
                        Console.WriteLine(txt +" did not sort.");
                        break;
                    }
                }
            }

            Console.WriteLine($"{txt}: {MSE(justsort)} (ms)");
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

    abstract class SortAlgorithmBench<T, TOrder>
        where TOrder : struct, IOrdering<T> { }

    public interface IOrdering<in T>
    {
        bool LessThan(T a, T b);
    }

    abstract class OrderedAlgorithms<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
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

        public static void TopDownMergeSort(T[] array)
            => TopDownMergeSort(array, GetCachedAccumulator(array.Length), array.Length);

        public static T[] TopDownMergeSort_Copy(T[] array)
            => CopyingTopDownMergeSort(array, new T[array.Length], array.Length);

        public static void AltTopDownMergeSort(T[] array)
            => AltTopDownMergeSort(array, GetCachedAccumulator(array.Length), array.Length);

        public static void BottomUpMergeSort(T[] array)
            => BottomUpMergeSort(array, GetCachedAccumulator(array.Length), array.Length);
        public static void BottomUpMergeSort2(T[] array)
            => BottomUpMergeSort2(array, GetCachedAccumulator(array.Length), array.Length);

        public static void QuickSort(T[] array)
            => QuickSort(array, 0, array.Length);

        public static void QuickSort(T[] array, int firstIdx, int endIdx) { QuickSort_Inclusive(array, firstIdx, endIdx - 1); }

        static void QuickSort_Inclusive(T[] array, int firstIdx, int lastIdx)
        {
            while (true)
                if (lastIdx - firstIdx < InsertionSortBatchSize - 1) {
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

        static int Partition(T[] array, int firstIdx, int lastIdx)
        {
            var pivotValue = array[(firstIdx + lastIdx) / 2];
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

        const int InsertionSortBatchSize = 48;

        static void AltTopDownMergeSort(T[] items, T[] scratch, int n)
        {
            CopyArray(items, 0, n, scratch);
            TopDownSplitMerge_Either(items, 0, n, scratch);
        }

        static void TopDownSplitMerge_Either(T[] items, int iBegin, int iEnd, T[] scratch)
        {
            if (iEnd - iBegin < InsertionSortBatchSize) {
                InsertionSort_InPlace(items, iBegin, iEnd);
                return;
            }

            int iMiddle = (iEnd + iBegin) / 2;
            TopDownSplitMerge_Either(scratch, iBegin, iMiddle, items);
            TopDownSplitMerge_Either(scratch, iMiddle, iEnd, items);
            Merge(scratch, iBegin, iMiddle, iEnd, items);
        }

        static T[] CopyingTopDownMergeSort(T[] items, T[] scratch, int n)
        {
            var retval = new T[n];
            CopyingTopDownSplitMerge(items, retval, scratch, 0, n);
            return retval;
        }

        static void CopyingTopDownSplitMerge(T[] src, T[] items, T[] scratch, int iBegin, int iEnd)
        {
            if (iEnd - iBegin < InsertionSortBatchSize) {
                //CopyArray(src, iBegin, iEnd, items);
                //InsertionSort_InPlace(items, iBegin, iEnd);
                InsertionSort_Copy(src, iBegin, iEnd, items);
                return;
            }

            int iMiddle = (iEnd + iBegin) / 2;
            CopyingTopDownSplitMerge(src, scratch, items, iBegin, iMiddle);
            CopyingTopDownSplitMerge(src, scratch, items, iMiddle, iEnd);
            Merge(scratch, iBegin, iMiddle, iEnd, items);
        }

        static void TopDownMergeSort(T[] items, T[] scratch, int n) { TopDownSplitMerge_toItems(items, 0, n, scratch); }

        static void TopDownSplitMerge_toItems(T[] items, int iBegin, int iEnd, T[] scratch)
        {
            if (iEnd - iBegin < InsertionSortBatchSize) {
                InsertionSort_InPlace(items, iBegin, iEnd);
                return;
            }

            int iMiddle = (iEnd + iBegin) / 2;
            TopDownSplitMerge_toScratch(items, iBegin, iMiddle, scratch);
            TopDownSplitMerge_toScratch(items, iMiddle, iEnd, scratch);
            Merge(scratch, iBegin, iMiddle, iEnd, items);
        }

        static void TopDownSplitMerge_toScratch(T[] items, int iBegin, int iEnd, T[] scratch)
        {
            if (iEnd - iBegin < InsertionSortBatchSize) {
                InsertionSort_Copy(items, iBegin, iEnd, scratch);
                return;
            }

            int iMiddle = (iEnd + iBegin) / 2;
            TopDownSplitMerge_toItems(items, iBegin, iMiddle, scratch);
            TopDownSplitMerge_toItems(items, iMiddle, iEnd, scratch);
            Merge(items, iBegin, iMiddle, iEnd, scratch);
        }

        static void Merge(T[] source, int iBegin, int iMiddle, int iEnd, T[] target)
        {
            int i = iBegin, j = iMiddle, k = iBegin;
            while (true)
                if (!Ordering.LessThan(source[j], source[i])) {
                    target[k++] = source[i++];
                    if (i == iMiddle) {
                        while (j < iEnd)
                            target[k++] = source[j++];
                        return;
                    }
                } else {
                    target[k++] = source[j++];
                    if (j == iEnd) {
                        while (i < iMiddle)
                            target[k++] = source[i++];
                        return;
                    }
                }
        }

        static void BottomUpMergeSort(T[] target, T[] scratchSpace, int n)
        {
            var batchesSortedUpto = 0;
            while (true)
                if (batchesSortedUpto + InsertionSortBatchSize <= n) {
                    InsertionSort_InPlace(target, batchesSortedUpto, batchesSortedUpto + InsertionSortBatchSize);
                    batchesSortedUpto += InsertionSortBatchSize;
                } else {
                    if (n - batchesSortedUpto >= 2)
                        InsertionSort_InPlace(target, batchesSortedUpto, n);
                    break;
                }

            var A = target;
            var B = scratchSpace;

            for (var width = InsertionSortBatchSize; width < n; width = width << 1) {
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

            if (target != A) {
                CopyArray(A, 0, n, target);
            }
        }

        public static void BottomUpMergeSort2(T[] a, T[] b, int n)
        {
            var s = (GetPassCount(n) & 1) != 0 ? 32 : 64;
            { // insertion sort
                int r;
                for (int l = 0; l < n; l = r) {
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

        public static void CopyArray(T[] source, int iBegin, int iEnd, T[] target)
        {
            for (int k = iBegin; k < iEnd; k++)
                target[k] = source[k];
        }
    }
}
