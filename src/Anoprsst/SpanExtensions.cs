using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Anoprsst
{
    public static class SpanExtensions
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Orderable<T, TOrder> SortUsing<T, TOrder>(this Span<T> block, TOrder ordering)
            where TOrder : struct, IOrdering<T>
            => new Orderable<T, TOrder>(block, ordering);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Orderable<T, TOrder> SortUsing<T, TOrder>(this T[] block, TOrder ordering)
            where TOrder : struct, IOrdering<T>
            => new Orderable<T, TOrder>(block, ordering);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Orderable<T, ComparableOrdering<T>> SortIComparableUsing<T>(this T[] block)
            where T : struct, IComparable<T>
            => new Orderable<T, ComparableOrdering<T>>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Orderable<T, ComparableOrdering<T>> SortIComparableUsing<T>(this Span<T> block)
            where T : struct, IComparable<T>
            => new Orderable<T, ComparableOrdering<T>>(block, default);
    }
}
