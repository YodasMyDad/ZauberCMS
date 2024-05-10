using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;

namespace ZauberCMS.Core.Plugins;

public class AssemblyLoader : AssemblyLoadContext
{
    protected override Assembly? Load(AssemblyName? assemblyName)
    {
        if (assemblyName != null)
        {
            var deps = DependencyContext.Default;
            var res = deps?.CompileLibraries.Where(d => assemblyName.Name != null && d.Name.Contains(assemblyName.Name)).ToList();
            if (res != null)
            {
                var assembly = Assembly.Load(new AssemblyName(res.First().Name));
                return assembly;
            }
        }

        return null;
    }
}