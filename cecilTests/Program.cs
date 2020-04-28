using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace cecilTests
{
    class Program
    {
        static void Main(string[] args)
        {
            var a = AssemblyDefinition.ReadAssembly(args[0]);
            List<TypeDefinition> types = a.MainModule.Types.OrderBy(x => x.FullName).ToList();
            for (int index = 0; index < types.Count; index++)
            {
                var currentType = types[index];
                if (currentType.HasNestedTypes)
                {
                    types.InsertRange(index + 1, currentType.NestedTypes.ToList().OrderBy(x => x.FullName));
                }
                var methods = currentType.Methods;
                for(int methodIndex = 0; methodIndex < methods.Count; methodIndex++)
                {
                    var currentMethod = methods[methodIndex];
                    Console.WriteLine(currentMethod.Name);
                    foreach(var pd in currentMethod.Parameters)
                    {
                        Console.WriteLine($"\t{pd.Index}:{pd.ParameterType.FullName}");
                    }
                }
            }
        }

        public int NonStaticAdd(long a, int c)
        {
            a = Math.Max(a, int.MaxValue);
            long result = a + c;
            return (int)Math.Max(result, int.MaxValue);
        }
    }
}
