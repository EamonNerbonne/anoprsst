using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        static readonly int TopDownInsertionSortBatchSize = 32;
        /*/
        static readonly int TopDownInsertionSortBatchSize = !typeof(T).IsValueType ? 24 
            : Unsafe.SizeOf<T>() > 32 ? 16
            : Unsafe.SizeOf<T>() > 8 ? 32
            : Unsafe.SizeOf<T>() > 4 ? 44
            : 64;
            /**/
        static readonly int BottomUpInsertionSortBatchSize = 24;
        static readonly int QuickSortNoMedianThreshold = 16_384;
        static readonly int MinimalParallelQuickSortBatchSize = 64;

        static OrderedAlgorithms() {

            if (!typeof(T).IsValueType) {
                TopDownInsertionSortBatchSize = 24;
                BottomUpInsertionSortBatchSize = 16;
                QuickSortNoMedianThreshold = 16_000;
                MinimalParallelQuickSortBatchSize = 80;
            } else if (Unsafe.SizeOf<T>() <= 8) {
                TopDownInsertionSortBatchSize = 64;
                BottomUpInsertionSortBatchSize = 40;
                QuickSortNoMedianThreshold = 16_000;
                MinimalParallelQuickSortBatchSize = 80;
            } else {
                TopDownInsertionSortBatchSize = Math.Max(8, 550 / (Unsafe.SizeOf<T>()));
                BottomUpInsertionSortBatchSize = TopDownInsertionSortBatchSize * 2 / 3;
                QuickSortNoMedianThreshold = 16_000;
                MinimalParallelQuickSortBatchSize = 80;
            }
            Console.WriteLine($"{typeof(T)}: {TopDownInsertionSortBatchSize}/{QuickSortNoMedianThreshold}/{MinimalParallelQuickSortBatchSize}");
        }

        public static SortAlgorithmBench<T, TOrder> BencherFor(T[] arr, int TimingTrials, int IterationsPerTrial) => new SortAlgorithmBench<T, TOrder>(arr, TimingTrials, IterationsPerTrial);
        protected OrderedAlgorithms() => throw new NotSupportedException("allow subclassing so you can fix type parameters, but not instantiation.");
        public static void TopDownMergeSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx)) {
                ref var firstItemsPtr = ref array[0];
                ref var lastItemsPtr = ref Unsafe.Add(ref firstItemsPtr, endIdx - 1);
                if (endIdx <= TopDownInsertionSortBatchSize) {
                    InsertionSort_InPlace_Unsafe_Inclusive(ref firstItemsPtr, ref lastItemsPtr);
                    return;
                }
                var scratch = new T[endIdx];
                ref var firstScratchPtr = ref scratch[0];
                ref var lastScratchPtr = ref Unsafe.Add(ref firstScratchPtr, endIdx - 1);
                TopDownSplitMerge_toItems(ref firstItemsPtr, ref lastItemsPtr, ref firstScratchPtr, ref lastScratchPtr, endIdx);
            }
        }

        public static void BottomUpMergeSort(T[] array, int endIdx) {
            if (Helpers.NeedsSort_WithBoundsCheck(array, endIdx))
                BottomUpMergeSort(array, new T[endIdx], endIdx);
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
            if (lastIdx - firstIdx < MinimalParallelQuickSortBatchSize << 2) {
                QuickSort_Inclusive_Small_Unsafe(ref array[firstIdx], lastIdx - firstIdx);
                return;
            }
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

        public static void AltTopDownMergeSort(T[] items, int n) {
            if (!Helpers.NeedsSort_WithBoundsCheck(items, n))
                return;

            if (n < TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace_Unsafe_Inclusive(ref items[0], ref items[n - 1]);
                return;
            }

            var mergeCount = 2;
            for (var s = (uint)TopDownInsertionSortBatchSize << 2; s < (uint)n; s <<= 2)
                mergeCount += 2;

            ref var itemsPtr = ref items[0];
            var scratch = new T[n];
            ref var scratchPtr = ref scratch[0];

            AltTopDownSplitMerge_Unsafe(ref itemsPtr, ref Unsafe.Add(ref itemsPtr, n - 1), ref scratchPtr, ref Unsafe.Add(ref scratchPtr, n - 1), n, mergeCount);
            //AltTopDownSplitMerge(items, 0, n, scratch, mergeCount);
        }

        static void AltTopDownSplitMerge_Unsafe(ref T firstItemsPtr, ref T lastItemsPtr, ref T firstScratchPtr, ref T lastScratchPtr, int length, int mergeCount) {
            var firstHalfLength = length >> 1;
            var secondHalfLength = length - firstHalfLength;
            ref var middleItemsPtr = ref Unsafe.Add(ref firstItemsPtr, firstHalfLength);
            ref var middleScratchPtr = ref Unsafe.Add(ref firstScratchPtr, firstHalfLength);
            if (mergeCount == 1) {
                InsertionSort_InPlace_Unsafe_Inclusive(ref middleScratchPtr, ref lastScratchPtr);
                InsertionSort_InPlace_Unsafe_Inclusive(ref firstScratchPtr, ref Unsafe.Subtract(ref middleScratchPtr, 1));
            } else {
                AltTopDownSplitMerge_Unsafe(ref middleScratchPtr, ref lastScratchPtr, ref middleItemsPtr, ref lastItemsPtr, secondHalfLength, mergeCount - 1);
                AltTopDownSplitMerge_Unsafe(ref firstScratchPtr, ref Unsafe.Subtract(ref middleScratchPtr, 1), ref firstItemsPtr, ref Unsafe.Subtract(ref middleItemsPtr, 1), firstHalfLength, mergeCount - 1);
            }
            Merge_Unsafe(ref firstScratchPtr, ref middleScratchPtr, ref lastScratchPtr, ref firstItemsPtr);
        }

        static void TopDownSplitMerge_toItems(ref T firstItemsPtr, ref T lastItemsPtr, ref T firstScratchPtr, ref T lastScratchPtr, int length) {
            var firstHalfLength = length >> 1;
            TopDownSplitMerge_toScratch(ref Unsafe.Add(ref firstItemsPtr, firstHalfLength), ref lastItemsPtr, ref Unsafe.Add(ref firstScratchPtr, firstHalfLength), ref lastScratchPtr, length - firstHalfLength);
            TopDownSplitMerge_toScratch(ref firstItemsPtr, ref Unsafe.Add(ref firstItemsPtr, firstHalfLength - 1), ref firstScratchPtr, ref Unsafe.Add(ref firstScratchPtr, firstHalfLength - 1), firstHalfLength);
            Merge_Unsafe(ref firstScratchPtr, ref Unsafe.Add(ref firstScratchPtr, firstHalfLength), ref lastScratchPtr, ref firstItemsPtr);
        }

        static void TopDownSplitMerge_toScratch(ref T firstItemsPtr, ref T lastItemsPtr, ref T firstScratchPtr, ref T lastScratchPtr, int length) {
            if (length <= TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace_Unsafe_Inclusive(ref firstItemsPtr, ref lastItemsPtr);
                CopyInclusiveRefRange_Unsafe(ref firstItemsPtr, ref lastItemsPtr, ref firstScratchPtr);
                return;
            }

            var firstHalfLength = length >> 1;
            var secondHalfLength = length - firstHalfLength;
            ref var middleItemsPtr = ref Unsafe.Add(ref firstItemsPtr, firstHalfLength);
            ref var middleScratchPtr = ref Unsafe.Add(ref firstScratchPtr, firstHalfLength);

            if (firstHalfLength < TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace_Unsafe_Inclusive(ref firstItemsPtr, ref Unsafe.Subtract(ref middleItemsPtr, 1));
                InsertionSort_InPlace_Unsafe_Inclusive(ref middleItemsPtr, ref lastItemsPtr);
            } else {
                TopDownSplitMerge_toItems(ref middleItemsPtr, ref lastItemsPtr, ref middleScratchPtr, ref lastScratchPtr, secondHalfLength);
                TopDownSplitMerge_toItems(ref firstItemsPtr, ref Unsafe.Subtract(ref middleItemsPtr, 1), ref firstScratchPtr, ref Unsafe.Subtract(ref middleScratchPtr, 1), firstHalfLength);
            }

            Merge_Unsafe(ref firstItemsPtr, ref middleItemsPtr, ref lastItemsPtr, ref firstScratchPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Merge(T[] source, int firstIdx, int middleIdx, int endIdx, T[] target) {
            Merge_Unsafe(ref source[firstIdx], ref source[middleIdx], ref source[endIdx - 1], ref target[firstIdx]);
        }

        static void Merge_Unsafe(ref T readPtrA, ref T readPtrB, ref T lastPtrB, ref T writePtr) {
            ref var lastPtrA = ref Unsafe.Subtract(ref readPtrB, 1);
            while (true) {
                if (!default(TOrder).LessThan(readPtrB, readPtrA)) {
                    writePtr = readPtrA;
                    writePtr = ref Unsafe.Add(ref writePtr, 1);
                    if (Unsafe.IsAddressLessThan(ref readPtrA, ref lastPtrA)) {
                        readPtrA = ref Unsafe.Add(ref readPtrA, 1);
                    } else {
                        while (true) {
                            writePtr = readPtrB;
                            if (!Unsafe.AreSame(ref readPtrB, ref lastPtrB)) {
                                readPtrB = ref Unsafe.Add(ref readPtrB, 1);
                                writePtr = ref Unsafe.Add(ref writePtr, 1);
                                continue;
                            }
                            return;
                        }
                    }
                } else {
                    writePtr = readPtrB;
                    writePtr = ref Unsafe.Add(ref writePtr, 1);
                    if (Unsafe.IsAddressLessThan(ref readPtrB, ref lastPtrB)) {
                        readPtrB = ref Unsafe.Add(ref readPtrB, 1);
                    } else {
                        while (true) {
                            writePtr = readPtrA;
                            if (!Unsafe.AreSame(ref readPtrA, ref lastPtrA)) {
                                writePtr = ref Unsafe.Add(ref writePtr, 1);
                                readPtrA = ref Unsafe.Add(ref readPtrA, 1);
                                continue;
                            }
                            return;
                        }
                    }
                }
            }
        }

        static void BottomUpMergeSort(T[] targetArr, T[] scratchArr, int n) {
            ref var targetPtr = ref targetArr[0];
            ref var scratchPtr = ref scratchArr[0];

            var mergeCount = 0;
            int defaultBatchSize = TopDownInsertionSortBatchSize & ~1;
            for (var s = defaultBatchSize; s < n; s <<= 1)
                mergeCount++;

            var width = (mergeCount & 1) == 0 ? defaultBatchSize : (defaultBatchSize >> 1);
            var batchesSortedUpto = 0;

            while (true)
                if (batchesSortedUpto + width <= n) {
                    InsertionSort_InPlace_Unsafe_Inclusive(ref Unsafe.Add(ref targetPtr, batchesSortedUpto), ref Unsafe.Add(ref targetPtr, batchesSortedUpto + width - 1));
                    batchesSortedUpto += width;
                } else {
                    if (batchesSortedUpto < n - 1)
                        InsertionSort_InPlace_Unsafe_Inclusive(ref Unsafe.Add(ref targetPtr, batchesSortedUpto), ref Unsafe.Add(ref targetPtr, n - 1));
                    break;
                }

            while (width < n) {
                var firstIdx = 0;
                var middleIdx = width;
                var endIdx = width = width << 1;
                while (endIdx <= n) {
                    Merge_Unsafe(ref Unsafe.Add(ref targetPtr, firstIdx), ref Unsafe.Add(ref targetPtr, middleIdx), ref Unsafe.Add(ref targetPtr, endIdx - 1), ref Unsafe.Add(ref scratchPtr, firstIdx));
                    firstIdx += width;
                    middleIdx += width;
                    endIdx += width;
                }

                if (middleIdx < n)
                    Merge_Unsafe(ref Unsafe.Add(ref targetPtr, firstIdx), ref Unsafe.Add(ref targetPtr, middleIdx), ref Unsafe.Add(ref targetPtr, n - 1), ref Unsafe.Add(ref scratchPtr, firstIdx));
                else if (firstIdx < n)
                    CopyInclusiveRefRange_Unsafe(ref Unsafe.Add(ref targetPtr, firstIdx), ref Unsafe.Add(ref targetPtr, n - 1), ref Unsafe.Add(ref scratchPtr, firstIdx));
                ref var tmp = ref scratchPtr;
                scratchPtr = ref targetPtr;
                targetPtr = ref tmp;
            }
        }

        static void CopyInclusiveRefRange_Unsafe(ref T readPtr, ref T readUntil, ref T writePtr) {
            while (true) {
                writePtr = readPtr;
                if (Unsafe.AreSame(ref readPtr, ref readUntil)) {
                    break;
                }
                readPtr = ref Unsafe.Add(ref readPtr, 1);
                writePtr = ref Unsafe.Add(ref writePtr, 1);
            }
        }
    }
}
