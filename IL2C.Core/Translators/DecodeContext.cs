﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

using IL2C.ILConveters;

namespace IL2C.Translators
{
    internal sealed class DecodeContext
    {
        #region Private types
        private sealed class StackInformationHolder
        {
            private readonly List<SymbolInformation> typedStackInformation;
            private readonly int stackPointer;
            private int selectedStackInformation = -1;

            private StackInformationHolder(
                int stackPointer, List<SymbolInformation> typedStackInformation)
            {
                this.stackPointer = stackPointer;
                this.typedStackInformation = typedStackInformation;
            }

            public StackInformationHolder(int stackPointer)
                : this(stackPointer, new List<SymbolInformation>())
            {
            }

            public string GetOrAdd(TypeReference targetType)
            {
                var index = typedStackInformation
                    .FindIndex(si => si.TargetType == targetType);
                if (index >= 0)
                {
                    selectedStackInformation = index;
                    return typedStackInformation[index].SymbolName;
                }

                var symbolName = string.Format(
                    "__stack{0}_{1}",
                    stackPointer,
                    typedStackInformation.Count);

                var stackInformation = new SymbolInformation(symbolName, targetType);
                selectedStackInformation = typedStackInformation.Count;
                typedStackInformation.Add(stackInformation);

                return symbolName;
            }

            public SymbolInformation GetCurrent()
            {
                Debug.Assert(selectedStackInformation >= 0);
                Debug.Assert(typedStackInformation.Any());

                return typedStackInformation[selectedStackInformation];
            }

            public IEnumerable<SymbolInformation> ExtractStacks()
            {
                return typedStackInformation;
            }
        }

        private struct BranchTargetInformation
        {
            public readonly int ILByteIndex;
            public readonly SymbolInformation[] StackInformationsSnapshot;

            public BranchTargetInformation(
                int ilByteIndex,
                int stackInformationsPosition,
                List<StackInformationHolder> stackInformationHolderList)
            {
                this.ILByteIndex = ilByteIndex;
                this.StackInformationsSnapshot = stackInformationHolderList
                    .Take(stackInformationsPosition)
                    .Select(stackInformationList => stackInformationList.GetCurrent())
                    .ToArray();
            }
        }
        #endregion

        #region Fields
        public readonly string MethodName;
        public readonly TypeReference ReturnType;
        public readonly Parameter[] Parameters;
        public readonly VariableDefinition[] Locals;
        public readonly IPrepareContext prepareContext;

        private readonly byte[] ilBytes;
        private int ilByteIndex = -1;

        private int decodingPathNumber = 0;
        private readonly int[] decodedPathNumbersAtILByteIndex;

        private readonly List<StackInformationHolder> stackList =
            new List<StackInformationHolder>();
        private int stackPointer = -1;
        private readonly Dictionary<int, string> labelNames = 
            new Dictionary<int, string>();
        private readonly Queue<BranchTargetInformation> pathRemains =
            new Queue<BranchTargetInformation>();
        #endregion

        public DecodeContext(
            string methodName,
            TypeReference returnType,
            Parameter[] parameters,
            VariableDefinition[] locals,
            Instruction[] ilBytes,
            IPrepareContext prepareContext)
        {
            if (ilBytes.Length == 0)
            {
                throw new InvalidProgramSequenceException(
                    "Method body is empty: MethodName={0}",
                    methodName);
            }

            this.MethodName = methodName;
            this.ReturnType = returnType;
            this.Parameters = parameters;
            this.Locals = locals;

            this.prepareContext = prepareContext;

            //TODO:CECIL
            //this.ilBytes = ilBytes;
            this.decodedPathNumbersAtILByteIndex = new int[ilBytes.Length];

            // First valid process is TryDequeueNextPath.
            this.pathRemains.Enqueue(new BranchTargetInformation(0, 0, new List<StackInformationHolder>()));
        }

        #region Decode
        public bool TryDecode(out ILConverter ilc)
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            // Finish if current position already decoded.
            if (decodedPathNumbersAtILByteIndex[ilByteIndex] >= 1)
            {
                ilc = null;
                return false;
            }

            decodedPathNumbersAtILByteIndex[ilByteIndex] = decodingPathNumber;

            // Decode next bytes.
            var byte0 = ilBytes[ilByteIndex];
            if (Utilities.TryGetILConverter(byte0, out ilc))
            {
                ilByteIndex++;
                return true;
            }

            if (ilByteIndex >= ilBytes.Length)
            {
                throw new InvalidProgramSequenceException(
                    "End of method body reached: MethodName={0}",
                    this.MethodName);
            }

            ilByteIndex++;

            var byte1 = ilBytes[ilByteIndex];
            var word = (ushort)(byte0 << 8 | byte1);
            if (Utilities.TryGetILConverter(word, out ilc))
            {
                ilByteIndex++;
                return true;
            }

            throw new InvalidProgramSequenceException(
                "Invalid opcode: MethodName={0}, OpCode={1:x2}, CurrentIndex={2}",
                this.MethodName,
                byte0,
                ilByteIndex);
        }
        #endregion

        #region Fetch
        public byte FetchByte()
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            if (ilByteIndex >= ilBytes.Length)
            {
                throw new InvalidProgramSequenceException(
                    "End of method body reached: MethodName={0}",
                    this.MethodName);
            }

