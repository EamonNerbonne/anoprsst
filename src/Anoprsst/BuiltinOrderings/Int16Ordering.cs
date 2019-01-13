using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct Int16Ordering : IOrdering<short>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(short a, short b)
            => a < b;
    }
}