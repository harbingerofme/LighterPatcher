using BepInEx;
using Mono.Cecil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LighterPatcher
{
    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs => CollectTargetDLLs();

        private static BepInEx.Logging.ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("LighterHook");

        private static ulong countMethodContainingHooks, countTotalHooks;
        private static Collection<MethodContainer> allMethods;

        private static List<AssemblyDefinition> toCollectFrom = null;

        private static string mmh;

        public static string[] ResolveDirectories { get; set; } =
        {
            Paths.PluginPath
        };


        public static void Initialize()
        {
            allMethods = new Collection<MethodContainer>();
            toCollectFrom = new List<AssemblyDefinition>();
        }

        private static IEnumerable<string> CollectTargetDLLs()
        {
            
            Logger.LogInfo("Collecting all plugins");

            List<string> results = new List<string>();

            mmh = null;

            foreach (var pluginDll in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories))
            {
                Logger.LogDebug($"Checking {pluginDll}.");
                try
                {
                    using (var ass = AssemblyDefinition.ReadAssembly(pluginDll))
                    {
                        if (ass.Name.Name == "MMHOOK_Assembly-CSharp")
                        {
                            Logger.LogInfo("MMHOOK found!");
                            mmh = pluginDll;
                            continue;
                        }
                        foreach (var refer in ass.MainModule.AssemblyReferences)
                        {
                            if (refer.Name == "MMHOOK_Assembly-CSharp")
                            {
                                //imm.Step(pluginDll + ass.FullName);
                                results.Add(pluginDll);
                                break;
                            }
                        }
                    }
                }
                catch (Exception e) { Logger.LogError($"Error on: {pluginDll}"); Logger.LogError(e); }
            }

            if (results.Count == 0)
            {
                Logger.LogMessage("No plugins to patch MMHook for.");
                return new string[0];
            }


            if (mmh == null)
            {
                Logger.LogMessage("No MMHOOK found. I can't make lighter what's already 0.");
                return new string[0];
            }

            //ulong hash = imm.Finalize();
            //imm.Clear();

            Logger.LogMessage($"Found {results.Count} mods with a MMHook dependency.");// Hash: {hash}");
            /*var hashLocation = new FileInfo(Path.Combine(Path.GetDirectoryName(mmh), $"LighterHook-{hash}"));
            if (hashLocation.Exists)
            {
                Logger.LogMessage("Lighterhook up to date! If you believe you see this in error, delete the LighterHook-{hash} in your plugins and restart.");
                return new string[0];
            }
            */

            string[] disabledMMH = Directory.GetFiles(Paths.PluginPath, "MMHOOK_Assembly-CSharp.dll.disabled", SearchOption.AllDirectories);

            if (disabledMMH.Length > 0)
            {
                File.Delete(mmh);
                File.Move(disabledMMH[0], mmh);
            }

            Logger.LogDebug($"Backing up MMHOOK.");
            File.Move(mmh, mmh + ".disabled");

            foreach(string result in results)
            {
                toCollectFrom.Add(AssemblyDefinition.ReadAssembly(result));
            }

            return new string[0];
        }

        public static void Patch(AssemblyDefinition assemblyDefinition)
        {
            Logger.LogDebug($"Scanning {assemblyDefinition.Name.Name} for MMHook refs.");
            var hashSetMethodContainers = new HashSet<MethodContainer>();

            foreach (var method in assemblyDefinition.MainModule
                .GetTypes()
                .SelectMany(t => t.Methods.Where(m => m.HasBody)).ToList())
            {
                var instructions = method.Body.Instructions;
                foreach (var instruction in instructions)
                {
                    //Console.WriteLine($"\t{instruction.OpCode} \"{instruction.Operand}\"");
                    if (instruction.Operand == null) continue;

                    var ilHook = instruction.OpCode.ToString().ToLower().Contains("call") &&
                                  instruction.Operand.ToString().ToLower().Contains("ilcontext/manipulator");

                    var onHook = instruction.OpCode.ToString().ToLower().Contains("call") && instruction.Operand.ToString().Contains("On.");

                    if (ilHook || onHook)
                    {
                        var alreadyExisting = hashSetMethodContainers.FirstOrDefault(container =>
                            container.Method.FullName.Equals(method.FullName));

                        if (alreadyExisting != null)
                        {
                            //hashSetMethodContainers.Remove(alreadyExisting);

                            alreadyExisting.AddInstruction(instruction);
                            hashSetMethodContainers.Add(alreadyExisting);
                        }
                        else
                        {
                            hashSetMethodContainers.Add(new MethodContainer(method, instruction));
                        }
                    }
                }

            }

            foreach (var methodContainer in hashSetMethodContainers)
            {
                //Console.WriteLine($"Method : {methodContainer.Method.FullName}");
                //Console.WriteLine($"Type : {methodContainer.Method.DeclaringType.FullName} | Method : {methodContainer.Method.FullName}");
                foreach (var instruction in methodContainer.Instructions)
                {
                    Console.WriteLine($"\t{instruction.OpCode} \"{instruction.Operand}\"");
                    countTotalHooks++;
                }

                countMethodContainingHooks++;
                //Console.WriteLine("\n");
            }

            if (allMethods.Count == 0)
                allMethods = new Collection<MethodContainer>(hashSetMethodContainers.ToList());
            else
            {
                allMethods = new Collection<MethodContainer>(allMethods.Concat(hashSetMethodContainers).ToList());
            }
        }

        public static void Finish()
        {


            foreach (AssemblyDefinition assembly in toCollectFrom)
                Patch(assembly);

            Logger.LogInfo($"Number of methods containing hooks : {countMethodContainingHooks}");
            Logger.LogInfo($"Number of hooks : {countTotalHooks}" );
            Logger.LogInfo($"Making new MMHook...");

            var mmhookRemainingTypes = new HashSet<TypeDefinition>();
            var onHookCounterPartCount = 0;
            var mmHookAssembly = AssemblyDefinition.ReadAssembly(mmh+".disabled");

            Logger.LogDebug($"Scanned in disabled mmHook");
            int max = mmHookAssembly.MainModule.Types.Count;
            int counter = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var innerWatch = new Stopwatch();
            foreach (var mmType in mmHookAssembly.MainModule.Types)
            {
                if (counter % 10 == 0)
                    Logger.LogDebug($"[{stopwatch.Elapsed.TotalMilliseconds}]Types parsed: {counter} / {max}");
                counter++;
                foreach (var methodFromMm in mmType.Methods)
                {
                    if (methodFromMm.HasBody == false)
                        continue;
                    //Logger.LogDebug($"Parsing method: {methodFromMm.Name}");
                    foreach (var methodContainer in allMethods)
                    {
                        foreach (var instruction in methodContainer.Instructions)
                        {
                            if (instruction.Operand.ToString().Contains(methodFromMm.FullName.GetUntilOrEmpty("(")))
                            {
                                bool isIlHook = instruction.Operand.ToString().Contains("IL.");

                                if (isIlHook)
                                {
                                    var ilTypeIntoOnType = instruction.Operand.ToString().Replace("IL.", "On.").Replace("System.Void ", "").GetUntilOrEmpty(":");
                                    if (!mmhookRemainingTypes.Any(definition => definition.FullName.Contains(ilTypeIntoOnType)))
                                    {
                                        foreach (var typeDefinition in mmHookAssembly.MainModule.GetTypes())
                                        {
                                            if (typeDefinition.FullName.Equals(ilTypeIntoOnType))
                                            {
                                                mmhookRemainingTypes.Add(typeDefinition);
                                                Logger.LogDebug($"type added : {typeDefinition.FullName} | iltypeIntoOn : {ilTypeIntoOnType}");
                                                onHookCounterPartCount++;
                                                break;
                                            }
                                        }
                                    }
                                }

                                // handle nested types
                                var parentType = methodFromMm.DeclaringType.FullName.GetUntilOrEmpty("/");
                                if (parentType.Length > 1 && !mmhookRemainingTypes.Any(definition => definition.FullName.Contains(parentType)))
                                {
                                    foreach (var typeDefinition in mmHookAssembly.MainModule.GetTypes())
                                    {
                                        if (typeDefinition.FullName.Contains(parentType))
                                        {
                                            mmhookRemainingTypes.Add(typeDefinition);

                                            var ilTypeIntoOnType2 = parentType.Replace("IL.", "On.");
                                            var onTypeDef = mmHookAssembly.MainModule.GetTypes()
                                                .Where(definition => definition.FullName.Equals(ilTypeIntoOnType2)).ToArray();
                                            if (onTypeDef.Length == 1)
                                            {
                                                mmhookRemainingTypes.Add(onTypeDef[0]);
                                                Logger.LogDebug(onTypeDef[0].FullName);
                                                onHookCounterPartCount++;
                                            }
                                            break;
                                        }
                                    }
                                }

                                if (!mmhookRemainingTypes.Any(definition => definition.FullName.Equals(methodFromMm.DeclaringType.FullName)))
                                {
                                    mmhookRemainingTypes.Add(methodFromMm.DeclaringType);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            stopwatch.Stop();
            Logger.LogInfo($"Time elapsed: {stopwatch.ElapsedMilliseconds} milliseconds");
            Logger.LogInfo($"Number of On Hooks added because of their IL counterpart : {onHookCounterPartCount}");

            var typeToRemove = new List<TypeDefinition>();
            foreach (var type in mmHookAssembly.MainModule.Types)
            {
                if (!typeToRemove.Any(definition => definition.FullName.Equals(type.FullName)))
                {
                    if (!mmhookRemainingTypes.Any(definition => definition.FullName.Equals(type.FullName)))
                    {
                        typeToRemove.Add(type);
                    }
                }
            }

            for (int i = 0; i < typeToRemove.Count; i++)
            {
                mmHookAssembly.MainModule.Types.Remove(typeToRemove[i]);
            }

            Logger.LogMessage("Writing LighterHook");
            mmHookAssembly.Write(mmh);
        }

        public static string GetUntilOrEmpty(this string text, string stopAt = "-")
        {
            if (!String.IsNullOrWhiteSpace(text))
            {
                int charLocation = text.IndexOf(stopAt, StringComparison.Ordinal);

                if (charLocation > 0)
                {
                    return text.Substring(0, charLocation);
                }
            }

            return String.Empty;
        }
    }
}
