using System;
using System.Runtime.CompilerServices;
using Decoder = UnhollowerRuntimeLib.XrefScans.XrefScanner.DecoderSettings;

namespace UnhollowerRuntimeLib.XrefScans
{
    internal static class XrefScanUtilFinder
    {
        public static IntPtr FindLastRcxReadAddressBeforeCallTo(IntPtr codeStart, IntPtr callTarget)
        {
            var decoder = XrefScanner.DecoderForAddress(codeStart);
            return FindLastRcxReadAddressBeforeCallTo_Native(ref decoder, callTarget);
        }

        public static IntPtr FindByteWriteTargetRightAfterCallTo(IntPtr codeStart, IntPtr callTarget)
        {
            var decoder = XrefScanner.DecoderForAddress(codeStart);
            return FindByteWriteTargetRightAfterCallTo_Native(ref decoder, callTarget);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static IntPtr FindLastRcxReadAddressBeforeCallTo_Native(ref Decoder codeStart, IntPtr callTarget);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static IntPtr FindByteWriteTargetRightAfterCallTo_Native(ref Decoder codeStart, IntPtr callTarget);
    }
}