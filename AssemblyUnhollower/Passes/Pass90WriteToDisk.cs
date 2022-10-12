using System.Linq;
using System.Threading.Tasks;
using AssemblyUnhollower.Contexts;
using UnhollowerBaseLib;

namespace AssemblyUnhollower.Passes
{
    public static class Pass90WriteToDisk
    {
        public static void DoPass(RewriteGlobalContext context, UnhollowerOptions options)
        {
            // the original code sometimes caused a hard crash, don't know why, but this seems to have fixed it
            foreach (var asm in context.Assemblies.Where(it => !options.AdditionalAssembliesBlacklist.Contains(it.NewAssembly.Name.Name)))
            {
                string filePath = System.IO.Path.Combine(options.OutputDir, asm.NewAssembly.Name.Name + ".dll");
                LogSupport.Info($"Writing {asm.NewAssembly.Name.Name} to {filePath}");
                asm.NewAssembly.Write(filePath);
            }
        }
    }
}