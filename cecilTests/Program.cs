using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cecilTests
{
    class Program
    {
        static void Main(string[] args)
        {
            var a = AssemblyDefinition.ReadAssembly(args[0]);
            List<TypeDefinition> list = a.MainModule.Types.ToList();
            foreach (TypeDefinition type in list.OrderBy(x => x.FullName))
            {
                Console.WriteLine(type.FullName);
            }
        }
    }
}
