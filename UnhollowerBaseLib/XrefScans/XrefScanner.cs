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

namespace UnhollowerRuntimeLib.XrefScans
{
    public static class XrefScanner
    {
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

        internal static IEnumerable<XrefInstance> XrefScanImpl(Decoder decoder, bool skipClassCheck = false) {
            XrefScanImplNativeRes res;
            do
            {
                res = new XrefScanImplNativeRes();

                XrefScanImpl_Native(ref decoder, skipClassCheck, ref res);

                if (res.complete)
                {
                    break;
                }

                //LogSupport.Info($"{((XrefType)res.type).ToString()} {res.complete} {string.Format("0x{0:X8} 0x{0:X8}", res.target, res.codeStart)}");

                yield return new XrefInstance((XrefType)res.type, (IntPtr)res.target, (IntPtr)res.codeStart);
            } while (!res.complete);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern static void XrefScanImpl_Native(ref Decoder decoder, bool skipClassCheck, ref XrefScanImplNativeRes nativeRes);

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
    }
}