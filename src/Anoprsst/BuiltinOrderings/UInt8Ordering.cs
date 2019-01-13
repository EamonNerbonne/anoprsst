using System.Runtime.CompilerServices;

namespace Anoprsst.BuiltinOrderings
{
    public struct UInt8Ordering : IOrdering<byte>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LessThan(byte a, byte b)
            => a < b;
    }
}