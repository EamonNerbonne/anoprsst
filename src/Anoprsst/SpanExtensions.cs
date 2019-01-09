using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Anoprsst.BuiltinOrderings;

namespace Anoprsst
{
    public static class SpanExtensions
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("This method has been renamed to "+nameof(WithOrder) + ".", true)]
        public static SortableSpan<T, TOrder> SortUsing<T, TOrder>(this Span<T> block, TOrder ordering)
            where TOrder : struct, IOrdering<T>
            => new SortableSpan<T, TOrder>(block, ordering);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("This method has been renamed to "+nameof(WithOrder) + ".", true)]
        public static SortableSpan<T, TOrder> SortUsing<T, TOrder>(this T[] block, TOrder ordering)
            where TOrder : struct, IOrdering<T>
            => new SortableSpan<T, TOrder>(block, ordering);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("This method has been renamed to "+nameof(WithIComparableOrder) + ".", true)]
        public static SortableSpan<T, ComparableOrdering<T>> SortIComparableUsing<T>(this T[] block)
            where T : struct, IComparable<T>
            => new SortableSpan<T, ComparableOrdering<T>>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("This method has been renamed to "+nameof(WithIComparableOrder) + ".", true)]
        public static SortableSpan<T, ComparableOrdering<T>> SortIComparableUsing<T>(this Span<T> block)
            where T : struct, IComparable<T>
            => new SortableSpan<T, ComparableOrdering<T>>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<T, TOrder> WithOrder<T, TOrder>(this Span<T> block, TOrder ordering)
            where TOrder : struct, IOrdering<T>
            => new SortableSpan<T, TOrder>(block, ordering);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<T, TOrder> WithOrder<T, TOrder>(this T[] block, TOrder ordering)
            where TOrder : struct, IOrdering<T>
            => new SortableSpan<T, TOrder>(block, ordering);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<T, ComparableOrdering<T>> WithIComparableOrder<T>(this T[] block)
            where T : struct, IComparable<T>
            => new SortableSpan<T, ComparableOrdering<T>>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<T, ComparableOrdering<T>> WithIComparableOrder<T>(this Span<T> block)
            where T : struct, IComparable<T>
            => new SortableSpan<T, ComparableOrdering<T>>(block, default);
    }
}
