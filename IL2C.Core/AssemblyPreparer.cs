﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using IL2C.ILConveters;
using IL2C.Translators;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace IL2C
{
    public static class AssemblyPreparer
    {
        private struct ILBody
        {
            public readonly Label Label;
            public readonly ILConverter ILConverter;
            public readonly object Operand;

            public ILBody(Label label, ILConverter ilc, object operand)
            {
                this.Label = label;
                this.ILConverter = ilc;
                this.Operand = operand;
            }
        }

        private static IEnumerable<ILBody> DecodeAndEnumerateILBodies(
            DecodeContext decodeContext)
        {
            while (true)
            {
                var label = decodeContext.MakeLabel();
                if (decodeContext.TryDecode(out var ilc) == false)
                {
                    break;
                }

                var operand = ilc.DecodeOperand(decodeContext);
                yield return new ILBody(label, ilc, operand);

                if (ilc.IsEndOfPath)
                {
                    yield break;
                }
            }
        }

        private static PreparedFunction PrepareMethod(
            IPrepareContext prepareContext,
            string methodName,
            string rawMethodName,
            TypeReference returnType,
            Parameter[] parameters,
            MethodBody body)
        {
            var localVariables = body.Variables.ToArray();

            var decodeContext = new DecodeContext(
                methodName,
                returnType,
                parameters,
                localVariables,
                body.Instructions.ToArray(),
                prepareContext);

            var preparedOpCodes = decodeContext
                .Traverse(dc => dc.TryDequeueNextPath() ? dc : null, true)
                .SelectMany(dc =>
                    from ilBody in DecodeAndEnumerateILBodies(dc)
                    let generator = ilBody.ILConverter.Apply(ilBody.Operand, dc)
                    select new PreparedILBody(ilBody.Label, generator))
                .ToArray();

            var stacks = decodeContext
                .ExtractStacks()
                .ToArray();

            var labelNames = decodeContext
                .ExtractLabelNames();

            return new PreparedFunction(
                methodName,
                rawMethodName,
                returnType,
                parameters,
                preparedOpCodes,
                localVariables,
                stacks,
                labelNames);
        }

        private static PreparedFunction PreparePInvokeMethod(
            IPrepareContext prepareContext,
            string methodName,
            string rawMethodName,
            TypeReference returnType,
            Parameter[] parameters,
            CustomAttribute dllImportAttribute)
        {
            // TODO: Switch DllImport.Value include direction to library direction.
            var value = (dllImportAttribute.ConstructorArguments
                .Select(argument => argument.Value as string).FirstOrDefault());
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidProgramSequenceException(
                    "Not given DllImport attribute argument. Name={0}",
                    methodName);
            }

            prepareContext.RegisterPrivateIncludeFile(value);

            return new PreparedFunction(
                methodName,
                rawMethodName,
                returnType,
                parameters);
        }

        private static PreparedFunction PrepareMethod(
            IPrepareContext prepareContext,
            MethodDefinition method)
        {
            var methodName = method.GetFullMemberName();
            var returnType = method.ReturnType?.Resolve() ?? CecilHelper.VoidType;
            var parameters = method.GetSafeParameters();

            prepareContext.RegisterType(returnType);
            parameters.ForEach(parameter => prepareContext.RegisterType(parameter.ParameterType));

            if (method.IsPInvokeImpl)
            {
                var dllImportAttribute = method.CustomAttributes
                    .FirstOrDefault(attribute =>
                    attribute.AttributeType.FullName == typeof(DllImportAttribute).FullName);
                if (dllImportAttribute == null)
                {
                    throw new InvalidProgramSequenceException(
                        "Missing DllImport attribute at P/Invoke entry: Method={0}",
                        methodName);
                }

                return PreparePInvokeMethod(
                    prepareContext,
                    methodName,
                    method.Name,
                    returnType,
                    parameters,
                    dllImportAttribute);
            }

            return PrepareMethod(
                prepareContext,
                methodName,
                method.Name,
                returnType,
                parameters,
                method.Body);
        }

        internal static IReadOnlyDictionary<MethodDefinition, PreparedFunction> Prepare(
            TranslateContext translateContext,
            Func<MethodDefinition, bool> predict)
        {
            IPrepareContext prepareContext = translateContext;

            var allTypes = translateContext.Assembly.Modules
                .SelectMany(module => module.Types)
                .Where(type => type.IsValueType || type.IsClass)
                .ToArray();
            var types = allTypes
                .Where(type => !(type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly))
                .ToArray();

            // Lookup type references.
            types.ForEach(prepareContext.RegisterType);

            // Lookup fields.
            types.SelectMany(type => type.Fields)
                .ForEach(field => prepareContext.RegisterType(field.FieldType));

            // Construct result.
            return allTypes
                .SelectMany(type => type.Methods)
                .Where(predict)
                .ToDictionary(method => method, method => PrepareMethod(prepareContext, method));
        }

        public static IReadOnlyDictionary<MethodDefinition, PreparedFunction> Prepare(
            TranslateContext translateContext)
        {
            return Prepare(translateContext, method => (method.IsConstructor == false) || (method.IsStatic == false));
        }
    }
}
