using Mono.Cecil.Cil;
using System.Collections.Generic;


namespace LighterPatcher
{
    static class Helpers 
    {
        /// <summary>
        /// Gets the needed types from the instruction if it's an `On.` or an `Il.` call.
        /// </summary>
        public static void GetNeededTypes(this Instruction instruction, ref List<string> typeList)
        {
            if (instruction.OpCode != OpCodes.Call)
                return;
            var operand = instruction.Operand.ToString();

            string CompleteClass;

            var i = operand.IndexOf("On.");
            if (i != -1)
            {
                GetTypesFromIndex(ref typeList);
            }

            i = operand.IndexOf("Il.");
            if(i != -1)
            {
                GetTypesFromIndex(ref typeList);
            }

            void GetTypesFromIndex(ref List<string> types)
            {

                var j = operand.IndexOf("::");
                CompleteClass = operand.Substring(i, j - i);
                var classes = CompleteClass.Split('/');
                var requiredOn = classes[0].Replace("Il.", "On.");
                var requiredIL = classes[0].Replace( "On.", "Il.");
                types.UAdd(requiredOn);
                types.UAdd(requiredIL);
                for (int x = 1; x < classes.Length; x++)
                {
                    requiredOn += "/" + classes[x];
                    requiredIL += "/" + classes[x];
                    types.UAdd(requiredOn);
                    types.UAdd(requiredIL);
                }
                return;
            }
        }

        /// <summary>
        ///  Adds an element only if it's not already present in the list.
        /// </summary>
        /// <returns>If the element was already present in the list.</returns>
        static bool UAdd<T>(this List<T> list, T toAdd)
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
