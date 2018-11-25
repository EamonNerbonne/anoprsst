using System;
using System.Runtime.CompilerServices;

namespace Anoprsst
{
    public readonly ref struct Orderable<T, TOrder>
        where TOrder : struct, IOrdering<T>
    {
        internal readonly Span<T> Block;
        internal readonly TOrder Order;

        public Orderable(Span<T> block, TOrder order)
        {
            Block = block;
            Order = order;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QuickSort()
            => OrderedAlgorithms<T, TOrder>.QuickSort(Order, Block);

    }

    public static class OrderableExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.ParallelQuickSort(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ParallelQuickSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.ParallelQuickSort(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QuickSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.QuickSort(orderable.Order, orderable.Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MergeSort<T, TOrder>(this Orderable<T, TOrder> orderable)
            where TOrder : struct, IOrdering<T>
            => OrderedAlgorithms<T, TOrder>.TopDownMergeSort(orderable.Order, orderable.Block);
    }
}
