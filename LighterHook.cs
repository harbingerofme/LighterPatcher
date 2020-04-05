using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LighterHook
{
    internal static class LighterHook
    {
        private static void Main(string[] args)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            string pathIn = args[0];
            //string pathOut = Path.Combine(Path.GetDirectoryName(pathIn) ?? throw new InvalidOperationException(), "MMHOOK_" + Path.ChangeExtension(Path.GetFileName(pathIn), "dll"));
            //var mmHookAssembly = AssemblyDefinition.ReadAssembly(path + @"\R2API\MMHOOK_Assembly-CSharp.dll");

            if (!pathIn.Contains("ins"))
            {
                Console.WriteLine(@"[LighterHook] Couldn't locate the BepInEx\plugins\ folder. Please put MMHOOK_Assembly-CSharp.dll into the BepInEx\plugins\R2API folder, and drag and drop the MMHOOK File into the LighterHook executable.");
            }

            var pluginsPath = Directory.GetParent(pathIn).FullName;

            while (!pluginsPath.EndsWith("ins"))
            {
                pluginsPath = Directory.GetParent(pluginsPath).FullName;
            }
            Console.WriteLine("Current Plugins Folder Path : " + pluginsPath);

            var currentMD5 = Helper.CreateMD5ForFolder(pluginsPath);
            Console.WriteLine("Current Plugins Folder MD5 : " + currentMD5);

            bool alreadyLighted = false;
            var length = new FileInfo(pathIn).Length;
            if (currentMD5.Equals(Properties.Settings.Default.PluginFolderMD5) && length < 6000000)
            {
                alreadyLighted = true;

                Console.WriteLine("[LighterHook] Already lighted MMHook... Exiting");
                Console.ReadLine();
            }

            if (alreadyLighted)
                return;

            var mmHookAssembly = AssemblyDefinition.ReadAssembly(pathIn);
            var allPlugins = new List<AssemblyDefinition>();
            var allMethods = new Collection<MethodContainer>();

            var countMethodContainingHooks = 0;
            var countTotalHooks = 0;

            foreach (string dll in Directory.GetFiles(pluginsPath, "*.dll", SearchOption.AllDirectories))
            {
                if (!dll.ToLower().Contains("mmhook") && !dll.ToLower().Contains("mmbait"))
                {
                    try
                    {
                        Console.WriteLine(Path.GetFileName(dll));
                        allPlugins.Add(AssemblyDefinition.ReadAssembly(dll));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }

            }

            foreach (var assembly in allPlugins)
            {
                var hashSetMethodContainers = new HashSet<MethodContainer>();

                foreach (var method in assembly.MainModule
                    .GetTypes()
                    .SelectMany(t => t.Methods.Where(m => m.HasBody)).ToList())
                {
                    if (!method.HasBody) continue;
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
                            var alreadyExistings = hashSetMethodContainers.Where(container =>
                                container.Method.FullName.Equals(method.FullName)).ToArray();

                            if (alreadyExistings.Length == 1)
                            {
                                var alreadyExisting = alreadyExistings[0];
                                if (alreadyExisting != null)
                                {
                                    //hashSetMethodContainers.Remove(alreadyExisting);

                                    alreadyExisting.AddInstruction(instruction);
                                    hashSetMethodContainers.Add(alreadyExisting);
                                }
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
                    Console.WriteLine($"Method : {methodContainer.Method.FullName}");
                    //Console.WriteLine($"Type : {methodContainer.Method.DeclaringType.FullName} | Method : {methodContainer.Method.FullName}");
                    foreach (var instruction in methodContainer.Instructions)
                    {
                        Console.WriteLine($"\t{instruction.OpCode} \"{instruction.Operand}\"");
                        countTotalHooks++;
                    }

                    countMethodContainingHooks++;
                    Console.WriteLine("\n");
                }
                if (allMethods.Count == 0)
                    allMethods = new Collection<MethodContainer>(hashSetMethodContainers.ToList());
                else
                {
                    allMethods = new Collection<MethodContainer>(allMethods.Concat(hashSetMethodContainers).ToList());
                }
            }
            Console.WriteLine("[LighterHook] Number of methods containing hooks : " + countMethodContainingHooks);
            Console.WriteLine("[LighterHook] Number of hooks : " + countTotalHooks);
            Console.WriteLine("[LighterHook] Making new MMHook...");

            var mmhookRemainingTypes = new HashSet<TypeDefinition>();
            var onHookCounterPartCount = 0;

            foreach (var methodFromMm in mmHookAssembly.MainModule
                .GetTypes()
                .SelectMany(t => t.Methods.Where(m => m.HasBody)).ToList())
            {
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
                                            //Console.WriteLine("type added : " + typeDefinition.FullName + " | iltypeIntoOn : " + ilTypeIntoOnType);
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
                                            //Console.WriteLine(onTypeDef[0].FullName);
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

            Console.WriteLine("[LighterHook] Number of On Hooks added because of their IL counterpart : " + onHookCounterPartCount);

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

            mmHookAssembly.Write(pathIn + "a");
            mmHookAssembly.Dispose();

            File.Move(pathIn, pathIn + ".bak");
            File.Move(pathIn + "a", pathIn);

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            var elapsedTime = $"{ts.Seconds:00},{ts.Milliseconds / 10:00} seconds";
            Console.WriteLine("[LighterHook] Done in " + elapsedTime + " !");
            Properties.Settings.Default.PluginFolderMD5 = currentMD5;
            Properties.Settings.Default.Save();

            //if (System.Diagnostics.Debugger.IsAttached)
            //{
            Console.ReadLine();
            //}
        }
    }
}
