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

    public abstract class OrderedAlgorithms<T, TOrder>
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
        const int QuickSortNoMedianThreshold = 16_384;
        const int MinimalParallelQuickSortBatchSize = 64;

        public static SortAlgorithmBench<T, TOrder> BencherFor(T[] arr, int TimingTrials, int IterationsPerTrial) => new SortAlgorithmBench<T, TOrder>(arr, TimingTrials, IterationsPerTrial);
        protected OrderedAlgorithms() => throw new NotSupportedException("allow subclassing so you can fix type parameters, but not instantiation.");
        public static void TopDownMergeSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                TopDownSplitMerge_toItems(array, 0, endIdx, new T[endIdx]);
        }

        public static void ParallelTopDownMergeSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                TopDownSplitMerge_toItems_Par(array, 0, endIdx, new T[endIdx]);
        }

        public static T[] TopDownMergeSort_Copy(T[] array, int endIdx) {
            return CopyingTopDownMergeSort(array, new T[endIdx], endIdx);
        }

        public static void AltTopDownMergeSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                AltTopDownMergeSort(array, new T[endIdx], endIdx);
        }

        public static void BottomUpMergeSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                BottomUpMergeSort(array, new T[endIdx], endIdx);
        }

        public static void BottomUpMergeSort2(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                BottomUpMergeSort2(array, new T[endIdx], endIdx);
        }

        public static void QuickSort(T[] array) {
            if (Helpers.NeedsSort_WithBoundsCheck(array))
                QuickSort(array, array.Length - 1);
        }

        public static void QuickSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                QuickSort_Inclusive_Unsafe(ref array[0], endIdx - 1);
        }
        public static void QuickSort(T[] array, int firstIdx, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, firstIdx, endIdx))
                QuickSort_Inclusive_Unsafe(ref array[firstIdx], endIdx - firstIdx - 1);
        }
        public static void ParallelQuickSort(T[] array) {
            if (Helpers.NeedsSort_WithBoundsCheck(array))
                QuickSort_Inclusive_Parallel(array, 0, array.Length - 1);
        }

        public static void ParallelQuickSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                QuickSort_Inclusive_Parallel(array, 0, endIdx - 1);
        }
        public static void ParallelQuickSort(T[] array, int firstIdx, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, firstIdx, endIdx))
                QuickSort_Inclusive_Parallel(array, firstIdx, endIdx - 1);
        }
        public static void DualPivotQuickSort(T[] array) {
            if (Helpers.NeedsSort_WithBoundsCheck(array))
                DualPivotQuickSort_Inclusive(array, 0, array.Length - 1);
        }

        public static void DualPivotQuickSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                DualPivotQuickSort_Inclusive(array, 0, endIdx - 1);
        }

        public static void DualPivotQuickSort(T[] array, int firstIdx, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, firstIdx, endIdx))
                DualPivotQuickSort_Inclusive(array, firstIdx, endIdx - 1);
        }

        static void QuickSort_Inclusive_Parallel(T[] array, int firstIdx, int lastIdx) {
            var countdownEvent = new CountdownEvent(1);
            new QuickSort_Inclusive_ParallelArgs {
                array = array,
                countdownEvent = countdownEvent,
                splitAt = Math.Max(lastIdx - firstIdx >> SortAlgoBenchProgram.ParallelSplitScale, MinimalParallelQuickSortBatchSize),
            }.Impl(firstIdx, lastIdx);
            countdownEvent.Wait();
        }

        class QuickSort_Inclusive_ParallelArgs {
            public T[] array;
            public CountdownEvent countdownEvent;
            public int splitAt;
            static readonly WaitCallback QuickSort_Inclusive_Par2_callback = o => { var (parArgs, firstIdx, lastIdx) = (((QuickSort_Inclusive_ParallelArgs, int, int))o); parArgs.Impl(firstIdx, lastIdx); };

            public void Impl(int firstIdx, int lastIdx) {
                var array = this.array;
                var splitAt = this.splitAt;
                var countdownEvent = this.countdownEvent;
                while (lastIdx - firstIdx >= splitAt) {
                    var pivot = PartitionWithMedian_Unsafe(ref array[firstIdx], lastIdx - firstIdx) + firstIdx;
                    countdownEvent.AddCount(1);
                    ThreadPool.UnsafeQueueUserWorkItem(QuickSort_Inclusive_Par2_callback, (this, pivot + 1, lastIdx));
                    lastIdx = pivot; //effectively QuickSort_Inclusive(array, firstIdx, pivot);
                }

                QuickSort_Inclusive_Small_Unsafe(ref array[firstIdx], lastIdx - firstIdx);
                countdownEvent.Signal();
            }
        }

        static void QuickSort_Inclusive_Unsafe(ref T ptr, int lastOffset) {
            while (lastOffset >= QuickSortNoMedianThreshold) {
                var pivot = PartitionWithMedian_Unsafe(ref ptr, lastOffset);
                QuickSort_Inclusive_Unsafe(ref Unsafe.Add(ref ptr, pivot + 1), lastOffset - (pivot + 1));
                lastOffset = pivot; //QuickSort_Inclusive_Unsafe(ref ptr, pivot);
            }
            QuickSort_Inclusive_Small_Unsafe(ref ptr, lastOffset);
        }


        /// <summary>
        /// precondition:memory in range [firstPtr, firstPtr+lastOffset] can be mutated, also implying lastOffset >= 0
        /// </summary>
        static void QuickSort_Inclusive_Small_Unsafe(ref T firstPtr, int lastOffset) {
            while (lastOffset >= TopDownInsertionSortBatchSize) {
                //invariant: lastOffset >= 1
                var pivotIdx = Partition_Unsafe(ref firstPtr, lastOffset);
                //invariant: pivotIdx in [0, lastOffset-1]
                QuickSort_Inclusive_Small_Unsafe(ref Unsafe.Add(ref firstPtr, pivotIdx + 1), lastOffset - (pivotIdx + 1));
                lastOffset = pivotIdx; //QuickSort(array, firstIdx, pivot);
            }
            InsertionSort_InPlace_Unsafe_Inclusive(ref firstPtr, ref Unsafe.Add(ref firstPtr, lastOffset));
        }


        /// <summary>
        /// Precondition: memory in range [firstPtr, firstPtr+lastOffset] can be mutated, and lastOffset >= 1
        /// Postcondition: returnvalue in range [0, lastOffset-1]
        /// </summary>
        public static int Partition_Unsafe(ref T firstPtr, int lastOffset) {
            //precondition: 1 <= lastOffset
            //so midpoint != lastOffset
#if false
            ref var midPtr = ref Unsafe.Add(ref firstPtr, lastOffset >> 1);
            SortThreeIndexes(ref firstPtr, ref midPtr, ref Unsafe.Add(ref firstPtr, lastOffset));
            var pivotValue = midPtr;
            lastOffset--;
            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
            firstPtr = ref Unsafe.Add(ref firstPtr, 1);
#else
            var pivotValue = Unsafe.Add(ref firstPtr, lastOffset >> 1);
            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
#endif
            while (true) {
                //on the first iteration,  the following loop bails at the lastest when it reaches the midpoint, so ref firstPtr < ref lastPtr
                while (default(TOrder).LessThan(firstPtr, pivotValue)) {
                    firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                }
                //on the first iteration, the following loop either succeeds at least once (decrementing lastOffset), or it bails immediately
                while (default(TOrder).LessThan(pivotValue, lastPtr)) {
                    lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
                    lastOffset--;
                }
                //on the first iteration, either lastOffset has been decremented, OR ref lastPtr > ref firstPtr; so if we break here, then lastOffset was decremented
                if (!Unsafe.IsAddressGreaterThan(ref lastPtr, ref firstPtr))
                    break;// TODO: Workaround for https://github.com/dotnet/coreclr/issues/9692
                (firstPtr, lastPtr) = (lastPtr, firstPtr);
                firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
                //on the first iteration lastOffset was decremented at least once.
                lastOffset--;
            }
            return lastOffset;
        }

        static int PartitionWithMedian_Unsafe(ref T firstPtr, int lastOffset) {
            var midpoint = lastOffset >> 1;
            ref var midPtr = ref Unsafe.Add(ref firstPtr, midpoint);

#if true
            //InsertionSort_InPlace_Unsafe_Inclusive(ref Unsafe.Add(ref firstPtr, midpoint-3),ref Unsafe.Add(ref firstPtr, midpoint+3));
            //var pivotValue = midPtr;
            //ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
            MedianOf5(
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, 1),
                ref midPtr,
                ref Unsafe.Add(ref firstPtr, lastOffset - 1),
                ref Unsafe.Add(ref firstPtr, lastOffset));

            var pivotValue = midPtr;

            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset - 2);
            firstPtr = ref Unsafe.Add(ref firstPtr, 2);
            lastOffset = lastOffset - 2;
