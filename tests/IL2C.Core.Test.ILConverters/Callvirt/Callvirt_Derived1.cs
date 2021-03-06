using System;
using System.Runtime.CompilerServices;

namespace IL2C.ILConverters
{
    [TestId("Callvirt")]
    [TestCase("CallvirtTest", new[] { "Derived1_ToString_System_Object", "ToString" })]
    [TestCase("CallvirtTest", new[] { "Derived1_ToString_IL2C_ILConverters_Callvirt", "ToString" })]
    public sealed class Callvirt_Derived1
    {
        public override string ToString()
        {
            return "CallvirtTest";
        }

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern string Derived1_ToString_System_Object();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern string Derived1_ToString_IL2C_ILConverters_Callvirt();
    }
}
