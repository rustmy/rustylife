using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using Oxide.Plugins;
using RustyCore.Utils;

namespace RustyCore.Plugins
{
    [Info("BaseCore","bazuka5801", "1.0.0")]
    internal class BaseCore : RustPlugin
    {
        public static BaseCore m_Instance;
        private static Command cmd = Interface.Oxide.GetLibrary<Command>(  );
        private static RCore core = Interface.Oxide.GetLibrary<RCore>(  );

        private void Service()
        {
            Cooldowns.Service();
        }

        private void OnServerInitialized()
        {
            m_Instance = this;
            ImageStorage.Init();
            Cooldowns.Load();
            timer.Every( 1f, Service );
        }

        private void Unload()
        {
            Cooldowns.Save();
        }

        [HookMethod( "OnPluginLoaded" )]
        void OnPluginLoaded( Plugin plugin )
        {
            RustyLang.OnPluginLoaded(plugin);
        }


        public static RCore GetCore() => core;
        public static Command GetCmd() => cmd;
        public static PluginTimers GetTimer() => m_Instance.timer;
    }
}