#elif true
            MedianOf7(
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, 1),
                ref Unsafe.Add(ref firstPtr, 2),
                ref midPtr,
                ref Unsafe.Add(ref firstPtr, lastOffset - 2),
                ref Unsafe.Add(ref firstPtr, lastOffset - 1),
                ref Unsafe.Add(ref firstPtr, lastOffset));

            var pivotValue = midPtr;

            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset - 3);
            firstPtr = ref Unsafe.Add(ref firstPtr, 3);
            lastOffset = lastOffset - 3;
#else 
            SortThreeIndexes(
                ref Unsafe.Add(ref firstPtr, 1),
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, 2));
            SortThreeIndexes(
                ref Unsafe.Add(ref midPtr, -1),
                ref midPtr,
                ref Unsafe.Add(ref firstPtr, 1));
            SortThreeIndexes(
                ref Unsafe.Add(ref firstPtr, lastOffset - 2),
                ref Unsafe.Add(ref firstPtr, lastOffset),
                ref Unsafe.Add(ref firstPtr, lastOffset - 1));

            SortThreeIndexes(
                ref firstPtr,
                ref midPtr,
                ref Unsafe.Add(ref firstPtr, lastOffset));
            var pivotValue = midPtr;

            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset - 1);
            firstPtr = ref Unsafe.Add(ref firstPtr, 1);
            lastOffset = lastOffset - 1;
