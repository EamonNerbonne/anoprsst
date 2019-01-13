using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct Int8Ordering : IOrdering<sbyte>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(sbyte a, sbyte b)
            => a < b;
    }
}
