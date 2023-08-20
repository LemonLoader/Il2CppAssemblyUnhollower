using System;
using System.Collections.Generic;
using System.IO;
using Disarm;
using Disarm.InternalDisassembly;
using Iced.Intel;

namespace UnhollowerRuntimeLib.XrefScans
{
    public static class XrefScannerLowLevel
    {
        public unsafe static IEnumerable<IntPtr> JumpTargets(IntPtr codeStart)
        {
            return JumpTargetsImpl(Disassembler.Disassemble((byte*)codeStart, 1000, (ulong)codeStart.ToInt64(), new Disassembler.Options() { ContinueOnError = true, ThrowOnUnimplemented = false }));
        }

        private static IEnumerable<IntPtr> JumpTargetsImpl(IEnumerable<Arm64Instruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (IsUnconditionalBranch(instruction))
                {
                    yield return (IntPtr)instruction.BranchTarget;
                    if (instruction.Mnemonic == Arm64Mnemonic.B)
                        yield break;
                }
            }
        }

        public unsafe static IEnumerable<IntPtr> CallAndIndirectTargets(IntPtr pointer)
        {
            return CallAndIndirectTargetsImpl(Disassembler.Disassemble((byte*)pointer, 1024*1024, (ulong)pointer.ToInt64(), new Disassembler.Options() { ContinueOnError = true, ThrowOnUnimplemented = false }));
        }

        private static IEnumerable<IntPtr> CallAndIndirectTargetsImpl(IEnumerable<Arm64Instruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (IsCallOrJump(instruction))
                {
                    if (instruction.Op0Kind == Arm64OperandKind.ImmediatePcRelative)
                    {
                        yield return (IntPtr)(instruction.Op0Imm + (long)instruction.Address);
                        continue;
                    }
                }
            }
        }

        private static bool IsUnconditionalBranch(Arm64Instruction instruction)
        {
            return instruction.Mnemonic == Arm64Mnemonic.B;
        }

        private static bool IsCallOrJump(Arm64Instruction instruction)
        {
            return instruction.Mnemonic == Arm64Mnemonic.BL;
        }

        public static byte[] ASMBYTES = null;
    }
}