using System;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Plugins;

namespace RustyCore.Plugins
{
    [Info( "WipeManager", "bazuka5801", "1.0.0" )]
    public class WipeManager : RustPlugin
    {
        private DynamicConfigFile wipeFile = Interface.Oxide.DataFileSystem.GetFile( "Temp/Wipe" );

        private DateTime wipeTime;

        void OnServerInitialized()
        {
            wipeTime = wipeFile.ReadObject<DateTime>();
            if (wipeTime == default( DateTime )) wipeTime = DateTime.Now;
        }

        void OnNewSave()
        {
            Interface.Call( "OnWipe" );
            if (DateTime.Now.Day <= 7)
            {
                Interface.Call( "OnGlobalWipe" );
            }
            wipeFile.WriteObject( DateTime.Now.ToString( "yyyy-MM-dd HH:mm" ) );
        }

        internal DateTime GetWipeTime()
        {
            if (wipeTime == default( DateTime )) OnServerInitialized();
            return wipeTime;
        }
    }
}
