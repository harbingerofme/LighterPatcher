LighterPatcher is an adaptation of [@xiaoxiao921](https://github.com/xiaoxiao921)'s LighterHook. 
It aims to reduce the weight of [MonoMod](https://github.com/MonoMod/MonoMod) RuntimeDetour's generated MMHook file as a [BepInEx](https://github.com/BepInEx) patcher.

# Talk simple to me
`MMHOOK_Assembly-CSharp.dll` sometimes makes games slow. This patcher makes MMHOOK_Assembly-CSharp the smallest it can be.

# Talk simpler to me
Patcher make make computer go faster.

# Installation: 
Put the `LighterPatcher.dll` into `BepInEx\Patchers` folder.

# Talk advanced to me
`MMHOOK_Assembly-Csharp` contains a lot of extranous types that take a lot of processing power to handle, this patcher strips all types that aren't required by any plugins.

The process to do so is as follows:

1. Scan all dll files in the `BepInEx\Plugins` folder, and when that finds a mod with a reference to `MMHOOK_Assembly-CSharp.dll`, scan all methods of that dll for references to `On.*` and `IL.*`.
2. Back up the original `MMHOOK_Assembly-CSharp.dll` to `MMHOOK_Assembly-CSharp.dll.backup`.
3. Sort all types needed, and all types present. 
4. Do a mergeSort-esque iteration over both lists and remove types in the 'original' list that not present in the 'needed' list.
    * Exapnd all nested types while doing so.
5. Write the stripped MMHOOK.

# Changelog:

* 1.0.2
    * Fix case where patcher would remove types needed by parameters of uncalled methods.
    * Failing to succesfully build will no longer leave the enviroment in an unstable state.
    * More expansive logging when set to 'Debug'.
    * Remove unused non-delegate nested types. (Thanks iDeath for pointing this out)
    * No longer expand nested types of unused types.

* 1.0.1
    * Fix case where patcher would fail to backup the mmhook because it already existed.

* 1.0.0
    * initial release