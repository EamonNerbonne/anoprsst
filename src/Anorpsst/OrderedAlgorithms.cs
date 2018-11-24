using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Anoprsst
{
    public interface IOrdering<in T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool LessThan(T a, T b);
    }

    public abstract class OrderedAlgorithms<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        public static readonly int TopDownInsertionSortBatchSize;
        public static readonly int BottomUpInsertionSortBatchSize;
        public static readonly int QuickSortFastMedianThreshold;
        public static readonly int MinimalParallelQuickSortBatchSize;

        static OrderedAlgorithms()
        {
            if (!typeof(T).IsValueType) {
                TopDownInsertionSortBatchSize = 24;
                BottomUpInsertionSortBatchSize = 16;
                QuickSortFastMedianThreshold = 10_000;
                MinimalParallelQuickSortBatchSize = 1500;
            } else if (Unsafe.SizeOf<T>() <= 8) {
                TopDownInsertionSortBatchSize = 64;
                BottomUpInsertionSortBatchSize = 40;
                QuickSortFastMedianThreshold = 13_000;
                MinimalParallelQuickSortBatchSize = 1100;
            } else {
                TopDownInsertionSortBatchSize = Math.Max(8, 550 / Unsafe.SizeOf<T>());
                BottomUpInsertionSortBatchSize = TopDownInsertionSortBatchSize * 2 / 3;
                QuickSortFastMedianThreshold = 16_000;
                MinimalParallelQuickSortBatchSize = 1000;
            }
        }

        protected OrderedAlgorithms() => throw new NotSupportedException("allow subclassing so you can fix type parameters, but not instantiation.");

        public static void TopDownMergeSort(Span<T> array, TOrder ordering)
        {
            var endIdx = array.Length;
            if (array.Length > 1) {
                ref var firstItemsPtr = ref array[0];
                ref var lastItemsPtr = ref Unsafe.Add(ref firstItemsPtr, endIdx - 1);
                if (endIdx <= TopDownInsertionSortBatchSize) {
                    InsertionSort_InPlace_Unsafe_Inclusive(ref firstItemsPtr, ref lastItemsPtr, ordering);
                    return;
                }

                var scratch = memPool.Rent(endIdx);
                ref var firstScratchPtr = ref scratch[0];
                ref var lastScratchPtr = ref Unsafe.Add(ref firstScratchPtr, endIdx - 1);
                TopDownSplitMerge_toItems(ref firstItemsPtr, ref lastItemsPtr, ref firstScratchPtr, ref lastScratchPtr, endIdx, ordering);
                Array.Clear(scratch, 0, endIdx);
                memPool.Return(scratch);
            }
        }

        public static void BottomUpMergeSort(Span<T> array, TOrder ordering)
        {
            if (array.Length > 1) {
                var scratch = memPool.Rent(array.Length);

                BottomUpMergeSort(array, new T[array.Length], ordering);
                Array.Clear(scratch, 0, array.Length);
                memPool.Return(scratch);
            }
        }

        public static void QuickSort(Span<T> array, TOrder ordering)
        {
            if (array.Length > 1)
                QuickSort_Inclusive_Unsafe(ref array[0], array.Length - 1, ordering);
        }

        public static void DualPivotQuickSort(Span<T> array, TOrder ordering)
        {
            if (array.Length > 1)
                DualPivotQuickSort_Inclusive(ref array[0], array.Length - 1,ordering);
        }

        public static unsafe void ParallelQuickSort(Span<T> array, TOrder ordering)
        {
            var length = array.Length;
            if (length < MinimalParallelQuickSortBatchSize << 1) {
                if (length > 1)
                    QuickSort_Inclusive_Small_Unsafe(ref array[0], array.Length - 1, ordering);
                return;
            }

            var countdownEvent = new CountdownEvent(1);
            ref var byteRef = ref Unsafe.As<T, byte>(ref array.GetPinnableReference());
            fixed (byte* ptr = &byteRef) {
                new QuickSort_Inclusive_ParallelArgs {
                    CountdownEvent = countdownEvent,
                    Ptr = ptr,
                    SplitAt = Math.Max(length >> ParallelismConstants.ParallelSplitScale, MinimalParallelQuickSortBatchSize),
                    LastIdx = length - 1,
                    Ordering = ordering,
                }.Impl();
                countdownEvent.Wait();
            }
        }

        sealed unsafe class QuickSort_Inclusive_ParallelArgs
        {
            public CountdownEvent CountdownEvent;
            public void* Ptr;
            public int SplitAt;
            public int LastIdx;
            static readonly WaitCallback QuickSort_Inclusive_Par2_callback = o => ((QuickSort_Inclusive_ParallelArgs)o).Impl();
            public TOrder Ordering;

            public void Impl()
            {
                ref var firstRef = ref Unsafe.AsRef<T>(Ptr);
                var lastIdx = LastIdx;
                var splitAt = SplitAt;
                var countdownEvent = CountdownEvent;
                var ordering = Ordering;
                while (lastIdx >= splitAt) {
                    countdownEvent.AddCount(1);
                    var pivot = PartitionWithMedian_Unsafe(ref firstRef, lastIdx, ordering);
                    ThreadPool.UnsafeQueueUserWorkItem(
                        QuickSort_Inclusive_Par2_callback,
                        new QuickSort_Inclusive_ParallelArgs {
                            CountdownEvent = countdownEvent,
                            Ptr = Unsafe.AsPointer(ref Unsafe.Add(ref firstRef, pivot + 1)),
                            SplitAt = splitAt,
                            LastIdx = lastIdx - (pivot + 1),
                        });
                    lastIdx = pivot; //effectively QuickSort_Inclusive(array, firstIdx, pivot);
                }

                QuickSort_Inclusive_Unsafe(ref firstRef, lastIdx, ordering);
                countdownEvent.Signal();
            }
        }

        static void QuickSort_Inclusive_Unsafe(ref T ptr, int lastOffset, TOrder ordering)
        {
            while (lastOffset >= QuickSortFastMedianThreshold) {
                var pivot = PartitionWithMedian_Unsafe(ref ptr, lastOffset, ordering);
                QuickSort_Inclusive_Unsafe(ref Unsafe.Add(ref ptr, pivot + 1), lastOffset - (pivot + 1), ordering);
                lastOffset = pivot; //QuickSort_Inclusive_Unsafe(ref ptr, pivot);
            }

            QuickSort_Inclusive_Small_Unsafe(ref ptr, lastOffset, ordering);
        }

        /// <summary>
        ///     precondition:memory in range [firstPtr, firstPtr+lastOffset] can be mutated, also implying lastOffset >= 0
        /// </summary>
        static void QuickSort_Inclusive_Small_Unsafe(ref T firstPtr, int lastOffset, TOrder ordering)
        {
            while (lastOffset >= TopDownInsertionSortBatchSize) {
                //invariant: lastOffset >= 1
                var pivotIdx = Partition_Unsafe(ref firstPtr, lastOffset, ordering);
                //invariant: pivotIdx in [0, lastOffset-1]
                QuickSort_Inclusive_Small_Unsafe(ref Unsafe.Add(ref firstPtr, pivotIdx + 1), lastOffset - (pivotIdx + 1), ordering);
                lastOffset = pivotIdx; //QuickSort(array, firstIdx, pivot);
            }

            InsertionSort_InPlace_Unsafe_Inclusive(ref firstPtr, ref Unsafe.Add(ref firstPtr, lastOffset), ordering);
        }

        /// <summary>
        ///     Precondition: memory in range [firstPtr, firstPtr+lastOffset] can be mutated, and lastOffset >= 1
        ///     Postcondition: returnvalue in range [0, lastOffset-1]
        /// </summary>
        public static int Partition_Unsafe(ref T firstPtr, int lastOffset, TOrder ordering)
        {
            //precondition: 1 <= lastOffset
            //so midpoint != lastOffset
#if true
            ref var midPtr = ref Unsafe.Add(ref firstPtr, lastOffset >> 1);
            SortThreeIndexes(ref firstPtr, ref midPtr, ref Unsafe.Add(ref firstPtr, lastOffset), ordering);
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
                while (ordering.LessThan(firstPtr, pivotValue))
                    firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                //on the first iteration, the following loop either succeeds at least once (decrementing lastOffset), or it bails immediately
                while (ordering.LessThan(pivotValue, lastPtr)) {
                    lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
                    lastOffset--;
                }

                //on the first iteration, either lastOffset has been decremented, OR ref lastPtr > ref firstPtr; so if we break here, then lastOffset was decremented
                if (!Unsafe.IsAddressGreaterThan(ref lastPtr, ref firstPtr))
                    break; // TODO: Workaround for https://github.com/dotnet/coreclr/issues/9692
                (firstPtr, lastPtr) = (lastPtr, firstPtr);
                firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
                //on the first iteration lastOffset was decremented at least once.
                lastOffset--;
            }

            return lastOffset;
        }

        static int PartitionWithMedian_Unsafe(ref T firstPtr, int lastOffset, TOrder ordering)
        {
#if false
//InsertionSort_InPlace_Unsafe_Inclusive(ref Unsafe.Add(ref firstPtr, midpoint-3),ref Unsafe.Add(ref firstPtr, midpoint+3));
//var pivotValue = midPtr;
//ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
            MedianOf5(
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, 1),
                ref Unsafe.Add(ref firstPtr, lastOffset >> 1),
                ref Unsafe.Add(ref firstPtr, lastOffset - 1),
                ref Unsafe.Add(ref firstPtr, lastOffset));

            var pivotValue = Unsafe.Add(ref firstPtr, lastOffset >> 1);
            lastOffset = lastOffset - 2;
            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
            firstPtr = ref Unsafe.Add(ref firstPtr, 2);
