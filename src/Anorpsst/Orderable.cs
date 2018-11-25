﻿using System;

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

        public void Sort()
            => OrderedAlgorithms<T, TOrder>.ParallelQuickSort(Order, Block);

        public void ParallelQuickSort()
            => OrderedAlgorithms<T, TOrder>.ParallelQuickSort(Order, Block);

        public void QuickSort()
            => OrderedAlgorithms<T, TOrder>.QuickSort(Order, Block);

        public void MergeSort()
            => OrderedAlgorithms<T, TOrder>.TopDownMergeSort(Order, Block);

        public void InsertionSort_ForVerySmallArrays()
            => OrderedAlgorithms<T, TOrder>.InsertionSort_ForVerySmallArrays(Order, Block);
    }
}