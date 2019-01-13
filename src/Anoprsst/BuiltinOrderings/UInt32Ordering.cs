using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct UInt32Ordering : IOrdering<uint>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(uint a, uint b)
            => a < b;
    }
}