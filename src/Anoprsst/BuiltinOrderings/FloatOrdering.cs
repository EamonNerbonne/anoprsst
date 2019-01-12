using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct FloatOrdering : IOrdering<float>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(float a, float b)
            => a < b || !(a >= b);
    }
}
