﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Mono.Cecil;

namespace IL2C.Metadata
{
    internal static class MetadataUtilities
    {
        public static string GetLabelName(int offset) =>
            string.Format("IL_{0:x4}", offset);

        private static string TrimGenericShortIdentifier(string memberName)
        {
            var index = memberName.LastIndexOf('`');
            return (index >= 0) ? memberName.Substring(0, index) : memberName;
        }

        private static string GetMemberName(this MemberReference member)
        {
            var declaringTypes = member.DeclaringType.
                Traverse(current => current.DeclaringType).
                Reverse().
                ToArray();
            var namespaceName = declaringTypes.FirstOrDefault()
                ?.Namespace
                ?? (member as TypeReference)?.Namespace;

            return string.Join(
                ".",
                new[] { namespaceName }.
                    Concat(declaringTypes.Select(type => TrimGenericShortIdentifier(type.Name))).
                    Concat(new[] { TrimGenericShortIdentifier(member.Name) }));
        }

        public static string GetUniqueName(this MemberReference member)
        {
            var typeReference = member as TypeReference;
            if (typeReference?.IsGenericParameter ?? false)
            {
                return member.Name;
            }

            var memberName = GetMemberName(member);

            if (typeReference?.HasGenericParameters ?? false)
            {
                return memberName +
                    "<" + string.Join(",", typeReference.GenericParameters.Select(parameter => parameter.GetUniqueName())) + ">";
            }

            if (member is GenericInstanceType genericInstanceType &&
                genericInstanceType.HasGenericArguments)
            {
                return memberName +
                    "<" + string.Join(",", genericInstanceType.GenericArguments.Select(argument => argument.GetUniqueName())) + ">";
            }

            if (member is MethodReference methodReference &&
                methodReference.HasGenericParameters)
            {
                return memberName +
                    "<" + string.Join(",", methodReference.GenericParameters.Select(parameter => parameter.GetUniqueName())) + ">";
            }

            if (member is GenericInstanceMethod genericInstanceMethod &&
                genericInstanceMethod.HasGenericArguments)
            {
                return memberName +
                    "<" + string.Join(",", genericInstanceMethod.GenericArguments.Select(argument => argument.GetUniqueName())) + ">";
            }

            return memberName;
        }

        public static string GetFriendlyName(this MemberReference member) =>
            GetUniqueName(member);

        public static ITypeInformation UnwrapCoveredType(this ITypeInformation type)
        {
            if (type.IsByReference || type.IsPointer)
            {
                return type.ElementType;
            }
            if (type.IsArray)
            {
                return type.ElementType.UnwrapCoveredType();
            }
            return type;
        }

