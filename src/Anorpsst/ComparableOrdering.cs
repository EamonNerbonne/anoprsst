using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Anoprsst
{
    /// <summary>
    /// Avoid using this to wrap non-struct IComparables.  Although that works, doing so imposes large overheads, rendering any sorting algorithm pretty much as slow as Array.Sort.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ComparableOrdering<T> : IOrdering<T>
        where T : IComparable<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(T a, T b)
            => a.CompareTo(b) < 0;
    }
}
