using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System.Collections.Generic;

namespace LighterPatcher
{
    static class Helpers 
    {
        /// <summary>
        ///  Adds an element only if it's not already present in the list.
        /// </summary>
        /// <returns>If the element was already present in the list.</returns>
        public static bool UAdd<T>(this List<T> list, T toAdd)
        {
            bool flag = list.Contains(toAdd);
            if (!flag)
                list.Add(toAdd);
            return flag;
        }

        /// <summary>
        /// Gets the cumulative hashcode of all elements in the list. Very innacurate and should not be used as security, only checksums.
        /// </summary>
        public static long MakeContentHashCode<T>(this ICollection<T> list)
        {
            long output = 0;
            foreach(var entry in list)
            {
                output += entry.GetHashCode();
            }
            return output;
        }
    }
}
