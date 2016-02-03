using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Ullink.NugetConverter.utils
{
    public static class GacUtil
    {
        [DllImport("fusion.dll")]
        private static extern IntPtr CreateAssemblyCache(
            out IAssemblyCache ppAsmCache,
            int reserved);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("e707dcde-d1cd-11d2-bab9-00c04f8eceae")]
        private interface IAssemblyCache
        {
            int Dummy1();

            [PreserveSig()]
            IntPtr QueryAssemblyInfo(
                int flags,
                [MarshalAs(UnmanagedType.LPWStr)] string assemblyName,
                ref AssemblyInfo assemblyInfo);

            int Dummy2();
            int Dummy3();
            int Dummy4();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AssemblyInfo
        {
            public int cbAssemblyInfo;
            public int assemblyFlags;
            public long assemblySizeInKB;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string currentAssemblyPath;

            public int cchBuf;
        }

        public static bool IsAssemblyInGAC(string assemblyName)
        {
            var assembyInfo = new AssemblyInfo { cchBuf = 512 };
            assembyInfo.currentAssemblyPath = new string('\0', assembyInfo.cchBuf);

            IAssemblyCache assemblyCache;

            var hr = CreateAssemblyCache(out assemblyCache, 0);

            if (hr == IntPtr.Zero)
            {
                hr = assemblyCache.QueryAssemblyInfo(
                    1,
                    assemblyName,
                    ref assembyInfo);

                if (hr != IntPtr.Zero)
                {
                    return false;
                }

                return true;
            }

            Marshal.ThrowExceptionForHR(hr.ToInt32());
            return false;
        }

        public static bool IsFullNameAssemblyInGAC(string assemblyFullName)
        {
            try
            {
                //One more hack, System.Reactive.* has 31bf3856ad364e35 signature
                //But it's not in the GAC
                if (assemblyFullName.Contains("System.Reactive"))
                    return false;
                //And an other one...
                if (assemblyFullName.Contains("Microsoft.Threading.Task"))
                    return false;

                // Silverlight & portable are ignored.
                if ((assemblyFullName.Contains("System.Core")
                     || assemblyFullName.Contains("System.Xml")
                     || assemblyFullName.Contains("System.Observable")
                     || assemblyFullName.Contains("System.Windows")
                     || assemblyFullName.Contains("System")
                     || assemblyFullName.Contains("mscorlib")) &&
                    (assemblyFullName.Contains("2.0.5.0")
                     || assemblyFullName.Contains("2.0.0.0")
                     || assemblyFullName.Contains("2.0.5.0")))
                    return true;

                //IsAssemblyInGac is fast, but somtime it failed
                if (assemblyFullName.Contains("b77a5c561934e089"))
                    return true;

                if (assemblyFullName.Contains("b03f5f7f11d50a3a"))
                    return true;
                
                //PresentationFramework 4.0.0.0
                if (assemblyFullName.Contains("31bf3856ad364e35"))
                    return true;

                if (assemblyFullName.Contains("89845dcd8080cc91"))
                    return true;

                //TODO : is this really needed ?
                if (assemblyFullName.Contains("FSharp.Core"))
                    return true;

                //TODO : is this really needed ?
                if (assemblyFullName.Contains("FSharp.Reactive"))
                    return true;

                return IsAssemblyInGAC(assemblyFullName);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAssemblyInGAC(Assembly assembly)
        {
            return assembly.GlobalAssemblyCache;
        }
    }
}
