LighterPatcher is an adaptation of [@xiaoxiao921](https://github.com/xiaoxiao921)'s LighterHook. 
It aims to reduce the weight of [MonoMod](https://github.com/MonoMod/MonoMod) RuntimeDetour's generated MMHook file as a [BepInEx](https://github.com/BepInEx) patcher.

# Talk simple to me
MMHOOK_Assembly-CSharp.dll sometimes makes games slow. This patcher makes MMHOOK_Assembly-CSharp the smallest it can be.

# Talk simpler to me
Thing make computer go zoom, maybe.

# Installation: 
Put the `LighterPatcher.dll` into `BepInEx\Patchers` folder.

# Talk advanced to me
First, the patcher scans all dll files in the `BepInEx\Plugins` folder, and when it finds a mod with a reference to `MMHOOK_Assembly-CSharp.dll`, it scans all methods of that dll for calls to `On.*` and `IL.*`.
Then it backs up the original `MMHOOK_Assembly-CSharp.dll` to `MMHOOK_Assembly-CSharp.dll.backup`.
Finally, it sorts all types needed, and all types present. Then it does a modified mergesort to remove all types no longer needed. 