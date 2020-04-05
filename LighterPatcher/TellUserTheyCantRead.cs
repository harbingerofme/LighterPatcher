using BepInEx;

namespace LighterPatcher
{
    [BepInPlugin("com.harbingerofme.lighterhook", "lighterhook", "0.0.1")]
    class TellUserTheyCantRead : BaseUnityPlugin
    {
        public void Awake()
        {
            Logger.LogError("This .dll should be in your patchers folder. Read the installation instructions and try again!");
        }
    }
}
