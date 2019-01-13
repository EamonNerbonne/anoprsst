using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct UInt64Ordering : IOrdering<ulong>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(ulong a, ulong b)
            => a < b;
    }
}