#elif true
            MedianOf7(
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, 1),
                ref Unsafe.Add(ref firstPtr, 2),
                ref Unsafe.Add(ref firstPtr, lastOffset >> 1),
                ref Unsafe.Add(ref firstPtr, lastOffset - 2),
                ref Unsafe.Add(ref firstPtr, lastOffset - 1),
                ref Unsafe.Add(ref firstPtr, lastOffset),
                ordering);

            var pivotValue = Unsafe.Add(ref firstPtr, lastOffset >> 1);
            lastOffset = lastOffset - 3;
            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
            firstPtr = ref Unsafe.Add(ref firstPtr, 3);
#else
            SortThreeIndexes(
                ref Unsafe.Add(ref firstPtr, 1),
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, 2));
            SortThreeIndexes(
                ref Unsafe.Add(ref firstPtr, (lastOffset >> 1)-1),
                ref Unsafe.Add(ref firstPtr, lastOffset >> 1),
                ref Unsafe.Add(ref firstPtr, (lastOffset >> 1)+1));
            SortThreeIndexes(
                ref Unsafe.Add(ref firstPtr, lastOffset - 2),
                ref Unsafe.Add(ref firstPtr, lastOffset),
                ref Unsafe.Add(ref firstPtr, lastOffset - 1));
            SortThreeIndexes(
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, lastOffset >> 1),
                ref Unsafe.Add(ref firstPtr, lastOffset));

            var pivotValue = Unsafe.Add(ref firstPtr, lastOffset >> 1);
            lastOffset = lastOffset - 1;
            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
            firstPtr = ref Unsafe.Add(ref firstPtr, 1);
