using System;
using System.Runtime.CompilerServices;

namespace IL2C.ILConverters
{
    [TestCase((sbyte)123, "Int32", 123)]
    [TestCase(unchecked ((sbyte)456), "Int32", 456)]
    [TestCase((sbyte)123, "Int64", 123L)]
    [TestCase(unchecked((sbyte)456), "Int64", 456L)]
    [TestCase((sbyte)123, "IntPtr", 123)]
    [TestCase(unchecked((sbyte)456), "IntPtr", 456)]
    [TestCase((sbyte)123, "Single", 123.45f)]
    [TestCase(unchecked((sbyte)456), "Single", 456.78f)]
    [TestCase((sbyte)123, "Double", 123.45)]
    [TestCase(unchecked((sbyte)456), "Double", 456.78)]
    public sealed class Conv_i1
    {
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern sbyte Int32(int value);

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern sbyte Int64(long value);

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern sbyte IntPtr(IntPtr value);

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern sbyte Single(float value);

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern sbyte Double(double value);
    }
}
