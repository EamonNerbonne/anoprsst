using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct DoubleOrdering : IOrdering<double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(double a, double b)
            => a < b || !(a >= b);
    }
}