#endif
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
        static void MedianOf5(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4) {
            if (default(TOrder).LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (default(TOrder).LessThan(v3, v1)) (v3, v1) = (v1, v3);
            if (default(TOrder).LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (default(TOrder).LessThan(v4, v2)) (v4, v2) = (v2, v4);
            if (default(TOrder).LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (default(TOrder).LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (default(TOrder).LessThan(v4, v1)) (v4, v1) = (v1, v4);
            if (default(TOrder).LessThan(v2, v1)) (v2, v1) = (v1, v2);
            //if (default(TOrder).LessThan(v4, v3)) (v4, v3) = (v3, v4);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void MedianOf7(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4, ref T v5, ref T v6) {
            if (default(TOrder).LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (default(TOrder).LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (default(TOrder).LessThan(v6, v2)) (v6, v2) = (v2, v6);
            if (default(TOrder).LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (default(TOrder).LessThan(v3, v1)) (v3, v1) = (v1, v3);
            if (default(TOrder).LessThan(v6, v4)) (v6, v4) = (v4, v6);
            if (default(TOrder).LessThan(v4, v2)) (v4, v2) = (v2, v4);
            if (default(TOrder).LessThan(v5, v3)) (v5, v3) = (v3, v5);
            if (default(TOrder).LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (default(TOrder).LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (default(TOrder).LessThan(v5, v4)) (v5, v4) = (v4, v5);
            if (default(TOrder).LessThan(v4, v1)) (v4, v1) = (v1, v4);
            if (default(TOrder).LessThan(v6, v3)) (v6, v3) = (v3, v6);
            if (default(TOrder).LessThan(v4, v3)) (v4, v3) = (v3, v4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Sort11Indexes(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4, ref T v5, ref T v6, ref T v7, ref T v8, ref T v9, ref T v10) {
            if (default(TOrder).LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (default(TOrder).LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (default(TOrder).LessThan(v5, v4)) (v5, v4) = (v4, v5);
            if (default(TOrder).LessThan(v7, v6)) (v7, v6) = (v6, v7);
            if (default(TOrder).LessThan(v9, v8)) (v9, v8) = (v8, v9);
            if (default(TOrder).LessThan(v3, v1)) (v3, v1) = (v1, v3);
            if (default(TOrder).LessThan(v7, v5)) (v7, v5) = (v5, v7);
            if (default(TOrder).LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (default(TOrder).LessThan(v6, v4)) (v6, v4) = (v4, v6);
            if (default(TOrder).LessThan(v10, v8)) (v10, v8) = (v8, v10);
            if (default(TOrder).LessThan(v2, v1)) (v2, v1) = (v1, v2);
            if (default(TOrder).LessThan(v6, v5)) (v6, v5) = (v5, v6);
            if (default(TOrder).LessThan(v10, v9)) (v10, v9) = (v9, v10);
            if (default(TOrder).LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (default(TOrder).LessThan(v7, v3)) (v7, v3) = (v3, v7);
            if (default(TOrder).LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (default(TOrder).LessThan(v10, v6)) (v10, v6) = (v6, v10);
            if (default(TOrder).LessThan(v8, v4)) (v8, v4) = (v4, v8);
            if (default(TOrder).LessThan(v9, v5)) (v9, v5) = (v5, v9);
            if (default(TOrder).LessThan(v6, v2)) (v6, v2) = (v2, v6);
            if (default(TOrder).LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (default(TOrder).LessThan(v8, v3)) (v8, v3) = (v3, v8);
            if (default(TOrder).LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (default(TOrder).LessThan(v10, v6)) (v10, v6) = (v6, v10);
            if (default(TOrder).LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (default(TOrder).LessThan(v9, v8)) (v9, v8) = (v8, v9);
            if (default(TOrder).LessThan(v4, v1)) (v4, v1) = (v1, v4);
            if (default(TOrder).LessThan(v10, v7)) (v10, v7) = (v7, v10);
            if (default(TOrder).LessThan(v5, v3)) (v5, v3) = (v3, v5);
            if (default(TOrder).LessThan(v8, v6)) (v8, v6) = (v6, v8);
            if (default(TOrder).LessThan(v4, v2)) (v4, v2) = (v2, v4);
            if (default(TOrder).LessThan(v9, v7)) (v9, v7) = (v7, v9);
            if (default(TOrder).LessThan(v6, v5)) (v6, v5) = (v5, v6);
            if (default(TOrder).LessThan(v4, v3)) (v4, v3) = (v3, v4);
            if (default(TOrder).LessThan(v8, v7)) (v8, v7) = (v7, v8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void MedianOf11(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4, ref T v5, ref T v6, ref T v7, ref T v8, ref T v9, ref T v10) {
            if (default(TOrder).LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (default(TOrder).LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (default(TOrder).LessThan(v5, v4)) (v5, v4) = (v4, v5);
            if (default(TOrder).LessThan(v7, v6)) (v7, v6) = (v6, v7);
            if (default(TOrder).LessThan(v9, v8)) (v9, v8) = (v8, v9);
            if (default(TOrder).LessThan(v3, v1)) (v3, v1) = (v1, v3);
            if (default(TOrder).LessThan(v7, v5)) (v7, v5) = (v5, v7);
            if (default(TOrder).LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (default(TOrder).LessThan(v6, v4)) (v6, v4) = (v4, v6);
            if (default(TOrder).LessThan(v10, v8)) (v10, v8) = (v8, v10);
            if (default(TOrder).LessThan(v2, v1)) (v2, v1) = (v1, v2);
            if (default(TOrder).LessThan(v6, v5)) (v6, v5) = (v5, v6);
            if (default(TOrder).LessThan(v10, v9)) (v10, v9) = (v9, v10);
            if (default(TOrder).LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (default(TOrder).LessThan(v7, v3)) (v7, v3) = (v3, v7);
            if (default(TOrder).LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (default(TOrder).LessThan(v10, v6)) (v10, v6) = (v6, v10);
            if (default(TOrder).LessThan(v8, v4)) (v8, v4) = (v4, v8);
            if (default(TOrder).LessThan(v9, v5)) (v9, v5) = (v5, v9);
            if (default(TOrder).LessThan(v6, v2)) (v6, v2) = (v2, v6);
            if (default(TOrder).LessThan(v8, v3)) (v8, v3) = (v3, v8);
            if (default(TOrder).LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (default(TOrder).LessThan(v10, v6)) (v10, v6) = (v6, v10);
            if (default(TOrder).LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (default(TOrder).LessThan(v9, v8)) (v9, v8) = (v8, v9);
            if (default(TOrder).LessThan(v5, v3)) (v5, v3) = (v3, v5);
            if (default(TOrder).LessThan(v8, v6)) (v8, v6) = (v6, v8);
            if (default(TOrder).LessThan(v6, v5)) (v6, v5) = (v5, v6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SortThreeIndexes(ref T v0, ref T v1, ref T v2) {
            if (default(TOrder).LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (default(TOrder).LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (default(TOrder).LessThan(v2, v1)) (v2, v1) = (v1, v2);
        }

        static void DualPivotQuickSort_Inclusive(T[] array, int firstIdx, int lastIdx) {
            if (lastIdx - firstIdx < 400) {
                if (lastIdx > firstIdx)
                    QuickSort_Inclusive_Small_Unsafe(ref array[firstIdx], lastIdx - firstIdx);
                //InsertionSort_InPlace(array, firstIdx, lastIdx + 1);
            } else {
                // lp means left pivot, and rp means right pivot.
                var (lowPivot, highPivot) = DualPivotPartition(array, firstIdx, lastIdx);
                DualPivotQuickSort_Inclusive(array, firstIdx, lowPivot - 1);
                DualPivotQuickSort_Inclusive(array, lowPivot + 1, highPivot - 1);
                DualPivotQuickSort_Inclusive(array, highPivot + 1, lastIdx);
            }
        }

        static (int lowPivot, int highPivot) DualPivotPartition(T[] arr, int firstIdx, int lastIdx) {
            if (default(TOrder).LessThan(arr[lastIdx], arr[firstIdx]))
                arr.Swap(firstIdx, lastIdx);

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