#endif

            while (true) {
                while (ordering.LessThan(firstPtr, pivotValue))
                    firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                while (ordering.LessThan(pivotValue, lastPtr)) {
                    lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
                    lastOffset--;
                }

                if (!Unsafe.IsAddressGreaterThan(ref lastPtr, ref firstPtr))
                    break; // TODO: Workaround for https://github.com/dotnet/coreclr/issues/9692
                lastOffset--;
                (firstPtr, lastPtr) = (lastPtr, firstPtr);
                firstPtr = ref Unsafe.Add(ref firstPtr, 1);
                lastPtr = ref Unsafe.Subtract(ref lastPtr, 1);
            }

            return lastOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void MedianOf5(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4, TOrder ordering)
        {
            if (ordering.LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (ordering.LessThan(v3, v1)) (v3, v1) = (v1, v3);
            if (ordering.LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (ordering.LessThan(v4, v2)) (v4, v2) = (v2, v4);
            if (ordering.LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (ordering.LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (ordering.LessThan(v4, v1)) (v4, v1) = (v1, v4);
            if (ordering.LessThan(v2, v1)) (v2, v1) = (v1, v2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SortOf5(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4, TOrder ordering)
        {
            MedianOf5(ref v0, ref v1, ref v2, ref v3, ref v4, ordering);
            if (ordering.LessThan(v4, v3)) (v4, v3) = (v3, v4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void MedianOf7(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4, ref T v5, ref T v6, TOrder ordering)
        {
            if (ordering.LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (ordering.LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (ordering.LessThan(v6, v2)) (v6, v2) = (v2, v6);
            if (ordering.LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (ordering.LessThan(v3, v1)) (v3, v1) = (v1, v3);
            if (ordering.LessThan(v6, v4)) (v6, v4) = (v4, v6);
            if (ordering.LessThan(v4, v2)) (v4, v2) = (v2, v4);
            if (ordering.LessThan(v5, v3)) (v5, v3) = (v3, v5);
            if (ordering.LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (ordering.LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (ordering.LessThan(v5, v4)) (v5, v4) = (v4, v5);
            if (ordering.LessThan(v4, v1)) (v4, v1) = (v1, v4);
            if (ordering.LessThan(v6, v3)) (v6, v3) = (v3, v6);
            if (ordering.LessThan(v4, v3)) (v4, v3) = (v3, v4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once UnusedMember.Local
        static void Sort11Indexes(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4, ref T v5, ref T v6, ref T v7, ref T v8, ref T v9, ref T v10, TOrder ordering)
        {
            if (ordering.LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (ordering.LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (ordering.LessThan(v5, v4)) (v5, v4) = (v4, v5);
            if (ordering.LessThan(v7, v6)) (v7, v6) = (v6, v7);
            if (ordering.LessThan(v9, v8)) (v9, v8) = (v8, v9);
            if (ordering.LessThan(v3, v1)) (v3, v1) = (v1, v3);
            if (ordering.LessThan(v7, v5)) (v7, v5) = (v5, v7);
            if (ordering.LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (ordering.LessThan(v6, v4)) (v6, v4) = (v4, v6);
            if (ordering.LessThan(v10, v8)) (v10, v8) = (v8, v10);
            if (ordering.LessThan(v2, v1)) (v2, v1) = (v1, v2);
            if (ordering.LessThan(v6, v5)) (v6, v5) = (v5, v6);
            if (ordering.LessThan(v10, v9)) (v10, v9) = (v9, v10);
            if (ordering.LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (ordering.LessThan(v7, v3)) (v7, v3) = (v3, v7);
            if (ordering.LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (ordering.LessThan(v10, v6)) (v10, v6) = (v6, v10);
            if (ordering.LessThan(v8, v4)) (v8, v4) = (v4, v8);
            if (ordering.LessThan(v9, v5)) (v9, v5) = (v5, v9);
            if (ordering.LessThan(v6, v2)) (v6, v2) = (v2, v6);
            if (ordering.LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (ordering.LessThan(v8, v3)) (v8, v3) = (v3, v8);
            if (ordering.LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (ordering.LessThan(v10, v6)) (v10, v6) = (v6, v10);
            if (ordering.LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (ordering.LessThan(v9, v8)) (v9, v8) = (v8, v9);
            if (ordering.LessThan(v4, v1)) (v4, v1) = (v1, v4);
            if (ordering.LessThan(v10, v7)) (v10, v7) = (v7, v10);
            if (ordering.LessThan(v5, v3)) (v5, v3) = (v3, v5);
            if (ordering.LessThan(v8, v6)) (v8, v6) = (v6, v8);
            if (ordering.LessThan(v4, v2)) (v4, v2) = (v2, v4);
            if (ordering.LessThan(v9, v7)) (v9, v7) = (v7, v9);
            if (ordering.LessThan(v6, v5)) (v6, v5) = (v5, v6);
            if (ordering.LessThan(v4, v3)) (v4, v3) = (v3, v4);
            if (ordering.LessThan(v8, v7)) (v8, v7) = (v7, v8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once UnusedMember.Local
        static void MedianOf11(ref T v0, ref T v1, ref T v2, ref T v3, ref T v4, ref T v5, ref T v6, ref T v7, ref T v8, ref T v9, ref T v10, TOrder ordering)
        {
            if (ordering.LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (ordering.LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (ordering.LessThan(v5, v4)) (v5, v4) = (v4, v5);
            if (ordering.LessThan(v7, v6)) (v7, v6) = (v6, v7);
            if (ordering.LessThan(v9, v8)) (v9, v8) = (v8, v9);
            if (ordering.LessThan(v3, v1)) (v3, v1) = (v1, v3);
            if (ordering.LessThan(v7, v5)) (v7, v5) = (v5, v7);
            if (ordering.LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (ordering.LessThan(v6, v4)) (v6, v4) = (v4, v6);
            if (ordering.LessThan(v10, v8)) (v10, v8) = (v8, v10);
            if (ordering.LessThan(v2, v1)) (v2, v1) = (v1, v2);
            if (ordering.LessThan(v6, v5)) (v6, v5) = (v5, v6);
            if (ordering.LessThan(v10, v9)) (v10, v9) = (v9, v10);
            if (ordering.LessThan(v4, v0)) (v4, v0) = (v0, v4);
            if (ordering.LessThan(v7, v3)) (v7, v3) = (v3, v7);
            if (ordering.LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (ordering.LessThan(v10, v6)) (v10, v6) = (v6, v10);
            if (ordering.LessThan(v8, v4)) (v8, v4) = (v4, v8);
            if (ordering.LessThan(v9, v5)) (v9, v5) = (v5, v9);
            if (ordering.LessThan(v6, v2)) (v6, v2) = (v2, v6);
            if (ordering.LessThan(v8, v3)) (v8, v3) = (v3, v8);
            if (ordering.LessThan(v5, v1)) (v5, v1) = (v1, v5);
            if (ordering.LessThan(v10, v6)) (v10, v6) = (v6, v10);
            if (ordering.LessThan(v3, v2)) (v3, v2) = (v2, v3);
            if (ordering.LessThan(v9, v8)) (v9, v8) = (v8, v9);
            if (ordering.LessThan(v5, v3)) (v5, v3) = (v3, v5);
            if (ordering.LessThan(v8, v6)) (v8, v6) = (v6, v8);
            if (ordering.LessThan(v6, v5)) (v6, v5) = (v5, v6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SortThreeIndexes(ref T v0, ref T v1, ref T v2, TOrder ordering)
        {
            if (ordering.LessThan(v2, v0)) (v2, v0) = (v0, v2);
            if (ordering.LessThan(v1, v0)) (v1, v0) = (v0, v1);
            if (ordering.LessThan(v2, v1)) (v2, v1) = (v1, v2);
        }

        static void DualPivotQuickSort_Inclusive(ref T firstPtr, int lastOffset, TOrder ordering)
        {
            if (lastOffset < 400) {
                QuickSort_Inclusive_Small_Unsafe(ref firstPtr, lastOffset, ordering);
                //InsertionSort_InPlace(array, firstIdx, lastIdx + 1);
            } else {
                // lp means left pivot, and rp means right pivot.
                var (lowPivotO, highPivotO) = DualPivotPartition(ref firstPtr, lastOffset, ordering);
                if (lowPivotO - 1 >= 1)
                    DualPivotQuickSort_Inclusive(ref firstPtr, lowPivotO - 1, ordering);
                if (highPivotO - lowPivotO - 2 >= 1)
                    DualPivotQuickSort_Inclusive(ref Unsafe.Add(ref firstPtr, lowPivotO + 1), highPivotO - lowPivotO - 2, ordering);
                if (lastOffset - (highPivotO + 1) >= 1)
                    DualPivotQuickSort_Inclusive(ref Unsafe.Add(ref firstPtr, highPivotO + 1), lastOffset - (highPivotO + 1), ordering);
            }
        }

        static (int lowPivot, int highPivot) DualPivotPartition(ref T firstPtr, int lastOffset, TOrder ordering)
        {
            ref var lastPtr = ref Unsafe.Add(ref firstPtr, lastOffset);
            //*
            var midpoint = lastOffset >> 1;
            var quarter = lastOffset >> 2;

            SortOf5(
                ref Unsafe.Add(ref firstPtr, quarter),
                ref firstPtr,
                ref Unsafe.Add(ref firstPtr, midpoint),
                ref lastPtr,
                ref Unsafe.Subtract(ref lastPtr, quarter),
                ordering);
            /*/
            if (ordering.LessThan(lastPtr, firstPtr))
                (firstPtr, lastPtr) = (lastPtr, firstPtr);
            /**/
            ref var lowPivot = ref Unsafe.Add(ref firstPtr, 1);
            var lowPivotIdx = 1;
            ref var highPivot = ref Unsafe.Subtract(ref lastPtr, 1);
            var highPivotIdx = lastOffset - 1;
            ref var betweenPivots = ref lowPivot;
            var lowPivotValue = firstPtr;
            var highPivotValue = lastPtr;
            while (!Unsafe.IsAddressGreaterThan(ref betweenPivots, ref highPivot)) {
                if (ordering.LessThan(betweenPivots, lowPivotValue)) {
                    (betweenPivots, lowPivot) = (lowPivot, betweenPivots);
                    lowPivot = ref Unsafe.Add(ref lowPivot, 1);
                    lowPivotIdx++;
                } else if (!ordering.LessThan(betweenPivots, highPivotValue)) {
                    while (ordering.LessThan(highPivotValue, highPivot) && Unsafe.IsAddressLessThan(ref betweenPivots, ref highPivot)) {
                        highPivot = ref Unsafe.Subtract(ref highPivot, 1);
                        highPivotIdx--;
                    }

                    (betweenPivots, highPivot) = (highPivot, betweenPivots);
                    highPivot = ref Unsafe.Subtract(ref highPivot, 1);
                    highPivotIdx--;

                    if (ordering.LessThan(betweenPivots, lowPivotValue)) {
                        (betweenPivots, lowPivot) = (lowPivot, betweenPivots);
                        lowPivot = ref Unsafe.Add(ref lowPivot, 1);
                        lowPivotIdx++;
                    }
                }

                betweenPivots = ref Unsafe.Add(ref betweenPivots, 1);
            }

            lowPivot = ref Unsafe.Subtract(ref lowPivot, 1);
            lowPivotIdx--;
            highPivot = ref Unsafe.Add(ref highPivot, 1);
            highPivotIdx++;

            // bring pivots to their appropriate positions.
            (firstPtr, lowPivot) = (lowPivot, firstPtr);
            (lastPtr, highPivot) = (highPivot, lastPtr);

            return (lowPivotIdx, highPivotIdx);
        }

        // ReSharper disable once UnusedMember.Local
        static void BitonicSort(int logn, T[] array, int firstIdx, TOrder ordering)
        {
            var endIdx = firstIdx + (1 << logn);
            var mask = (1 << logn) - 1;

            for (var i = 0; i < logn; i++)
            for (var j = 0; j <= i; j++) {
                var bitMask = 1 << i - j;

                for (var idx = firstIdx; idx < endIdx; idx++) {
                    var up = ((idx & mask) >> i & 2) == 0;

                    if ((idx & bitMask) == 0 && ordering.LessThan(array[idx | bitMask], array[idx]) == up) {
                        var t = array[idx];
                        array[idx] = array[idx | bitMask];
                        array[idx | bitMask] = t;
                    }
                }
            }
        }

        static void InsertionSort_InPlace_Unsafe_Inclusive(ref T firstPtr, ref T lastPtr, TOrder ordering)
        {
            if (Unsafe.AreSame(ref firstPtr, ref lastPtr))
                return;
            ref var writePtr = ref firstPtr;
            ref var readPtr = ref Unsafe.Add(ref firstPtr, 1);
            while (true) { //readIdx < idxEnd
                var readValue = readPtr;
                if (ordering.LessThan(readValue, writePtr)) {
                    while (true) {
                        //ordering.LessThan(readValue, writePtr) holds
                        Unsafe.Add(ref writePtr, 1) = writePtr;
                        if (Unsafe.AreSame(ref writePtr, ref firstPtr))
                            break;
                        writePtr = ref Unsafe.Subtract(ref writePtr, 1);
                        if (!ordering.LessThan(readValue, writePtr)) {
                            writePtr = ref Unsafe.Add(ref writePtr, 1);
                            break;
                        }

                        //ordering.LessThan(readValue, writePtr) holds
                    }

                    writePtr = readValue;
                }

                if (Unsafe.AreSame(ref readPtr, ref lastPtr))
                    break;
                writePtr = ref readPtr;
                readPtr = ref Unsafe.Add(ref readPtr, 1);
            }
        }

        static readonly ArrayPool<T> memPool = ArrayPool<T>.Shared;

        public static void AltTopDownMergeSort(Span<T> items, TOrder ordering)
        {
            if (!(items.Length > 1))
                return;

            var n = items.Length;
            if (n < TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace_Unsafe_Inclusive(ref items[0], ref items[n - 1], ordering);
                return;
            }
#if true
            var mergeCount = 2;
            for (var s = (uint)TopDownInsertionSortBatchSize << 2; s < (uint)n; s <<= 2)
                mergeCount += 2;

            ref var itemsPtr = ref items[0];
            var scratch = memPool.Rent(n);
            ref var scratchPtr = ref scratch[0];

            AltTopDownSplitMerge_Unsafe(ref itemsPtr, ref Unsafe.Add(ref itemsPtr, n - 1), ref scratchPtr, ref Unsafe.Add(ref scratchPtr, n - 1), n, mergeCount, ordering);
            Array.Clear(scratch, 0, n);
            memPool.Return(scratch);
#else
            var mergeCount = 1;
            for (var s = (uint)TopDownInsertionSortBatchSize << 1; s < (uint)n; s <<= 1)
                mergeCount += 1;

            ref var itemsPtr = ref items[0];
            var scratch = new T[n];
            ref var scratchPtr = ref scratch[0];

            if((mergeCount&1) == 0)
                AltTopDownSplitMerge_Unsafe(ref itemsPtr, ref Unsafe.Add(ref itemsPtr, n - 1), ref scratchPtr, ref Unsafe.Add(ref scratchPtr, n - 1), n, mergeCount);
            else {
                AltTopDownSplitMerge_Unsafe(ref scratchPtr, ref Unsafe.Add(ref scratchPtr, n - 1), ref itemsPtr, ref Unsafe.Add(ref itemsPtr, n - 1), n, mergeCount);
                CopyInclusiveRefRange_Unsafe(ref scratchPtr, ref Unsafe.Add(ref scratchPtr, n - 1), ref itemsPtr);
            }
#endif
        }

        static void AltTopDownSplitMerge_Unsafe(
            ref T firstItemsPtr,
            ref T lastItemsPtr,
            ref T firstScratchPtr,
            ref T lastScratchPtr,
            int length,
            int mergeCount,
            TOrder ordering)
        {
            var firstHalfLength = length >> 1;
            var secondHalfLength = length - firstHalfLength;
            ref var middleItemsPtr = ref Unsafe.Add(ref firstItemsPtr, firstHalfLength);
            ref var middleScratchPtr = ref Unsafe.Add(ref firstScratchPtr, firstHalfLength);
            if (mergeCount == 1) {
                InsertionSort_InPlace_Unsafe_Inclusive(ref middleScratchPtr, ref lastScratchPtr, ordering);
                InsertionSort_InPlace_Unsafe_Inclusive(ref firstScratchPtr, ref Unsafe.Subtract(ref middleScratchPtr, 1), ordering);
            } else {
                AltTopDownSplitMerge_Unsafe(ref middleScratchPtr, ref lastScratchPtr, ref middleItemsPtr, ref lastItemsPtr, secondHalfLength, mergeCount - 1, ordering);
                AltTopDownSplitMerge_Unsafe(
                    ref firstScratchPtr,
                    ref Unsafe.Subtract(ref middleScratchPtr, 1),
                    ref firstItemsPtr,
                    ref Unsafe.Subtract(ref middleItemsPtr, 1),
                    firstHalfLength,
                    mergeCount - 1,
                    ordering);
            }

            Merge_Unsafe(ref firstScratchPtr, ref middleScratchPtr, ref lastScratchPtr, out firstItemsPtr, ordering);
        }

        static void TopDownSplitMerge_toItems(ref T firstItemsPtr, ref T lastItemsPtr, ref T firstScratchPtr, ref T lastScratchPtr, int length, TOrder ordering)
        {
            var firstHalfLength = length >> 1;
            TopDownSplitMerge_toScratch(
                ref Unsafe.Add(ref firstItemsPtr, firstHalfLength),
                ref lastItemsPtr,
                ref Unsafe.Add(ref firstScratchPtr, firstHalfLength),
                ref lastScratchPtr,
                length - firstHalfLength,
                ordering);
            TopDownSplitMerge_toScratch(
                ref firstItemsPtr,
                ref Unsafe.Add(ref firstItemsPtr, firstHalfLength - 1),
                ref firstScratchPtr,
                ref Unsafe.Add(ref firstScratchPtr, firstHalfLength - 1),
                firstHalfLength,
                ordering);
            Merge_Unsafe(ref firstScratchPtr, ref Unsafe.Add(ref firstScratchPtr, firstHalfLength), ref lastScratchPtr, out firstItemsPtr, ordering);
        }

        static void TopDownSplitMerge_toScratch(ref T firstItemsPtr, ref T lastItemsPtr, ref T firstScratchPtr, ref T lastScratchPtr, int length, TOrder ordering)
        {
            if (length <= TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace_Unsafe_Inclusive(ref firstItemsPtr, ref lastItemsPtr, ordering);
                CopyInclusiveRefRange_Unsafe(ref firstItemsPtr, ref lastItemsPtr, out firstScratchPtr);
                return;
            }

            var firstHalfLength = length >> 1;
            var secondHalfLength = length - firstHalfLength;
            ref var middleItemsPtr = ref Unsafe.Add(ref firstItemsPtr, firstHalfLength);
            ref var middleScratchPtr = ref Unsafe.Add(ref firstScratchPtr, firstHalfLength);

            if (firstHalfLength < TopDownInsertionSortBatchSize) {
                InsertionSort_InPlace_Unsafe_Inclusive(ref firstItemsPtr, ref Unsafe.Subtract(ref middleItemsPtr, 1), ordering);
                InsertionSort_InPlace_Unsafe_Inclusive(ref middleItemsPtr, ref lastItemsPtr, ordering);
            } else {
                TopDownSplitMerge_toItems(ref middleItemsPtr, ref lastItemsPtr, ref middleScratchPtr, ref lastScratchPtr, secondHalfLength, ordering);
                TopDownSplitMerge_toItems(
                    ref firstItemsPtr,
                    ref Unsafe.Subtract(ref middleItemsPtr, 1),
                    ref firstScratchPtr,
                    ref Unsafe.Subtract(ref middleScratchPtr, 1),
                    firstHalfLength,
                    ordering);
            }

            Merge_Unsafe(ref firstItemsPtr, ref middleItemsPtr, ref lastItemsPtr, out firstScratchPtr, ordering);
        }

        static void Merge_Unsafe(ref T readPtrA, ref T readPtrB, ref T lastPtrB, out T writePtr, TOrder ordering)
        {
            ref var lastPtrA = ref Unsafe.Subtract(ref readPtrB, 1);
            while (true)
                if (!ordering.LessThan(readPtrB, readPtrA)) {
                    writePtr = readPtrA;
                    writePtr = ref Unsafe.Add(ref writePtr, 1);
                    if (Unsafe.IsAddressLessThan(ref readPtrA, ref lastPtrA))
                        readPtrA = ref Unsafe.Add(ref readPtrA, 1);
                    else
                        while (true) {
                            writePtr = readPtrB;
                            if (!Unsafe.AreSame(ref readPtrB, ref lastPtrB)) {
                                readPtrB = ref Unsafe.Add(ref readPtrB, 1);
                                writePtr = ref Unsafe.Add(ref writePtr, 1);
                                continue;
                            }

                            return;
                        }
                } else {
                    writePtr = readPtrB;
                    writePtr = ref Unsafe.Add(ref writePtr, 1);
                    if (Unsafe.IsAddressLessThan(ref readPtrB, ref lastPtrB))
                        readPtrB = ref Unsafe.Add(ref readPtrB, 1);
                    else
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

        static void BottomUpMergeSort(Span<T> targetArr, Span<T> scratchArr, TOrder ordering)
        {
            var n = targetArr.Length;
            ref var targetPtr = ref targetArr[0];
            ref var scratchPtr = ref scratchArr[0];

            var mergeCount = 0;
            var defaultBatchSize = BottomUpInsertionSortBatchSize & ~1;
            for (var s = defaultBatchSize; s < n; s <<= 1)
                mergeCount++;

            var width = (mergeCount & 1) == 0 ? defaultBatchSize : defaultBatchSize >> 1;
            var batchesSortedUpto = 0;

            while (true)
                if (batchesSortedUpto + width <= n) {
                    InsertionSort_InPlace_Unsafe_Inclusive(
                        ref Unsafe.Add(ref targetPtr, batchesSortedUpto),
                        ref Unsafe.Add(ref targetPtr, batchesSortedUpto + width - 1),
                        ordering);
                    batchesSortedUpto += width;
                } else {
                    if (batchesSortedUpto < n - 1)
                        InsertionSort_InPlace_Unsafe_Inclusive(ref Unsafe.Add(ref targetPtr, batchesSortedUpto), ref Unsafe.Add(ref targetPtr, n - 1), ordering);
                    break;
                }

            while (width < n) {
                var firstIdx = 0;
                var middleIdx = width;
                var endIdx = width = width << 1;
                while (endIdx <= n) {
                    Merge_Unsafe(
                        ref Unsafe.Add(ref targetPtr, firstIdx),
                        ref Unsafe.Add(ref targetPtr, middleIdx),
                        ref Unsafe.Add(ref targetPtr, endIdx - 1),
                        out Unsafe.Add(ref scratchPtr, firstIdx),
                        ordering);
                    firstIdx += width;
                    middleIdx += width;
                    endIdx += width;
                }

                if (middleIdx < n)
                    Merge_Unsafe(
                        ref Unsafe.Add(ref targetPtr, firstIdx),
                        ref Unsafe.Add(ref targetPtr, middleIdx),
                        ref Unsafe.Add(ref targetPtr, n - 1),
                        out Unsafe.Add(ref scratchPtr, firstIdx),
                        ordering);
                else if (firstIdx < n)
                    CopyInclusiveRefRange_Unsafe(ref Unsafe.Add(ref targetPtr, firstIdx), ref Unsafe.Add(ref targetPtr, n - 1), out Unsafe.Add(ref scratchPtr, firstIdx));
                ref var tmp = ref scratchPtr;
                scratchPtr = ref targetPtr;
                targetPtr = ref tmp;
            }
        }

        static void CopyInclusiveRefRange_Unsafe(ref T readPtr, ref T readUntil, out T writePtr)
        {
            while (true) {
                writePtr = readPtr;
                if (Unsafe.AreSame(ref readPtr, ref readUntil))
                    break;
                readPtr = ref Unsafe.Add(ref readPtr, 1);
                writePtr = ref Unsafe.Add(ref writePtr, 1);
            }
        }
    }
}
