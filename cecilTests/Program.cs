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
                Console.WriteLine($"{currentType.FullName} : HasNested:{currentType.HasNestedTypes} ");
                if (currentType.HasNestedTypes)//expand nested types.
                {
                    types.InsertRange(index + 1, currentType.NestedTypes.ToList().OrderBy(x => x.FullName));
                }
            }
        }
    }
}