        #region MethodSignatureTypeComparer
        private sealed class MethodSignatureTypeComparerImpl
            : ICombinedComparer<ITypeInformation>
        {
            public MethodSignatureTypeComparerImpl()
            {
            }

            public int Compare(ITypeInformation x, ITypeInformation y)
            {
                if (x.Equals(y))
                {
                    return 0;
                }

                // Prioritize for narrowing base type.
                var xr = x.IsAssignableFrom(y);
                var yr = y.IsAssignableFrom(x);

                if (!xr && yr)
                {
                    return -1;
                }
                if (xr && !yr)
                {
                    return 1;
                }
                Debug.Assert(!(xr && yr));

                if (!x.IsByReference && y.IsByReference)
                {
                    var r = Compare(x, y.ElementType);
                    return (r == 0) ? -1 : r;
                }
                if (x.IsByReference && !y.IsByReference)
                {
                    var r = Compare(x.ElementType, y);
                    return (r == 0) ? 1 : r;
                }
                if (x.IsByReference && y.IsByReference)
                {
                    return Compare(x.ElementType, y.ElementType);
                }

                if (x.IsPrimitive)
                {
                    return -1;
                }
                if (y.IsPrimitive)
                {
                    return 1;
                }
                if (x.IsValueType)
                {
                    return -1;
                }
                if (y.IsValueType)
                {
                    return 1;
                }

                if (x.IsClass)
                {
                    return -1;
                }
                if (y.IsClass)
                {
                    return 1;
                }
                if (x.IsInterface)
                {
                    return -1;
                }
                if (y.IsInterface)
                {
                    return 1;
                }
                if (x.IsArray)
                {
                    return -1;
                }
                if (y.IsArray)
                {
                    return 1;
                }
                if (x.IsPointer)
                {
                    return -1;
                }
                if (y.IsPointer)
                {
                    return 1;
                }

                return -1;
            }

            public bool Equals(ITypeInformation x, ITypeInformation y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(ITypeInformation obj)
            {
                return obj.UniqueName.GetHashCode();
            }
        }

        public static readonly ICombinedComparer<ITypeInformation> MethodSignatureTypeComparer =
            new MethodSignatureTypeComparerImpl();
        #endregion

        #region MethodSignatureParameterComparer
        private sealed class MethodSignatureParameterComparerImpl
            : ICombinedComparer<IParameterInformation>
        {
            public MethodSignatureParameterComparerImpl()
            {
            }

            public int Compare(IParameterInformation x, IParameterInformation y)
            {
                var xt = x.TargetType;
                var yt = y.TargetType;

                var xr = xt.IsAssignableFrom(yt);
                var yr = yt.IsAssignableFrom(xt);

                return MethodSignatureTypeComparer.Compare(xt, yt);
            }

            public bool Equals(IParameterInformation x, IParameterInformation y)
            {
                var xt = x.TargetType;
                var yt = y.TargetType;

                return MethodSignatureTypeComparer.Equals(xt, yt);
            }

            public int GetHashCode(IParameterInformation obj)
            {
                return obj.TargetType.GetHashCode();
            }
        }

        public static readonly ICombinedComparer<IParameterInformation> MethodSignatureParameterComparer =
            new MethodSignatureParameterComparerImpl();
        #endregion

        #region MethodSignatureComparer
        private sealed class MethodSignatureComparerImpl
            : ICombinedComparer<IMethodInformation>
        {
            // This is a overload stabilizer

            private readonly bool isVirtual;

            public MethodSignatureComparerImpl(bool isVirtual)
            {
                this.isVirtual = isVirtual;
            }

            public int Compare(IMethodInformation x, IMethodInformation y)
            {
                var rn = x.Name.CompareTo(y.Name);
                if (rn != 0)
                {
                    return rn;
                }

                var xps = x.Parameters;
                var yps = y.Parameters;

                rn = xps.Length.CompareTo(yps.Length);
                if (rn != 0)
                {
                    return rn;
                }

                // The arg0 type for virtual method has to ignore different types.
                return xps.
                    Zip(yps, (xp, yp) => new { xp, yp }).
                    Select((entry, index) =>
                        (isVirtual && (index == 0)) ? 0 : MethodSignatureParameterComparer.Compare(entry.xp, entry.yp)).
                    FirstOrDefault(r => r != 0);
            }

            public bool Equals(IMethodInformation x, IMethodInformation y)
            {
                if (x.Name != y.Name)
                {
                    return false;
                }

                var xps = x.Parameters;
                var yps = y.Parameters;

                if (xps.Length != yps.Length)
                {
                    return false;
                }

                // The arg0 type for virtual method has to ignore different types.
                return xps.
                    Zip(yps, (xp, yp) => new { xp, yp }).
                    Select((entry, index) =>
                        (isVirtual && (index == 0)) ? true : MethodSignatureParameterComparer.Equals(entry.xp, entry.yp)).
                    FirstOrDefault(r => r);
            }

            public int GetHashCode(IMethodInformation obj)
            {
                return obj.Name.GetHashCode();
            }
        }

        public static readonly ICombinedComparer<IMethodInformation> MethodSignatureComparer =
            new MethodSignatureComparerImpl(false);

        // It compares type equality without first argument.
        // Because the first argument is maybe different type by polymorphic at the virtual methods.
        public static readonly ICombinedComparer<IMethodInformation> VirtualMethodSignatureComparer =
            new MethodSignatureComparerImpl(true);
        #endregion

        public static IDictionary<string, IMethodInformation[]> CalculateOverloadMethods(
            this IEnumerable<IMethodInformation> methods)
        {
            // Aggregate overloads and overrides.
            var dict = new SortedDictionary<string, IMethodInformation[]>();
            foreach (var g in methods.GroupBy(method => method.Name))
            {
                var r = g.
                    OrderBy(method => method.IsStatic ? 1 : 0).
                    ThenBy(method => method.Parameters.Any(p => p.IsParamArray) ? 1 : 0).
                    ThenBy(method => method.Parameters.Length).
                    ThenBy(method => method.IsReuseSlot ? 1 : 0).
                    ThenBy(method => method, MethodSignatureComparer).
                    ToArray();

                dict.Add(g.Key, r);
            }
            return dict;
        }

        public static IEnumerable<(IMethodInformation method, int overloadIndex)> CalculateVirtualMethods(
            this IEnumerable<IMethodInformation> methods)
        {
            // Calculate overrided virtual methods using NewSlot attribute.

            var overloadIndexes = new List<(IMethodInformation method, int overloadIndex)>();

            foreach (var method in methods.Where(method => method.IsVirtual))
            {
                // Search from derived to base
                var index = overloadIndexes.FindLastIndex(entry =>
                    VirtualMethodSignatureComparer.Equals(entry.method, method));
                if (index >= 0)
                {
                    // It's new slotted.
                    if (method.IsNewSlot)
                    {
                        // Add new method.
                        var (_, oi) = overloadIndexes[index];
                        overloadIndexes.Add((method, oi + 1));
                    }
                    else
                    {
                        // ReuseSlot: It's overrided from base method.
                        Debug.Assert(method.IsReuseSlot);

                        // Replace.
                        var (_, oi) = overloadIndexes[index];
                        overloadIndexes[index] = (method, oi);
                    }
                }
                else
                {
                    overloadIndexes.Add((method, 0));
                }
            }

            return overloadIndexes.ToArray();
        }
    }
}
