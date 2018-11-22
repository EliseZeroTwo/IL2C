﻿using System.Diagnostics;
using System.Linq;

using IL2C.Metadata;

namespace IL2C.Writers
{
    internal static class TypeHelperWriter
    {
        public static void InternalConvertTypeHelper(
            CodeTextWriter tw,
            ITypeInformation declaredType)
        {
            Debug.Assert(!declaredType.IsInterface);

            tw.WriteLine("//////////////////////");
            tw.WriteLine("// [7] Runtime helpers:");
            tw.SplitLine();

            // Write trampoline virtual functions if type is value type.
            var virtualMethods = declaredType.CalculatedVirtualMethods;
            var trampolineTargets = virtualMethods.
                Where(entry => declaredType.IsValueType && entry.method.DeclaringType.Equals(declaredType)).
                Select(entry => entry.method).
                ToArray();
            foreach (var method in trampolineTargets)
            {
                tw.WriteLine(
                    "// [7-12] Trampoline virtual function: {0}",
                    method.FriendlyName);
                tw.WriteLine(
                    "static {0} {1}_Trampoline_VFunc__(System_ValueType* this__{2})",
                    method.ReturnType.CLanguageTypeName,
                    method.CLanguageFunctionName,
                    string.Concat(method.Parameters.
                        Skip(1).
                        Select(p => string.Format(", {0} {1}", p.TargetType.CLanguageTypeName, p.ParameterName))));
                tw.WriteLine(
                    "{");

                using (var _ = tw.Shift())
                {
                    tw.WriteLine(
                        "il2c_assert(this__ != NULL);");
                    tw.SplitLine();
                    tw.WriteLine(
                        "{0}* pValue =",
                        declaredType.CLanguageTypeName);

                    using (var __ = tw.Shift())
                    {
                        tw.WriteLine(
                            "il2c_unsafe_unbox__(this__, {0});",
                            declaredType.CLanguageTypeName);
                    }

                    tw.WriteLine(
                        "return {0}(pValue{1});",
                        method.CLanguageFunctionName,
                        string.Concat(method.Parameters.
                            Skip(1).
                            Select(p => string.Format(", {0}", p.ParameterName))));    // These aren't required expression evaluation.
                }

                tw.WriteLine(
                    "}");
                tw.SplitLine();
            }

            var overrideMethods = declaredType.OverrideMethods;
            var newSlotMethods = declaredType.NewSlotMethods;
            var overrideBaseMethods = declaredType.OverrideBaseMethods;

            // If virtual method collection doesn't contain reuseslot and newslot method at declared types:
            if (!overrideMethods.Any() &&
                !newSlotMethods.Any(method => method.DeclaringType.Equals(declaredType)))
            {
                tw.WriteLine(
                    "// [7-10-1] VTable (Not defined, same as {0})",
                    declaredType.BaseType.FriendlyName);
                tw.SplitLine();
            }
            // Require new vtable.
            else
            {
                // Write virtual methods
                tw.WriteLine(
                    "// [7-10-2] VTable");
                tw.WriteLine(
                    "{0}_VTABLE_DECL__ {0}_VTABLE__ = {{",
                    declaredType.MangledName);

                using (var _ = tw.Shift())
                {
                    tw.WriteLine("0, // Adjustor offset");

                    // Write only visible methods because virtual method collection contains the explicitly implementation methods.
                    foreach (var (method, _) in virtualMethods.
                        Where(entry => entry.method.IsPublic || entry.method.IsFamily || entry.method.IsFamilyOrAssembly))
                    {
                        // MEMO: Transfer trampoline virtual function if declared type is value type.
                        //   Because arg0 type is native value type pointer, but the virtual function requires boxed objref.
                        //   The trampoline will unbox from objref to target value type.
                        tw.WriteLine(
                            "({0}){1}{2},",
                            method.CLanguageFunctionTypePrototype,
                            method.CLanguageFunctionName,
                            (declaredType.IsValueType && method.DeclaringType.Equals(declaredType)) ? "_Trampoline_VFunc__" : string.Empty);
                    }
                }

                tw.WriteLine("};");
                tw.SplitLine();
            }

            // Aggregate all declared methods from derived to base types.
            var declaredMethods = declaredType.
                Traverse(type => type.BaseType).
                SelectMany(type => type.DeclaredMethods).
                Where(method =>
                    !method.IsConstructor &&
                    !method.IsStatic).
                Distinct(MetadataUtilities.VirtualMethodSignatureComparer).
                ToArray();

            // Write interface VTables.
            var interfaceTypes = declaredType.
                Traverse(type => type.BaseType).
                SelectMany(type => type.InterfaceTypes).
                Distinct(). // Important operator sequence: distinct --> reverse
                Reverse().  // Because all interface types overrided by derived class type,
                ToArray();  // we have to aggregate to be visible interface types.
            foreach (var interfaceType in interfaceTypes)
            {
                var implementationMethods = interfaceType.DeclaredMethods.
                    Select(interfaceMethod =>
                    {
                        // Extract interface implementation methods by overrided from derived to based.
                        var targetMethod = declaredMethods.
                            Select(dm => dm.Overrides.Contains(interfaceMethod) ? dm : null).
                            FirstOrDefault(vm => vm != null);
                        if (targetMethod != null)
                        {
                            return new { interfaceMethod, targetMethod };
                        }

                        /////////////////////////////////////////////////////////
                        // If didn't find the method for explicitly implementation,
                        // try to find implicitly implementation by the method signature.
                        // (See also: InstanceMultipleCombinedImplement test)

                        // Detect the implicitly interface implemented method:

                        // (1) Aggregate all visible declared methods from derived to base types.
                        //   firstInterfaceImplementedType = Foo2
                        //     interfaceType: IBar1
                        //     System.Object <-- Foo1 <-- Foo2 <-- Foo3
                        //                       |        |        +-- IBar2 / Method1
                        //                       |        +----------- IBar1 / Method2, Method3
                        //                       +-------------------- IBar2 / Method1, Method3
                        //   declaredVisibleMethods = [Foo2.Method2, Foo2.Method3, Foo1.Method1]
                        //     (Except Foo1.Method3 by VirtualMethodSignatureComparer)
                        var firstInterfaceImplementedType = declaredType.
                            Traverse(type => type.BaseType).
                            First(type => type.InterfaceTypes.Contains(interfaceType));
                        var declaredVisibleMethods = firstInterfaceImplementedType.
                            Traverse(type => type.BaseType).
                            SelectMany(type => type.DeclaredMethods).
                            Where(dm => !dm.IsConstructor && !dm.IsStatic &&
                                (dm.IsPublic || dm.IsFamily || dm.IsFamilyOrAssembly)).
                            Distinct(MetadataUtilities.VirtualMethodSignatureComparer).
                            ToArray();

                        // (2) Find first matching declaredMethod for same as this interfaceMethod.
                        //     "Same" means the method signature (using VirtualMethodSignatureComparer)
                        //   declaredVisibleMethods = [Foo2.Method2, Foo2.Method3, Foo1.Method1]
                        //   interfaceMethod = IBar2.Method1
                        //   targetBaseMethod = Foo1.Method1
                        var targetBaseMethod = declaredVisibleMethods.
                            Select(dm => MetadataUtilities.VirtualMethodSignatureComparer.Equals(dm, interfaceMethod) ? dm : null).
                            First(dm => dm != null);    // We will find exactly.

                        // (3) Find last matching (mostly derived) method from base to overrides.
                        //   baseInterfaceImplementedTypes = [Foo1, Foo2, Foo3]
                        //   interfaceMethod = IBar2.Method1
                        //   targetBaseMethod = Foo1.Method1
                        //   lastMatchedMethod = Foo3.Method1
                        var baseInterfaceImplementedTypes = declaredType.
                            Traverse(type => type.BaseType).
                            Reverse().
                            Where(type => targetBaseMethod.DeclaringType.IsAssignableFrom(type)).
                            ToArray();
                        targetMethod = baseInterfaceImplementedTypes.
                            SelectMany(type => type.DeclaredMethods.
                                Where(dm => MetadataUtilities.VirtualMethodSignatureComparer.Equals(dm, interfaceMethod))).
                            TakeWhile(dm => (dm.IsVirtual && (!dm.IsNewSlot || dm.IsReuseSlot)) ||
                                dm.Equals(targetBaseMethod)).
                            Last();

                        return new { interfaceMethod, targetMethod };
                    }).
                    ToArray();

                tw.WriteLine(
                    "// [7-12] VTable for {0}",
                    interfaceType.FriendlyName);
                tw.WriteLine(
                    "{0}_VTABLE_DECL__ {1}_{0}_VTABLE__ = {{",
                    interfaceType.MangledName,
                    declaredType.MangledName);

                using (var _ = tw.Shift())
                {
                    // The adjustor offset.
                    tw.WriteLine(
                        "il2c_adjustor_offset({0}, {1}),",
                        declaredType.MangledName,
                        interfaceType.MangledName);

                    foreach (var entry in implementationMethods)
                    {
                        tw.WriteLine(
                            "({0}){1},",
                            entry.interfaceMethod.CLanguageFunctionTypePrototype,
                            entry.targetMethod.CLanguageFunctionName);
                    }
                }

                tw.WriteLine("};");
                tw.SplitLine();
            }

            // Write runtime type information
            tw.WriteLine("// [7-8] Runtime type information");

            // Aggregate mark target fields (except the enum type and the delegate type)
            var markTargetFields =
                (!declaredType.IsEnum && !declaredType.IsDelegate) ?
                    declaredType.Fields.
                    Where(field => !field.IsStatic && field.FieldType.IsReferenceType).
                    ToArray() :
                new IFieldInformation[0];

            // ex: IL2C_RUNTIME_TYPE_BEGIN(System_ValueType, "System.ValueType", IL2C_TYPE_REFERENCE, System_Object, 0, 0)
            tw.WriteLine(
                "IL2C_RUNTIME_TYPE_BEGIN({0}, \"{1}\", {2}, {3}, {4}, {5})",
                declaredType.MangledName,
                declaredType.FriendlyName, // Type name (UTF-8 string, C compiler embeds)
                declaredType.IsEnum ?      // Type attribute flags
                    (declaredType.ElementType.IsUnsigned ? "IL2C_TYPE_UNSIGNED_INTEGER" : "IL2C_TYPE_INTEGER") :
                    declaredType.IsDelegate ? "IL2C_TYPE_VARIABLE" :
                    declaredType.IsReferenceType ? "IL2C_TYPE_REFERENCE" :
                    "IL2C_TYPE_VALUE",
                declaredType.BaseType.MangledName,
                declaredType.IsDelegate ? "System_Delegate_MarkHandler__" : markTargetFields.Length.ToString(),
                interfaceTypes.Length);

            using (var _ = tw.Shift())
            {
                // Mark target offsets.
                foreach (var field in markTargetFields)
                {
                    // ex: IL2C_RUNTIME_TYPE_MARK_TARGET(System_Exception, message__)
                    tw.WriteLine(
                        "IL2C_RUNTIME_TYPE_MARK_TARGET({0}, {1})",
                        declaredType.MangledName,
                        field.Name);
                }

                // Write implemented interfaces (IL2C_IMPLEMENTED_INTERFACE)
                foreach (var interfaceType in interfaceTypes)
                {
                    // ex: IL2C_RUNTIME_TYPE_INTERFACE(Foo, System_IDisposable)
                    tw.WriteLine(
                        "IL2C_RUNTIME_TYPE_INTERFACE({0}, {1})",
                        declaredType.MangledName,
                        interfaceType.MangledName);
                }
            }

            tw.WriteLine("IL2C_RUNTIME_TYPE_END();");
            tw.SplitLine();
        }

