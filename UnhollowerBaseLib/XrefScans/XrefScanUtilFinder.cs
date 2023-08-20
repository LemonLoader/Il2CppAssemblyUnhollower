using Disarm.InternalDisassembly;
using Disarm;
using System;
using System.Runtime.CompilerServices;
using Decoder = UnhollowerRuntimeLib.XrefScans.XrefScanner.DecoderSettings;

namespace UnhollowerRuntimeLib.XrefScans
{
    internal static class XrefScanUtilFinder
    {
        public unsafe static IntPtr FindLastRcxReadAddressBeforeCallTo(IntPtr codeStart, IntPtr callTarget)
        {
            IntPtr lastRcxRead = IntPtr.Zero;

            var instructions = Disassembler.Disassemble((byte*)codeStart, 1000, (ulong)codeStart.ToInt64(), new Disassembler.Options() { ContinueOnError = true, ThrowOnUnimplemented = false });
            foreach (var instruction in instructions)
            {
                if (instruction.Mnemonic == Arm64Mnemonic.RET)
                    return IntPtr.Zero;

                if (instruction.Mnemonic == Arm64Mnemonic.B)
                    continue;

                if (instruction.Mnemonic == Arm64Mnemonic.BL)
                {
                    var target = instruction.BranchTarget;
                    if ((IntPtr)target == callTarget)
                        return lastRcxRead;
                }

                if (instruction.Mnemonic == Arm64Mnemonic.MOV)
                {
                    if (instruction.Op0Kind == Arm64OperandKind.Register && instruction.Op0Reg == Arm64Register.X9 && instruction.Op1Kind == Arm64OperandKind.Memory && instruction.MemIsPreIndexed)
                    {
                        var movTarget = (IntPtr)instruction.MemOffset;
                        if (instruction.ExtractMemorySize() != 4) // Check if memory size is not 32 bits
                            continue;

                        lastRcxRead = movTarget;
                    }
                }
            }

            return IntPtr.Zero;
        }

        public unsafe static IntPtr FindByteWriteTargetRightAfterCallTo(IntPtr codeStart, IntPtr callTarget)
        {
            var seenCall = false;

            var instructions = Disassembler.Disassemble((byte*)codeStart, 1000, (ulong)codeStart.ToInt64(), new Disassembler.Options() { ContinueOnError = true, ThrowOnUnimplemented = false });
            foreach (var instruction in instructions)
            {
                if (instruction.Mnemonic == Arm64Mnemonic.RET)
                    return IntPtr.Zero;

                if (instruction.Mnemonic == Arm64Mnemonic.B)
                    continue;

                if (instruction.Mnemonic == Arm64Mnemonic.BL)
                {
                    var target = instruction.BranchTarget;
                    if ((IntPtr)target == callTarget)
                        seenCall = true;
                }

                if (instruction.Mnemonic == Arm64Mnemonic.MOV && seenCall)
                {
                    if (instruction.Op0Kind == Arm64OperandKind.Memory && (instruction.ExtractMemorySize() == 1 || instruction.ExtractMemorySize() == 2))
                        return (IntPtr)instruction.MemOffset;
                }
            }

            return IntPtr.Zero;
        }
    }
}