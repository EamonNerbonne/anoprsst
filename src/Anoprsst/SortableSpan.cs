using System;
using System.Runtime.CompilerServices;

namespace Anoprsst
{
    public readonly ref struct SortableSpan<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        internal readonly Span<T> Block;
        internal readonly TOrder Order;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SortableSpan(Span<T> block, TOrder order)
        {
            Block = block;
            Order = order;
        }
    }

    public static class OrderableExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T, TOrder>(this SortableSpan<T, TOrder> sortableSpan)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.ParallelQuickSort(sortableSpan.Order, sortableSpan.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ParallelQuickSort<T, TOrder>(this SortableSpan<T, TOrder> sortableSpan)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.ParallelQuickSort(sortableSpan.Order, sortableSpan.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QuickSort<T, TOrder>(this SortableSpan<T, TOrder> sortableSpan)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.QuickSort(sortableSpan.Order, sortableSpan.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MergeSort<T, TOrder>(this SortableSpan<T, TOrder> sortableSpan)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.TopDownMergeSort(sortableSpan.Order, sortableSpan.Block);
    }
}
