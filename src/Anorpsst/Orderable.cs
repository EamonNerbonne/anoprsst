using System;
using System.Runtime.CompilerServices;

namespace Anoprsst
{
    public ref struct Orderable<T, TOrder>
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
        public void Sort()
            => OrderedAlgorithms<T, TOrder>.ParallelQuickSort(Order, Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParallelQuickSort()
            => OrderedAlgorithms<T, TOrder>.ParallelQuickSort(Order, Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QuickSort()
            => OrderedAlgorithms<T, TOrder>.QuickSort(Order, Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MergeSort()
            => OrderedAlgorithms<T, TOrder>.TopDownMergeSort(Order, Block);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InsertionSort_ForVerySmallArrays()
            => OrderedAlgorithms<T, TOrder>.InsertionSort_ForVerySmallArrays(Order, Block);
    }
}
