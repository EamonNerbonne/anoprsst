using System;
using System.Runtime.CompilerServices;

namespace Anoprsst
{
    public readonly ref struct Orderable<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        internal readonly Span<T> Block;
        internal readonly TOrder Order;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Orderable(Span<T> block, TOrder order)
        {
            Block = block;
            Order = order;
        }

    }

    public static class OrderableExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.ParallelQuickSort(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ParallelQuickSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.ParallelQuickSort(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QuickSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.QuickSort(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MergeSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderingsFor<T>.WithOrder<TOrder>.TopDownMergeSort(orderable.Order, orderable.Block);
    }
}
