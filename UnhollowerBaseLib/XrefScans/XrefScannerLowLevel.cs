using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.IO;
using UnhollowerBaseLib;
using Decoder = UnhollowerRuntimeLib.XrefScans.XrefScanner.DecoderSettings;

namespace UnhollowerRuntimeLib.XrefScans
{
    public static class XrefScannerLowLevel
    {

        public static IEnumerable<IntPtr> JumpTargets(IntPtr codeStart)
        {
            //LogSupport.Info("JumpTargets");

            //UnhollowerBaseLib.LogSupport.Info(System.Environment.StackTrace);

            var decoder = XrefScanner.DecoderForAddress(codeStart);
            //LogSupport.Info(decoder.limit.ToString());

            while (true)
            {
                //LogSupport.Info("request");
                IntPtr res = JumpTargetsImpl_Native(ref decoder);
                //LogSupport.Info(string.Format("0x{0:X8}", res));
                if (res == IntPtr.Zero)
                {
                    yield return res;
                    break;
                }
                yield return res;
            };
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern static IntPtr JumpTargetsImpl_Native(ref Decoder codeStart);

        public static IEnumerable<IntPtr> CallAndIndirectTargets(IntPtr pointer) {
            throw new NotImplementedException("When porting this, I have no idea what this does.");
        }
    }
}