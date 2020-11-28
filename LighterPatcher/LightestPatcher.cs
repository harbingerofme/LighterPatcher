using BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LighterPatcher
{
    static class LightestPatcher
    {
        /// Because BepInEx patchers need to implement a few methods, they are here in the order they are called in. 
        /// We could put everything in the Initialize, as we need to manually iterate over the plugins anyway.
        /// Maybe in the future BepInEx will support patching plugins.

        private static List<string> neededTypes;
        private static string mmhLocation;
        private static long hash;
        internal static BepInEx.Logging.ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("LighterPatcher");

        public static void Initialize()
        {
            neededTypes = new List<string>();
        }

        public static IEnumerable<string> TargetDLLs => TellBepinAbsolutelyNothingBecauseThePluginsFolderIsntManaged();

        private static IEnumerable<string> TellBepinAbsolutelyNothingBecauseThePluginsFolderIsntManaged()
        {
            Logger.LogInfo($"Collecting information for new MMHook");
            string oldHash = null;
            int modsWithRefs = 0;
            foreach (var pluginDll in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    using (var ass = AssemblyDefinition.ReadAssembly(pluginDll))
                    {
                        if (ass.Name.Name == "MMHOOK_Assembly-CSharp")
                        {
                            oldHash = CollectHash(ass);

                            mmhLocation = pluginDll;
                            continue;
                        }
                        foreach (var refer in ass.MainModule.AssemblyReferences)
                        {
                            if (refer.Name == "MMHOOK_Assembly-CSharp")
                            {
                                CollectMethodDefinitions(ass);
                                modsWithRefs++;
                                break;
                            }
                        }
                    }
                }
                catch (Exception e) { Logger.LogError($"Error on: {pluginDll}"); Logger.LogError(e); }
            }

            if (mmhLocation == null)
            {
                Logger.LogMessage("No MMHOOK found. I can't make lighter what's already 0.");
                return Array.Empty<string>();
            }

            if (modsWithRefs == 0)
            {
                Logger.LogMessage("No plugins to patch MMHook for.");
                mmhLocation = null;
                return Array.Empty<string>();
            }


            Logger.LogMessage($"Found {modsWithRefs} mods with a MMHook dependency.");
            Logger.LogInfo($"Number of neededtypes for hooks : {neededTypes.Count}");
            neededTypes.Sort();
            hash = neededTypes.MakeContentHashCode();

            if (oldHash == hash.ToString())
            {
                Logger.LogMessage($"LighterhHook has already run for these mods. Using that old file again.");
                mmhLocation = null;
            }
            else
            {
                if (oldHash == null)
                {
                    Logger.LogDebug("Backing up vanilla MMHook");
                    File.Delete(mmhLocation + ".backup");
                    File.Move(mmhLocation, mmhLocation + ".backup");
                }
            }

            return Array.Empty<string>();
        }

        //Since BepInEx doesn't return plugins to us, this method is officially pointless.
        public static void Patch(AssemblyDefinition assemblyDefinition)
        {
            Logger.LogError($"How did you even end up here? Here's the definition: {assemblyDefinition.FullName}");
        }

        public static void Finish()
        {
            if (mmhLocation == null)
                return;//Nothing to patch.


            using (AssemblyDefinition mmHook = AssemblyDefinition.ReadAssembly(mmhLocation + ".backup"))
            {
                Logger.LogDebug("Stripping types.");

                Func<TypeDefinition, string> FullNameSelector = new Func<TypeDefinition, string>(td => td.FullName);
                var mTypes = mmHook.MainModule.Types;
                List<TypeDefinition> types = mTypes.ToList();
                types = types.OrderBy(FullNameSelector).ToList();

                int index = 0; TypeDefinition currentType;

                var debugTypes = neededTypes.ToArray();

                while (neededTypes.Count > 0 && index < types.Count)
                {
                    currentType = types[index];

                    if (!(currentType.FullName.StartsWith("On") || currentType.FullName.StartsWith("IL")))
                    {
                        Logger.LogDebug("Skip trimming '" + currentType.FullName + "' as it's not a Vanilla type");
                        index++;
                    }


                    if (currentType.FullName != neededTypes[0])
                    {
                        types.RemoveAt(index);
                        if (currentType.IsNested && currentType.BaseType.Name != nameof(MulticastDelegate))
                            currentType.DeclaringType.NestedTypes.Remove(currentType);
                        else
                            mTypes.Remove(currentType);
                        continue;
                    }
                    else
                    {
                        if (currentType.HasNestedTypes)//expand nested types.
                            types.InsertRange(index + 1, currentType.NestedTypes.ToList().OrderBy(FullNameSelector));

                        index++;
                        neededTypes.RemoveAt(0);
                    }
                }

                if (neededTypes.Count > 0)
                {
                    Logger.LogFatal("Couldn't find all needed types!");
                    Logger.LogMessage("Please report this! As a workaround, consider removing LighterPatcher!");
                    Logger.LogMessage("Using old backup mmHook");
                    File.Copy(mmhLocation + ".backup", mmhLocation);
                    mmhLocation += ".failed";
                    File.Delete(mmhLocation);
                    Logger.LogInfo($"Writing failed build to {mmhLocation}");
                    string tracefile = Path.Combine(Path.GetDirectoryName(mmhLocation), "LighterPatcherTrace.txt");
                    StringBuilder trace = new StringBuilder();
                    trace.AppendLine("Couldn't find all needed types!");
                    trace.AppendLine($"(First) missing type: {neededTypes[0]}");
                    trace.AppendLine("All needed types:");
                    trace.Append('\t');
                    trace.Append(string.Join("\n\t", debugTypes));
                    Logger.LogInfo($"Writing a trace to {tracefile}");
                    File.Delete(tracefile);
                    File.WriteAllText(tracefile, trace.ToString());
                    trace = null;
                }

                MarkAssembly(mmHook, hash);
                mmHook.Write(mmhLocation);
                debugTypes = null;
            }
        }


        private static string CollectHash(AssemblyDefinition MMHook)
        {
            var hashType = MMHook.MainModule.Types.FirstOrDefault((td) => td.Namespace == "LighterPatcher");
            if (hashType != null)
            {
                string oldHash = hashType.Name.Substring(4);
                Logger.LogInfo($"Lighter MMHOOK found!(hash:{oldHash})");
                return oldHash;
            }
            else
            {
                Logger.LogInfo("Vanilla MMHOOK found!");
                return null;
            }
        }

        private static void CollectMethodDefinitions(AssemblyDefinition assembly)
        {
            Logger.LogInfo($"Collecting methods from: {assembly.Name.Name}");

            foreach (var method in assembly.MainModule
                .GetTypes()
                .SelectMany(t => t.Methods).ToList())
            {
                GetNeededType(method.Parameters);
                if (!method.HasBody) continue;
                var instructions = method.Body.Instructions;
                foreach (var instruction in instructions)
                {
                    if (instruction.Operand == null) continue;
                    if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                        continue;

                    GetNeededTypes(instruction, "On.");
                    GetNeededTypes(instruction, "IL.");
                }
            }

            void GetNeededTypes(Instruction instruction, string nameSpace)
            {
                var operand = instruction.Operand.ToString();
                var i = operand.IndexOf(nameSpace);
                if (i != -1)
                {
                    var j = operand.IndexOf("::");
                    if (j > i)
                    {
                        string completeClass = operand.Substring(i, j - i);
                        Logger.LogDebug($"{instruction.OpCode.Name} {nameSpace}: {completeClass}");
                        ResolvePotentiallyNestedType(completeClass);
                        return;
                    }
                }
            }
        }

        public static void GetNeededType(Collection<ParameterDefinition> parameterDefinitions)
        {
            if (parameterDefinitions.Count != 0 && parameterDefinitions[0].ParameterType.FullName.StartsWith("On."))
            {
                string s = parameterDefinitions[0].ParameterType.FullName;
                s = s.Substring(0, s.IndexOf('/'));
                Logger.LogDebug($"Parameter: {s}");
                ResolvePotentiallyNestedType(s);
            }
        }

        private static void ResolvePotentiallyNestedType(string typeName)
        {
            var classes = typeName.Split('/');
            var requiredOn = classes[0].Replace("IL.", "On.");
            var requiredIL = classes[0].Replace("On.", "IL.");
            neededTypes.UAdd(requiredOn);
            neededTypes.UAdd(requiredIL);

            for (int x = 1; x < classes.Length; x++)
            {
                requiredOn += "/" + classes[x];
                requiredIL += "/" + classes[x];
                neededTypes.UAdd(requiredOn);
                if (classes[x].StartsWith("orig_") || classes[x].StartsWith("hook_"))
                {
                    continue;
                }
                neededTypes.UAdd(requiredIL);
            }
            return;
        }

        private static void MarkAssembly(AssemblyDefinition assembly, long hash)
        {
            var markerType = new TypeDefinition(
               "LighterPatcher",
               "Hash" + hash.ToString(),
               TypeAttributes.Class | TypeAttributes.Public,
               assembly.MainModule.ImportReference(typeof(object)));

            assembly.MainModule.Types.Add(markerType);
        }
    }
}
