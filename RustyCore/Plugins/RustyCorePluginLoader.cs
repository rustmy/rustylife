using System;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Logging;
using Oxide.Plugins;

namespace RustyCore.Plugins
{
    public class RustyCorePluginLoader : PluginLoader
    {
        private RustyCoreExtension coreExtension;

        private Logger logger;

        #region REFLECTION

        Dictionary<string,FieldInfo> plugins = new Dictionary<string, FieldInfo>();

        void LoadPlugin<T>(T plugin) where T : RustPlugin
        {
            var name = plugin.GetType().Name;
            var fields = typeof(RCore).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.Name == name)
                {
                    plugins.Add(name, field);
                    field.SetValue(coreExtension.core,(T)plugin);
                    break;
                }
            }
            this.LoadedPlugins.Add(name, plugin);
        }

        RustPlugin CreatePlugin(string name)
        {
            foreach (var type in CorePlugins)
            {
                if (type.Name == name)
                {
                    var plugin = (RustPlugin)Activator.CreateInstance(type);
                    return plugin;
                }
            }
            return null;
        }

        #endregion

        public RustyCorePluginLoader(RustyCoreExtension coreExtension)
        {
            this.coreExtension = coreExtension;
            this.logger = Interface.Oxide.RootLogger;
        }

        public new Type[] CorePlugins => new[] {typeof(CuiGenerator), typeof(NicknameFilter), typeof(PlayerFinder), typeof(HookSequence), typeof(Tracer), typeof(BaseCore), typeof(WipeManager) };

        public override IEnumerable<string> ScanDirectory(string str)
        {
            return new string[] { nameof(CuiGenerator), nameof(NicknameFilter), nameof(PlayerFinder), nameof(HookSequence), nameof(Tracer), nameof( BaseCore ), nameof( WipeManager ) };
        }

        public override Plugin Load(string directory, string name)
        {
            RustPlugin plugin = CreatePlugin(name);
            LoadPlugin(plugin);
            return plugin;
        }

        public override void Unloading(Plugin plugin)
        {
            plugins.Remove(plugin.Name);
            this.LoadedPlugins.Remove(plugin.Name);
        }
    }
}
