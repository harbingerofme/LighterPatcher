using BepInEx;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;

namespace LighterPatcher
{
    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs => CollectTargetDLLs();

        private static BepInEx.Logging.ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("LighterHook");

        public static string[] ResolveDirectories { get; set; } =
        {
            Paths.PluginPath
        };

        private static IEnumerable<string> CollectTargetDLLs()
        {
            Logger.LogInfo("Collecting all plugins");

            List<string> results = new List<string>();

            var imm = new IncrementalMd5Maker();
            AssemblyDefinition mmh = null;

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
                            mmh = ass;
                            continue;
                        }
                        foreach (var refer in ass.MainModule.AssemblyReferences)
                        {
                            if (refer.Name == "MMHOOK_Assembly-CSharp")
                            {
                                imm.Step(pluginDll + ass.FullName);
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
                return results;
            }


            if (mmh == null)
            {
                Logger.LogMessage("No MMHOOK found. I can't make lighter what's already 0.");
                return new string[0];
            }

            ulong hash = imm.Finalize();
            imm.Clear();
            Logger.LogMessage($"Found {results.Count} mods with a MMHook dependency. Hash: {hash}");

            if (mmh.MainModule.FileName == $"LighterHook-{hash}.dll")
            {
                Logger.LogMessage("Lighterhook up to date! If you believe you see this in error, delete the Lighterhook.dll in your plugins and restart.");
                return new string[0];
            }

            string[] mmhooks = Directory.GetFiles(Paths.PluginPath, "MMHOOK_Assembly-CSharp.dl*", SearchOption.AllDirectories);

            if (mmhooks.Length < 1)
            {
                Logger.LogFatal("No MMHook file found to patch");
                return new string[0];
            }

            if (mmh.MainModule.FileName.StartsWith("LighterHook"))
            {
                Logger.LogMessage("Found an Old LighterHook, rebuilding.");
                mmh.Dispose();
                mmh = AssemblyDefinition.ReadAssembly(mmhooks[0]);
            }

            string buildLocation = Path.Combine(Paths.PluginPath, $"LighterHook-{hash}.dll");
            Logger.LogDebug($"Writing MMHOOK to {buildLocation}");
            mmh.Write(buildLocation);
            Logger.LogDebug("Finsihed writing building file");
            return results;
        }

        public static void Patch(AssemblyDefinition assemblyDefinition)
        {

        }
    }
}