        public static void InternalConvertTypeHelperForInterface(
            CodeTextWriter tw,
            ITypeInformation declaredType)
        {
            Debug.Assert(declaredType.IsInterface);

            tw.WriteLine("//////////////////////");
            tw.WriteLine("// [8] Runtime helpers:");
            tw.SplitLine();

            // Write runtime type information
            tw.WriteLine("// [8-1] Runtime type information");

            // ex: IL2C_RUNTIME_TYPE_INTERFACE_BEGIN(System_IDisposable, "System.IDisposable", 0)
            var interfaceTypes = declaredType.InterfaceTypes;
            tw.WriteLine(
                "IL2C_RUNTIME_TYPE_INTERFACE_BEGIN({0}, \"{1}\", {2})",
                declaredType.MangledName,
                declaredType.FriendlyName, // Type name (UTF-8 string, C compiler embeds)
                interfaceTypes.Length);

            using (var _ = tw.Shift())
            {
                // TODO: can't place for IL2C_IMPLEMENTED_INTERFACE, because the interface type doesn't define the VTable.
                // Write implemented interfaces (IL2C_IMPLEMENTED_INTERFACE)
                foreach (var interfaceType in interfaceTypes)
                {
                    // ex: IL2C_RUNTIME_TYPE_INTERFACE(Foo, System_IDisposable)
                    tw.WriteLine(
                        "IL2C_RUNTIME_TYPE_INTERFACE({0}, {1})",
                        declaredType.MangledName,
                        interfaceType.MangledName);
                }
            }

            tw.WriteLine(
                "IL2C_RUNTIME_TYPE_END();");
            tw.SplitLine();
        }
    }
}
