using System;
using System.Runtime.CompilerServices;

namespace IL2C.ILConverters
{
    [TestCase((short)12345, "Int32", 12345)]
    [TestCase(unchecked ((short)45678), "Int32", 45678)]
    [TestCase((short)12345, "Int64", 12345L)]
    [TestCase(unchecked((short)45678), "Int64", 45678L)]
    [TestCase((short)12345, "IntPtr", 12345)]
    [TestCase(unchecked((short)45678), "IntPtr", 45678)]
    [TestCase((short)12345, "Single", 12345.67f)]
    [TestCase(unchecked((short)45678), "Single", 45678.91f)]
    [TestCase((short)12345, "Double", 12345.67)]
    [TestCase(unchecked((short)45678), "Double", 45678.91)]
    public sealed class Conv_i2
    {
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern short Int32(int value);

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern short Int64(long value);

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern short IntPtr(IntPtr value);

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern short Single(float value);

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern short Double(double value);
    }
}
