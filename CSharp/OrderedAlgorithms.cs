using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SortAlgoBench {
    public interface IOrdering<in T> {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool LessThan(T a, T b);
    }

    abstract class OrderedAlgorithms<T, TOrder>
        where TOrder : struct, IOrdering<T> {
        //*
        const int TopDownInsertionSortBatchSize = 32;
        /*/
        static readonly int TopDownInsertionSortBatchSize = !typeof(T).IsValueType ? 24 
            : Unsafe.SizeOf<T>() > 32 ? 16
            : Unsafe.SizeOf<T>() > 8 ? 32
            : Unsafe.SizeOf<T>() > 4 ? 44
            : 64;
            /**/
        const int BottomUpInsertionSortBatchSize = 24;


        public static SortAlgorithmBench<T, TOrder> BencherFor(T[] arr) => new SortAlgorithmBench<T, TOrder>(arr);
        protected OrderedAlgorithms() => throw new NotSupportedException("allow subclassing so you can fix type parameters, but not instantiation.");

        public static void TopDownMergeSort(T[] array, int endIdx)
            => TopDownSplitMerge_toItems(array, 0, endIdx, new T[endIdx]);

        public static void ParallelTopDownMergeSort(T[] array, int endIdx)
            => TopDownSplitMerge_toItems_Par(array, 0, endIdx, new T[endIdx]);

        public static T[] TopDownMergeSort_Copy(T[] array, int endIdx)
            => CopyingTopDownMergeSort(array, new T[endIdx], endIdx);

        public static void AltTopDownMergeSort(T[] array, int endIdx)
            => AltTopDownMergeSort(array, new T[endIdx], endIdx);

        public static void BottomUpMergeSort(T[] array, int endIdx)
            => BottomUpMergeSort(array, new T[endIdx], endIdx);

        public static void BottomUpMergeSort2(T[] array, int endIdx)
            => BottomUpMergeSort2(array, new T[endIdx], endIdx);

        public static void QuickSort(T[] array) => QuickSort(array, array.Length - 1);

        public static void QuickSort(T[] array, int endIdx) => QuickSort_Inclusive_Unsafe(ref array[0], endIdx - 1);

        public static void QuickSort(T[] array, int firstIdx, int endIdx) { QuickSort_Inclusive_Unsafe(ref array[firstIdx], endIdx - firstIdx - 1); }
        public static void ParallelQuickSort(T[] array) => QuickSort_Inclusive_Parallel(array, 0, array.Length - 1);
        public static void ParallelQuickSort(T[] array, int endIdx) => QuickSort_Inclusive_Parallel(array, 0, endIdx - 1);
        public static void ParallelQuickSort(T[] array, int firstIdx, int endIdx) { QuickSort_Inclusive_Parallel(array, firstIdx, endIdx - 1); }
        public static void ParallelDualPivotQuickSort(T[] array) => DualPivotQuickSort_Inclusive(array, 0, array.Length - 1);
        public static void ParallelDualPivotQuickSort(T[] array, int endIdx) => DualPivotQuickSort_Inclusive(array, 0, endIdx - 1);
        public static void ParallelDualPivotQuickSort(T[] array, int firstIdx, int endIdx) => DualPivotQuickSort_Inclusive(array, firstIdx, endIdx - 1);

        static void QuickSort_Inclusive_Parallel(T[] array, int firstIdx, int lastIdx) {
            Helpers.BoundsCheck(array, firstIdx, lastIdx);
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

        struct QuickSort_Inclusive_ParallelArgs {
            public T[] array;
            public int firstIdx;
            public int lastIdx;
            public int splitAt;
            public CountdownEvent countdownEvent;
            static readonly WaitCallback QuickSort_Inclusive_Par2_callback = o => Impl((QuickSort_Inclusive_ParallelArgs)o);

            public static void Impl(in QuickSort_Inclusive_ParallelArgs args) {
                var firstIdx = args.firstIdx;
                var lastIdx = args.lastIdx;
                var countdownEvent = args.countdownEvent;
                while (lastIdx - firstIdx >= args.splitAt) {
                    var pivot = PartitionMedian5_Unsafe(ref args.array[firstIdx], lastIdx - firstIdx) + firstIdx;
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

                QuickSort_Inclusive_Small_Unsafe(ref args.array[firstIdx], lastIdx - firstIdx);
                countdownEvent.Signal();
            }
        }

        static void QuickSort_Inclusive_Unsafe(ref T ptr, int lastOffset) {
            while (lastOffset >= TopDownInsertionSortBatchSize << 9) {
                var pivot = PartitionMedian5_Unsafe(ref ptr, lastOffset);
                QuickSort_Inclusive_Unsafe(ref Unsafe.Add(ref ptr, pivot + 1), lastOffset - (pivot + 1));
                lastOffset = pivot; //QuickSort_Inclusive_Unsafe(ref ptr, pivot);
            }
            QuickSort_Inclusive_Small_Unsafe(ref ptr, lastOffset);
        }

        static void QuickSort_Inclusive_Small_Unsafe(ref T firstPtr, int lastOffset) {
            while (lastOffset >= TopDownInsertionSortBatchSize) {
                var pivotIdx = Partition_Unsafe(ref firstPtr, lastOffset);
                QuickSort_Inclusive_Small_Unsafe(ref Unsafe.Add(ref firstPtr, pivotIdx + 1), lastOffset - (pivotIdx + 1));
                lastOffset = pivotIdx; //QuickSort(array, firstIdx, pivot);
            }
            InsertionSort_InPlace_Unsafe_Inclusive(ref firstPtr, ref Unsafe.Add(ref firstPtr, lastOffset));
        }

        static int Partition_Unsafe(ref T firstPtr, int lastOffset) {
            var midpoint = lastOffset >> 1;
            var pivotValue = Unsafe.Add(ref firstPtr, midpoint);
            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
            while (true) {
                while (default(TOrder).LessThan(firstPtr, pivotValue)) {
                    firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                }
                while (default(TOrder).LessThan(pivotValue, lastPtr)) {
                    lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
                    lastOffset--;
                }
                if (!Unsafe.IsAddressGreaterThan(ref lastPtr, ref firstPtr))
                    break;// TODO: Workaround for https://github.com/dotnet/coreclr/issues/9692
                (firstPtr, lastPtr) = (lastPtr, firstPtr);
                firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
                lastOffset--;
            }
            return lastOffset;
        }

        static int PartitionMedian5_Unsafe(ref T firstPtr, int lastOffset) {
            var midpoint = lastOffset >> 1;
            ref var midPtr = ref Unsafe.Add(ref firstPtr, midpoint);
            //*
            SortFiveIndexes(
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, 1),
                ref midPtr,
                ref Unsafe.Add(ref firstPtr, lastOffset - 1),
                ref Unsafe.Add(ref firstPtr, lastOffset));

            var pivotValue = midPtr;

            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset - 2);
            firstPtr = ref Unsafe.Add(ref firstPtr, 2);
            lastOffset = lastOffset - 2;
            /*/
            SortThreeIndexes(
                ref firstPtr,
                ref midPtr,
                ref Unsafe.Add(ref firstPtr, lastOffset));
            var pivotValue = midPtr;

            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset - 1);
            firstPtr = ref Unsafe.Add(ref firstPtr, 1);
            lastOffset = lastOffset - 1;
            var firstOffset = 1;
            /**/
            while (true) {
                while (default(TOrder).LessThan(firstPtr, pivotValue)) {
                    firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                }
                while (default(TOrder).LessThan(pivotValue, lastPtr)) {
                    lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
                    lastOffset--;
                }
                if (!Unsafe.IsAddressGreaterThan(ref lastPtr, ref firstPtr))
                    break;// TODO: Workaround for https://github.com/dotnet/coreclr/issues/9692
                lastOffset--;
                (firstPtr, lastPtr) = (lastPtr, firstPtr);
                firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
            }
            return lastOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SortFiveIndexes(ref T a, ref T b, ref T c, ref T d, ref T e) {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SortThreeIndexes(ref T a, ref T b, ref T c) {
            if (default(TOrder).LessThan(c, a)) (c, a) = (a, c);
            if (default(TOrder).LessThan(b, a)) (b, a) = (a, b);
            if (default(TOrder).LessThan(c, b)) (c, b) = (b, c);
        }

        static void DualPivotQuickSort_Inclusive(T[] array, int firstIdx, int lastIdx) {
            if (lastIdx - firstIdx < 400) {
                QuickSort_Inclusive_Small_Unsafe(ref array[firstIdx], lastIdx - firstIdx);
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

        static (int lowPivot, int highPivot) DualPivotPartition(T[] arr, int firstIdx, int lastIdx) {
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

        static void BitonicSort(int logn, T[] array, int firstIdx) {
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

        public static void InsertionSort_InPlace(T[] array, int firstIdx, int idxEnd) {
            var writeIdx = firstIdx;
            var readIdx = writeIdx + 1;
            while (readIdx < idxEnd) {
                if (default(TOrder).LessThan(array[readIdx], array[writeIdx])) {
                    var readValue = array[readIdx];
                    while (true) {
                        array[writeIdx + 1] = array[writeIdx];
                        if (writeIdx > firstIdx && default(TOrder).LessThan(readValue, array[writeIdx - 1])) {
                            writeIdx--;
                        } else {
                            array[writeIdx] = readValue;
                            break;
                        }
                    }
                }
                writeIdx = readIdx;
                readIdx = readIdx + 1;
            }
        }

        static void InsertionSort_InPlace_Unsafe_Inclusive(ref T firstPtr, ref T lastPtr) {
            if (Unsafe.AreSame(ref firstPtr, ref lastPtr))
                return;
            ref var writePtr = ref firstPtr;
            ref var readPtr = ref Unsafe.Add(ref firstPtr, 1);
            while (true) {//readIdx < idxEnd
                var readValue = readPtr;
                if (default(TOrder).LessThan(readValue, writePtr)) {
                    while (true) {
                        //default(TOrder).LessThan(readValue, writePtr) holds
                        Unsafe.Add(ref writePtr, 1) = writePtr;
                        if (Unsafe.AreSame(ref writePtr, ref firstPtr)) {
                            break;
                        }
                        writePtr = ref Unsafe.Subtract(ref writePtr, 1);
                        if (!default(TOrder).LessThan(readValue, writePtr)) {
                            writePtr = ref Unsafe.Add(ref writePtr, 1);
                            break;
                        }
                        //default(TOrder).LessThan(readValue, writePtr) holds
                    }
                    writePtr = readValue;
                }
                if (Unsafe.AreSame(ref readPtr, ref lastPtr)) {
                    break;
                }
                writePtr = ref readPtr;
                readPtr = ref Unsafe.Add(ref readPtr, 1);
            }
        }

        public static void SelectionSort_InPlace(T[] a, int firstIdx, int idxEnd) {
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

        public static void InsertionSort_Copy(T[] source, int firstIdx, int idxEnd, T[] target) {
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

        static void AltTopDownMergeSort(T[] items, T[] scratch, int n) {
            CopyArray(items, 0, n, scratch);
            AltTopDownSplitMerge(items, 0, n, scratch);
        }

        static void AltTopDownSplitMerge(T[] items, int firstIdx, int endIdx, T[] scratch) {
            if (endIdx - firstIdx < TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace(items, firstIdx, endIdx);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            AltTopDownSplitMerge(scratch, firstIdx, middleIdx, items);
            AltTopDownSplitMerge(scratch, middleIdx, endIdx, items);
            Merge(scratch, firstIdx, middleIdx, endIdx, items);
        }

        static T[] CopyingTopDownMergeSort(T[] items, T[] scratch, int n) {
            var retval = new T[n];
            CopyingTopDownSplitMerge(items, retval, scratch, 0, n);
            return retval;
        }

        static void CopyingTopDownSplitMerge(T[] src, T[] items, T[] scratch, int firstIdx, int endIdx) {
            if (endIdx - firstIdx < TopDownInsertionSortBatchSize) {
                InsertionSort_Copy(src, firstIdx, endIdx, items);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            CopyingTopDownSplitMerge(src, scratch, items, firstIdx, middleIdx);
            CopyingTopDownSplitMerge(src, scratch, items, middleIdx, endIdx);
            Merge(scratch, firstIdx, middleIdx, endIdx, items);
        }

        static void TopDownSplitMerge_toItems_Par(T[] items, int firstIdx, int endIdx, T[] scratch) {
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

        static void TopDownSplitMerge_toScratch_Par(T[] items, int firstIdx, int endIdx, T[] scratch) {
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

        static void TopDownSplitMerge_toItems(T[] items, int firstIdx, int endIdx, T[] scratch) {
            if (endIdx - firstIdx < TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace(items, firstIdx, endIdx);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            TopDownSplitMerge_toScratch(items, firstIdx, middleIdx, scratch);
            TopDownSplitMerge_toScratch(items, middleIdx, endIdx, scratch);
            Merge(scratch, firstIdx, middleIdx, endIdx, items);
        }

        static void TopDownSplitMerge_toScratch(T[] items, int firstIdx, int endIdx, T[] scratch) {
            if (endIdx - firstIdx < TopDownInsertionSortBatchSize) {
                InsertionSort_Copy(items, firstIdx, endIdx, scratch);
                return;
            }

            var middleIdx = (endIdx + firstIdx) / 2;
            TopDownSplitMerge_toItems(items, firstIdx, middleIdx, scratch);
            TopDownSplitMerge_toItems(items, middleIdx, endIdx, scratch);
            Merge(items, firstIdx, middleIdx, endIdx, scratch);
        }

        static void Merge(T[] source, int firstIdx, int middleIdx, int endIdx, T[] target) {
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

        static void BottomUpMergeSort(T[] target, T[] scratchSpace, int n) {
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

        public static void BottomUpMergeSort2(T[] a, T[] b, int n) {
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

        public static void CopyArray(T[] source, int firstIdx, int endIdx, T[] target) {
            for (var k = firstIdx; k < endIdx; k++)
                target[k] = source[k];
        }
    }
}
