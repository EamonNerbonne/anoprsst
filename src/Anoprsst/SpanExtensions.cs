using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Anoprsst.BuiltinOrderings;

namespace Anoprsst
{
    public static class SpanExtensions
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("This method has been renamed to " + nameof(WithOrder) + ".", true)]
        public static SortableSpan<T, TOrder> SortUsing<T, TOrder>(this Span<T> block, TOrder ordering)
            where TOrder : struct, IOrdering<T>
            => new SortableSpan<T, TOrder>(block, ordering);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("This method has been renamed to " + nameof(WithIComparableOrder) + ".", true)]
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
        public static SortableSpan<T, ComparableOrdering<T>> WithIComparableOrder<T>(this Span<T> block)
            where T : struct, IComparable<T>
            => new SortableSpan<T, ComparableOrdering<T>>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<double, DoubleOrdering> WithIComparableOrder(this Span<double> block)
            => new SortableSpan<double, DoubleOrdering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<float, FloatOrdering> WithIComparableOrder(this Span<float> block)
            => new SortableSpan<float, FloatOrdering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<sbyte, Int8Ordering> WithIComparableOrder(this Span<sbyte> block)
            => new SortableSpan<sbyte, Int8Ordering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<byte, UInt8Ordering> WithIComparableOrder(this Span<byte> block)
            => new SortableSpan<byte, UInt8Ordering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<short, Int16Ordering> WithIComparableOrder(this Span<short> block)
            => new SortableSpan<short, Int16Ordering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<ushort, UInt16Ordering> WithIComparableOrder(this Span<ushort> block)
            => new SortableSpan<ushort, UInt16Ordering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<int, Int32Ordering> WithIComparableOrder(this Span<int> block)
            => new SortableSpan<int, Int32Ordering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<uint, UInt32Ordering> WithIComparableOrder(this Span<uint> block)
            => new SortableSpan<uint, UInt32Ordering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<long, Int64Ordering> WithIComparableOrder(this Span<long> block)
            => new SortableSpan<long, Int64Ordering>(block, default);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SortableSpan<ulong, UInt64Ordering> WithIComparableOrder(this Span<ulong> block)
            => new SortableSpan<ulong, UInt64Ordering>(block, default);
    }
}
