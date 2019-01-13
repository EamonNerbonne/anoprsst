using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct Int64Ordering : IOrdering<long>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(long a, long b)
            => a < b;
    }
}