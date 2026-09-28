using System;
using System.Runtime.CompilerServices;

namespace RSMA.uDTP.CDR
{
    public static unsafe class CdrSerializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Pack<T>(in T value, Span<byte> destination) where T : struct
        {
            int size = Unsafe.SizeOf<T>();
            if (destination.Length < size)
                throw new ArgumentOutOfRangeException(nameof(destination), "Buffer size too small");

            fixed (byte* pDest = destination)
            {
                Unsafe.Write(pDest, value);
            }
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Unpack<T>(ReadOnlySpan<byte> source) where T : struct
        {
            fixed (byte* pSrc = source)
            {
                return Unsafe.Read<T>(pSrc);
            }
        }
    }
}