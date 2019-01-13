using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct Int32Ordering : IOrdering<int>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(int a, int b)
            => a < b;
    }
}