using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Decoder = UnhollowerRuntimeLib.XrefScans.XrefScanner.DecoderSettings;
using UnhollowerBaseLib;
using UnhollowerBaseLib.Attributes;
using Type = Il2CppSystem.Type;
using Disarm;
using Iced.Intel;

namespace UnhollowerRuntimeLib.XrefScans
{
    public static class XrefScanner
    {
        internal static unsafe Decoder DecoderForAddress(IntPtr codeStart, int lengthLimit = 1000)
        {
            return new DecoderSettings
            { 
                codeStart = (ulong)codeStart,
                transaction = CSHelper.GetAsyncId(),
                limit = lengthLimit
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct XrefScanImplNativeRes
        {
            public int type;
            public bool complete;
            public ulong target;
            public ulong codeStart;
        };

        [StructLayout(LayoutKind.Explicit)]
        internal struct DecoderSettings
        {
            [FieldOffset(0)]
            public ulong codeStart;
            [FieldOffset(8)]
            public ulong transaction;
            [FieldOffset(16)]
            public int limit;
        }

        public static unsafe IEnumerable<XrefInstance> XrefScan(MethodBase methodBase)
        {
            var fieldValue = UnhollowerUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(methodBase)?.GetValue(null);
            if (fieldValue == null) return Enumerable.Empty<XrefInstance>();

            var cachedAttribute = methodBase.GetCustomAttribute<CachedScanResultsAttribute>(false);
            if (cachedAttribute == null)
            {
                XrefScanMetadataRuntimeUtil.CallMetadataInitForMethod(methodBase);

                return XrefScanImpl(DecoderForAddress(*(IntPtr*)(IntPtr)fieldValue));
            }

            if (cachedAttribute.XrefRangeStart == cachedAttribute.XrefRangeEnd)
                return Enumerable.Empty<XrefInstance>();

            XrefScanMethodDb.CallMetadataInitForMethod(cachedAttribute);

            return XrefScanMethodDb.CachedXrefScan(cachedAttribute).Where(it => it.Type == XrefType.Method || XrefGlobalClassFilter(it.Pointer));
        }

        public static IEnumerable<XrefInstance> UsedBy(MethodBase methodBase)
        {
            var cachedAttribute = methodBase.GetCustomAttribute<CachedScanResultsAttribute>(false);
            if (cachedAttribute == null || cachedAttribute.RefRangeStart == cachedAttribute.RefRangeEnd)
                return Enumerable.Empty<XrefInstance>();

            return XrefScanMethodDb.ListUsers(cachedAttribute);
        }

        internal static IEnumerable<Arm64Instruction> disasm(ulong ptr)
        {
            unsafe
            {
                return Disassembler.Disassemble((byte*)ptr, 1000, ptr, new Disassembler.Options() { ContinueOnError = true, ThrowOnUnimplemented = false});
            }
        }

        internal static IEnumerable<XrefInstance> XrefScanImpl(Decoder decoder, bool skipClassCheck = false)
        {
            ulong codeStart = decoder.codeStart;
            var instructions = disasm(codeStart);
            foreach (var instruction in instructions)
            {
                if (instruction.Mnemonic == Arm64Mnemonic.RET)
                    yield break;

                // Call or Jmp?
                if (instruction.Mnemonic == Arm64Mnemonic.BL)
                {
                    var targetAddress = ExtractTargetAddress(instruction);
                    if (targetAddress != 0)
                        yield return new XrefInstance(XrefType.Method, (IntPtr)targetAddress, (IntPtr)instruction.Address);
                    continue;
                }

                if (instruction.Mnemonic == Arm64Mnemonic.B)
                    yield break;

                if (IsMoveMnemonic(instruction.Mnemonic))
                {
                    XrefInstance? result = null;
                    try
                    {
                        if (instruction.Op1Kind == Arm64OperandKind.Memory && instruction.MemIsPreIndexed)
                        {
                            var movTarget = (IntPtr)instruction.MemBase;
                            if (ExtractMemorySize(instruction) != (ulong)MemorySize.UInt64)
                                continue;

                            if (skipClassCheck || XrefGlobalClassFilter(movTarget))
                                result = new XrefInstance(XrefType.Global, movTarget, (IntPtr)instruction.Address);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogSupport.Error(ex.ToString());
                    }

                    if (result != null)
                        yield return result.Value;
                }
            }
        }

        internal static bool XrefGlobalClassFilter(IntPtr movTarget)
        {
            var valueAtMov = (IntPtr)Marshal.ReadInt64(movTarget);
            if (valueAtMov != IntPtr.Zero)
            {
                var targetClass = (IntPtr)Marshal.ReadInt64(valueAtMov);
                return targetClass == Il2CppClassPointerStore<string>.NativeClassPtr ||
                       targetClass == Il2CppClassPointerStore<Type>.NativeClassPtr;
            }

            return false;
        }

        internal static bool IsMoveMnemonic(Arm64Mnemonic mnemonic)
        {
            return mnemonic == Arm64Mnemonic.MOV || (mnemonic >= Arm64Mnemonic.ADD && mnemonic <= Arm64Mnemonic.USUBW2);
        }

        internal static ulong ExtractTargetAddress(Arm64Instruction instruction)
        {
            switch (instruction.Op0Kind)
            {
                case Arm64OperandKind.Immediate:
                    return (ulong)instruction.Op0Imm;
                case Arm64OperandKind.ImmediatePcRelative:
                    return (ulong)((long)instruction.Address + instruction.Op0Imm);
                default:
                    return 0;
            }
        }

        public static ulong ExtractMemorySize(this Arm64Instruction instruction)
        {
            switch (instruction.MemExtendType)
            {
                case Arm64ExtendType.UXTB:
                case Arm64ExtendType.SXTB:
                    return 1; // Size of 1 byte

                case Arm64ExtendType.UXTH:
                case Arm64ExtendType.SXTH:
                    return 2; // Size of 2 bytes

                case Arm64ExtendType.UXTW:
                case Arm64ExtendType.SXTW:
                    return 4; // Size of 4 bytes

                case Arm64ExtendType.UXTX:
                case Arm64ExtendType.SXTX:
                    return 8; // Size of 8 bytes

                default:
                    return 8; // Default size of 8 bytes (64 bits)
            }
        }
    }
}