﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace IL2C.RuntimeSystems
{
    public class ObjRefInsideObjRefType
    {
        public string Value;

        public ObjRefInsideObjRefType(string value) => this.Value = value;
    }

    public struct ObjRefInsideValueTypeType
    {
        public string Value;

        public ObjRefInsideValueTypeType(string value) => this.Value = value;
    }

    public class ObjRefInsideValueTypeInsideObjRefType
    {
        public ObjRefInsideValueTypeType Value;

        public ObjRefInsideValueTypeInsideObjRefType(string value) =>
            this.Value = new ObjRefInsideValueTypeType(value);
    }

    public struct ObjRefInsideObjRefInsideValueTypeType
    {
        public ObjRefInsideObjRefType Value;

        public ObjRefInsideObjRefInsideValueTypeType(string value) =>
            this.Value = new ObjRefInsideObjRefType(value);
    }

    public struct MultipleInsideValueTypeType
    {
        public string Value1;
        public ObjRefInsideValueTypeType Value2;
        public ObjRefInsideObjRefType Value3;

        public MultipleInsideValueTypeType(string value1, string value2, string value3)
        {
            this.Value1 = value1;
            this.Value2 = new ObjRefInsideValueTypeType(value2);
            this.Value3 = new ObjRefInsideObjRefType(value3);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class FinalizerCalleeHolder
    {
        public int Called;
    }

    public class FinalzerImplemented
    {
        private FinalizerCalleeHolder holder;

        public FinalzerImplemented(FinalizerCalleeHolder holder)
        {
            this.holder = holder;
        }

        ~FinalzerImplemented()
        {
            holder.Called = 1;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class FinalzerImplementedWithPinned
    {
        private GCHandle handle;

        public FinalzerImplementedWithPinned(FinalizerCalleeHolder holder)
        {
            this.handle = GCHandle.Alloc(holder, GCHandleType.Pinned);
        }

        ~FinalzerImplementedWithPinned()
        {
            var holder = (FinalizerCalleeHolder)(handle.Target);
            holder.Called = 1;
        }
    }

    public class StaticFieldInstanceType
    {
        public StaticFieldInstanceType()
        {
        }

        public int Value;
    }

    public static class StaticFieldTracible
    {
        public static StaticFieldInstanceType StaticFieldInstance;
    }

    public sealed class DelegateMarkHandlerForObjRef
    {
        private readonly string str;

        public DelegateMarkHandlerForObjRef(string str) => this.str = str;

        public string CombineForStrings(string v) => str + v;
    }

    public delegate string DelegateMarkHandlerForObjRefTestDelegate(string b);

    [Description("These tests are verified the IL2C manages tracing the object references and collect garbages from the heap memory.")]
    [TestCase("ABCDEF", "ObjRefInsideObjRef", IncludeTypes = new[] { typeof(ObjRefInsideObjRefType) })]
    [TestCase("ABCDEF", "ObjRefInsideValueType", IncludeTypes = new[] { typeof(ObjRefInsideValueTypeType) })]
    [TestCase("ABCDEF", "ObjRefInsideValueTypeInsideObjRef", IncludeTypes = new[] { typeof(ObjRefInsideValueTypeInsideObjRefType), typeof(ObjRefInsideValueTypeType) })]
    [TestCase("ABCDEF", "ObjRefInsideObjRefInsideValueType", IncludeTypes = new[] { typeof(ObjRefInsideObjRefInsideValueTypeType), typeof(ObjRefInsideObjRefType) })]
    [TestCase("ABCDEF1", "MultipleInsideValueType", 0, IncludeTypes = new[] { typeof(MultipleInsideValueTypeType), typeof(ObjRefInsideValueTypeType), typeof(ObjRefInsideObjRefType) })]
    [TestCase("ABCDEF2", "MultipleInsideValueType", 1, IncludeTypes = new[] { typeof(MultipleInsideValueTypeType), typeof(ObjRefInsideValueTypeType), typeof(ObjRefInsideObjRefType) })]
    [TestCase("ABCDEF3", "MultipleInsideValueType", 2, IncludeTypes = new[] { typeof(MultipleInsideValueTypeType), typeof(ObjRefInsideValueTypeType), typeof(ObjRefInsideObjRefType) })]
    [TestCase(1, new[] { "CallFinalizer", "RunCallFinalizer" }, IncludeTypes = new[] { typeof(FinalzerImplemented), typeof(FinalizerCalleeHolder) })]
    [TestCase(0, new[] { "CallFinalizerWithPinned", "RunCallFinalizerWithPinned" }, IncludeTypes = new[] { typeof(FinalzerImplementedWithPinned), typeof(FinalizerCalleeHolder) })]
    [TestCase(0, new[] { "SuppressFinalize", "RunCallFinalizerWithSuppressed" }, IncludeTypes = new[] { typeof(FinalzerImplemented), typeof(FinalizerCalleeHolder) })]
    [TestCase(1, new[] { "ReRegisterForFinalize", "RunCallFinalizerWithSuppressedAndReRegistered" }, IncludeTypes = new[] { typeof(FinalzerImplemented), typeof(FinalizerCalleeHolder) })]
    [TestCase(12345, new[] { "TraceStaticField", "RunTraceStaticField" }, 12345, IncludeTypes = new[] { typeof(StaticFieldTracible), typeof(StaticFieldInstanceType) })]
    [TestCase("ABCDEFABCGHI", new[] { "ArrayForObjRefInsideObjRefTypeTracking", "MakeArrayForObjRefInsideObjRefType" }, "ABC", "DEF", "GHI", IncludeTypes = new[] { typeof(ObjRefInsideObjRefType) })]
    [TestCase("ABCDEFABCGHI", new[] { "ArrayForObjRefInsideValueTypeTypeTracking", "MakeArrayForObjRefInsideValueTypeType" }, "ABC", "DEF", "GHI", IncludeTypes = new[] { typeof(ObjRefInsideValueTypeType) })]
    [TestCase("ABCDEFGHI", new[] { "DelegateMarkHandlerTracking", "MakeDelegateMarkHandlerForObjRefTestDelegate" }, "ABC", "DEF", IncludeTypes = new[] { typeof(DelegateMarkHandlerForObjRef), typeof(DelegateMarkHandlerForObjRefTestDelegate) })]
    [TestCase("ABCGHIJKL", new[] { "MulticastDelegateMarkHandlerTracking", "MakeMulticastDelegateMarkHandlerForObjRefTestDelegate" }, "ABC", "DEF", "GHI", IncludeTypes = new[] { typeof(DelegateMarkHandlerForObjRef), typeof(DelegateMarkHandlerForObjRefTestDelegate) })]
    public sealed class GarbageCollection
    {
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern string ObjRefInsideObjRef();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern string ObjRefInsideValueType();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern string ObjRefInsideValueTypeInsideObjRef();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern string ObjRefInsideObjRefInsideValueType();

        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern string MultipleInsideValueType(int index);

        private static void RunCallFinalizer(FinalizerCalleeHolder holder)
        {
            var implemented = new FinalzerImplemented(holder);
        }

        public static int CallFinalizer()
        {
            var holder = new FinalizerCalleeHolder();
            RunCallFinalizer(holder);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return holder.Called;
        }

        private static void RunCallFinalizerWithPinned(FinalizerCalleeHolder holder)
        {
            var implemented = new FinalzerImplementedWithPinned(holder);
            var handle = GCHandle.Alloc(implemented, GCHandleType.Pinned);
        }

        public static int CallFinalizerWithPinned()
        {
            var holder = new FinalizerCalleeHolder();
            RunCallFinalizerWithPinned(holder);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return holder.Called;
        }

        private static void RunCallFinalizerWithSuppressed(FinalizerCalleeHolder holder)
        {
            var implemented = new FinalzerImplemented(holder);
            GC.SuppressFinalize(implemented);
        }

        public static int SuppressFinalize()
        {
            var holder = new FinalizerCalleeHolder();
            RunCallFinalizerWithSuppressed(holder);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return holder.Called;
        }

        private static void RunCallFinalizerWithSuppressedAndReRegistered(FinalizerCalleeHolder holder)
        {
            var implemented = new FinalzerImplemented(holder);
            GC.SuppressFinalize(implemented);
            GC.ReRegisterForFinalize(implemented);
        }

        public static int ReRegisterForFinalize()
        {
            var holder = new FinalizerCalleeHolder();
            RunCallFinalizerWithSuppressedAndReRegistered(holder);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return holder.Called;
        }

        private static void RunTraceStaticField(int value)
        {
            var v = new StaticFieldInstanceType();
            v.Value = value;

            StaticFieldTracible.StaticFieldInstance = v;
        }

        public static int TraceStaticField(int value)
        {
            RunTraceStaticField(value);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return StaticFieldTracible.StaticFieldInstance.Value;
        }

        private static ObjRefInsideObjRefType[] MakeArrayForObjRefInsideObjRefType(string a, string b, string c) =>
            new[] { new ObjRefInsideObjRefType(a + b), new ObjRefInsideObjRefType(a + c) };

        public static string ArrayForObjRefInsideObjRefTypeTracking(string a, string b, string c)
        {
            // Test for Array_MarkHandler.
            var ea = MakeArrayForObjRefInsideObjRefType(a, b, c);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return ea[0].Value + ea[1].Value;
        }

        private static ObjRefInsideValueTypeType[] MakeArrayForObjRefInsideValueTypeType(string a, string b, string c) =>
            new[] { new ObjRefInsideValueTypeType(a + b), new ObjRefInsideValueTypeType(a + c) };

        public static string ArrayForObjRefInsideValueTypeTypeTracking(string a, string b, string c)
        {
            // Test for Array_MarkHandler.
            var ea = MakeArrayForObjRefInsideValueTypeType(a, b, c);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return ea[0].Value + ea[1].Value;
        }

        private static DelegateMarkHandlerForObjRefTestDelegate MakeDelegateMarkHandlerForObjRefTestDelegate(string a, string b)
        {
            var target = new DelegateMarkHandlerForObjRef(a + b);
            return new DelegateMarkHandlerForObjRefTestDelegate(target.CombineForStrings);
        }

        public static string DelegateMarkHandlerTracking(string a, string b)
        {
            // Test for Delegate_MarkHandler.
            var d = MakeDelegateMarkHandlerForObjRefTestDelegate(a, b);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return d("GHI");
        }

        private static DelegateMarkHandlerForObjRefTestDelegate MakeMulticastDelegateMarkHandlerForObjRefTestDelegate(string a, string b, string c)
        {
            var target1 = new DelegateMarkHandlerForObjRef(a + b);
            var target2 = new DelegateMarkHandlerForObjRef(a + c);
            return
                new DelegateMarkHandlerForObjRefTestDelegate(target1.CombineForStrings) +
                new DelegateMarkHandlerForObjRefTestDelegate(target2.CombineForStrings);
        }

        public static string MulticastDelegateMarkHandlerTracking(string a, string b, string c)
        {
            // Test for Delegate_MarkHandler (tracks each delegate target)
            var d = MakeMulticastDelegateMarkHandlerForObjRefTestDelegate(a, b, c);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return d("JKL");
        }
    }
}
