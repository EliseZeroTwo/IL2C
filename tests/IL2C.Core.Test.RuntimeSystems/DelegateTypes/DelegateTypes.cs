﻿using System.ComponentModel;

namespace IL2C.RuntimeSystems
{
    public delegate string Int32ToStringDelegate(int value);
    public delegate void RefIntDelegate(ref int value);

    public sealed class AnotherDelegateTypes
    {
        private int v1 = 456;

        public string AnotherInstance_Int32ToStringImpl(int value)
        {
            return value.ToString() + "DEF" + v1.ToString();
        }
    }

    [Description("The delegate types contain special member fields, the objref instance reference and the raw method pointer. These tests are verified the IL2C can invoke delegate types combination features between static, instance, virtual method and multicasting at the runtime.")]
    [TestCase("12345678ABC", new[] { "Static_Int32ToString", "Static_Int32ToStringImpl" }, 12345678, IncludeTypes = new[] { typeof(Int32ToStringDelegate) })]
    [TestCase(1111, new[] { "Static_Void_RefInt", "Static_Void_RefIntImpl" }, 1000, IncludeTypes = new[] { typeof(RefIntDelegate) })]
    [TestCase("87654321ABC123", new[] { "Instance_Int32ToString", "Instance_Int32ToStringImpl" }, 87654321, IncludeTypes = new[] { typeof(Int32ToStringDelegate) })]
    [TestCase(1123, new[] { "Instance_Void_RefInt", "Instance_Void_RefIntImpl" }, 1000, IncludeTypes = new[] { typeof(RefIntDelegate) })]
    [TestCase("11223344DEF456", new[] { "AnotherInstance_Int32ToString", "AnotherInstance_Int32ToStringImpl" }, 11223344, IncludeTypes = new[] { typeof(Int32ToStringDelegate), typeof(AnotherDelegateTypes) })]
    [TestCase("87654321ABC123", new[] { "Virtual_Int32ToString", "Virtual_Int32ToStringImpl" }, 87654321, IncludeTypes = new[] { typeof(Int32ToStringDelegate) })]
    public sealed class DelegateTypes
    {
        #region Static
        private static string Static_Int32ToStringImpl(int value)
        {
            return value.ToString() + "ABC";
        }

        public static string Static_Int32ToString(int value)
        {
            var dlg = new Int32ToStringDelegate(Static_Int32ToStringImpl);
            return dlg(value);
        }

        private static void Static_Void_RefIntImpl(ref int value)
        {
            value += 111;
        }

        public static int Static_Void_RefInt(int value)
        {
            var dlg = new RefIntDelegate(Static_Void_RefIntImpl);
            var v = value;
            dlg(ref v);
            return v;
        }
        #endregion

        #region Instance
        private int v1 = 123;

        private string Instance_Int32ToStringImpl(int value)
        {
            return value.ToString() + "ABC" + v1.ToString();
        }

        public static string Instance_Int32ToString(int value)
        {
            var inst = new DelegateTypes();
            var dlg = new Int32ToStringDelegate(inst.Instance_Int32ToStringImpl);
            return dlg(value);
        }

        private void Instance_Void_RefIntImpl(ref int value)
        {
            value += v1;
        }

        public static int Instance_Void_RefInt(int value)
        {
            var inst = new DelegateTypes();
            var dlg = new RefIntDelegate(inst.Instance_Void_RefIntImpl);
            var v = value;
            dlg(ref v);
            return v;
        }

        public static string AnotherInstance_Int32ToString(int value)
        {
            var inst = new AnotherDelegateTypes();
            var dlg = new Int32ToStringDelegate(inst.AnotherInstance_Int32ToStringImpl);
            return dlg(value);
        }
        #endregion
    }
}
