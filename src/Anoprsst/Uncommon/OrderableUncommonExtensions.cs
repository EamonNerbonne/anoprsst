using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Anoprsst.Uncommon
{
    public static class OrderableUncommonExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DualPivotQuickSort<T, TOrder>(this SortableSpan<T, TOrder> sortableSpan)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.DualPivotQuickSort(sortableSpan.Order, sortableSpan.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AltTopDownMergeSort<T, TOrder>(this SortableSpan<T, TOrder> sortableSpan)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.AltTopDownMergeSort(sortableSpan.Order, sortableSpan.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertionSort_ForVerySmallArrays<T, TOrder>(this SortableSpan<T, TOrder> sortableSpan)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.InsertionSort_ForVerySmallArrays(sortableSpan.Order, sortableSpan.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BottomUpMergeSort<T, TOrder>(this SortableSpan<T, TOrder> sortableSpan)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.BottomUpMergeSort(sortableSpan.Order, sortableSpan.Block);
    }
}
