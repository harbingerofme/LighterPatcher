using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
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

            var i = operand.IndexOf("On.");
            if (i != -1)
            {
                var j = operand.IndexOf("::");
                string completeClass = operand.Substring(i, j - i);
                LightestPatcher.Logger.LogDebug($"Call On: {completeClass}");
                ResolvePotentiallyNestedType(completeClass, ref typeList);

            }

            i = operand.IndexOf("IL.");
            if(i != -1)
            {
                var j = operand.IndexOf("::");
                string completeClass = operand.Substring(i, j - i);
                LightestPatcher.Logger.LogDebug($"Call IL: {completeClass}");
                ResolvePotentiallyNestedType(completeClass, ref typeList);
            }
        }

        public static void GetNeededType(this Collection<ParameterDefinition> parameterDefinitions, ref List<string> typeList)
        {
            if (parameterDefinitions.Count != 0 && parameterDefinitions[0].ParameterType.FullName.StartsWith("On."))
            {
                string s = parameterDefinitions[0].ParameterType.FullName;
                s = s.Substring(0, s.IndexOf('/'));
                LightestPatcher.Logger.LogDebug($"Parameter: {s}");
                ResolvePotentiallyNestedType(s, ref typeList);
            }
        }


        public static void ResolvePotentiallyNestedType(string typeName, ref List<string> typeList)
        {
            var classes = typeName.Split('/');
            var requiredOn = classes[0].Replace("IL.", "On.");
            var requiredIL = classes[0].Replace("On.", "IL.");
            typeList.UAdd(requiredOn);
            typeList.UAdd(requiredIL);
            for (int x = 1; x < classes.Length; x++)
            {
                requiredOn += "/" + classes[x];
                requiredIL += "/" + classes[x];
                typeList.UAdd(requiredOn);
                typeList.UAdd(requiredIL);
            }
            return;
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
