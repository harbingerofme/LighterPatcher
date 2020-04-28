using BepInEx;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
                return new string[0];
            }

            if (modsWithRefs == 0)
            {
                Logger.LogMessage("No plugins to patch MMHook for.");
                mmhLocation = null;
                return new string[0];
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

            return new string[0];
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
                while(neededTypes.Count > 0 && index < types.Count)
                {
                    currentType = types[index];


                        
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
                }

                MarkAssembly(mmHook, hash);
                mmHook.Write(mmhLocation);
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
                method.Parameters.GetNeededType(ref neededTypes);
                if (!method.HasBody) continue;
                var instructions = method.Body.Instructions;
                foreach (var instruction in instructions)
                {
                    if (instruction.Operand == null) continue;
                    instruction.GetNeededTypes(ref neededTypes);
                }
            }
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