            return ilBytes[ilByteIndex++];
        }

        public sbyte FetchSByte()
        {
            return (sbyte) this.FetchByte();
        }

        public ushort FetchUInt16()
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            var byte0 = this.FetchByte();
            var byte1 = this.FetchByte();
            return (ushort)((((ushort)byte1) << 8) | byte0);
        }

        public uint FetchUInt32()
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            var ushort0 = this.FetchUInt16();
            var ushort1 = this.FetchUInt16();
            return (uint)((((uint)ushort1) << 16) | ushort0);
        }

        public int FetchInt32()
        {
            return (int)this.FetchUInt32();
        }

        public ulong FetchUInt64()
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            var uint0 = this.FetchUInt32();
            var uint1 = this.FetchUInt32();
            return (ulong)((((ulong)uint1) << 32) | uint0);
        }

        public long FetchInt64()
        {
            return (long)this.FetchUInt64();
        }
        #endregion

        #region Label
        public Label MakeLabel()
        {
            return new Label(ilByteIndex);
        }
        #endregion

        #region ILByteIndex
        public int ILByteIndex => ilByteIndex;

        public void TransferIndex(int offset)
        {
            var newIndex = ilByteIndex + offset;
            this.SetIndex(newIndex);
        }

        public void SetIndex(int newIndex)
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            if ((newIndex >= this.ilBytes.Length) || (newIndex < 0))
            {
                throw new InvalidProgramSequenceException(
                    "Invalid branch target: Method={0}, Target={1}, CurrentIndex={2}",
                    this.MethodName,
                    newIndex,
                    ilByteIndex);
            }

            ilByteIndex = newIndex;
        }
        #endregion

        #region Stack
        public string PushStack(TypeReference targetType)
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            Debug.Assert(!CecilHelper.UInt64Type.Equals(targetType));
            Debug.Assert(!CecilHelper.UInt32Type.Equals(targetType));

            Debug.Assert(!CecilHelper.ByteType.Equals(targetType));
            Debug.Assert(!CecilHelper.SByteType.Equals(targetType));
            Debug.Assert(!CecilHelper.Int16Type.Equals(targetType));
            Debug.Assert(!CecilHelper.UInt16Type.Equals(targetType));

            Debug.Assert(!CecilHelper.BooleanType.Equals(targetType));

            StackInformationHolder stackInformationHolder;
            if (stackPointer >= stackList.Count)
            {
                stackInformationHolder = new StackInformationHolder(stackPointer);
                stackList.Add(stackInformationHolder);
            }
            else
            {
                stackInformationHolder = stackList[stackPointer];
            }

            stackPointer++;

            return stackInformationHolder.GetOrAdd(targetType);
        }

        public SymbolInformation PopStack()
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            if (stackPointer <= 0)
            {
                throw new InvalidProgramSequenceException(
                    "Evaluation stack underflow: Method={0}, CurrentIndex={1}",
                    this.MethodName,
                    ilByteIndex);
            }

            stackPointer--;
            return stackList[stackPointer].GetCurrent();
        }
        #endregion

        #region Path
        public string EnqueueNewPath(int branchTargetIndex)
        {
            Debug.Assert(decodingPathNumber >= 1);
            Debug.Assert(stackList != null);
            Debug.Assert(stackPointer >= 0);

            pathRemains.Enqueue(new BranchTargetInformation(
                branchTargetIndex, stackPointer, stackList));

            if (labelNames.TryGetValue(
                branchTargetIndex, out var labelName) == false)
            {
                labelName = string.Format("L_{0:x4}", labelNames.Count);
                labelNames.Add(branchTargetIndex, labelName);
            }

            return labelName;
        }

        public bool TryDequeueNextPath()
        {
            // Finish if remains path is empty.
            while (pathRemains.Count >= 1)
            {
                // Get queued branch target.
                var branchTarget = pathRemains.Dequeue();

                // If current position already decoded:
                if (decodedPathNumbersAtILByteIndex[branchTarget.ILByteIndex] >= 1)
                {
                    // Skip if stack information equals.
                    var currentStacks = stackList
                        .Take(stackPointer)
                        .Select(si => si.GetCurrent());
                    var branchTargetStacks = branchTarget.StackInformationsSnapshot;
                    if (currentStacks.SequenceEqual(branchTargetStacks))
                    {
                        continue;
                    }
                }

                // Start next path.
                decodingPathNumber++;
                ilByteIndex = branchTarget.ILByteIndex;

                // Retreive stack informations.
                for (var index = 0;
                    index < branchTarget.StackInformationsSnapshot.Length;
                    index++)
                {
                    stackList[index]
                        .GetOrAdd(branchTarget.StackInformationsSnapshot[index].TargetType);
                }
                stackPointer = branchTarget.StackInformationsSnapshot.Length;

                return true;
            }

            ilByteIndex = -1;
            return false;
        }
        #endregion

        #region Resolvers
        public TypeReference ResolveType(int typeToken)
        {
            return null; // TODO: Remove
        }

        public FieldDefinition ResolveField(int fieldToken)
        {
            return null; // TODO: Remove
        }

        public MethodDefinition ResolveMethod(int methodToken)
        {
            return null; // TODO: Remove
        }
        #endregion

        #region Extractors
        public IEnumerable<SymbolInformation> ExtractStacks()
        {
            return stackList
                .SelectMany(stackInformations => stackInformations.ExtractStacks());
        }

        public IReadOnlyDictionary<int, string> ExtractLabelNames()
        {
            return labelNames;
        }
        #endregion
    }
}
