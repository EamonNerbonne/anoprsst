using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Anoprsst.Uncommon
{
    public static class OrderableExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DualPivotQuickSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.DualPivotQuickSort(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AltTopDownMergeSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.AltTopDownMergeSort(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QuickSort_ForSmallArrays<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.QuickSort_ForSmallArrays(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertionSort_ForVerySmallArrays<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.InsertionSort_ForVerySmallArrays(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BottomUpMergeSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.BottomUpMergeSort(orderable.Order, orderable.Block);
    }
}
