using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct UInt16Ordering : IOrdering<ushort>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(ushort a, ushort b)
            => a < b;
    }
}