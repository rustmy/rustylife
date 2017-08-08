using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Logging;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using Oxide.Plugins;
using System.Reflection;
using System;
namespace RustyCore.Plugins
{
    public class RustPlugin : CSPlugin
    {
        private CompoundLogger logger;
        protected PluginTimers timer;
        private Command cmd;

        protected Permission permission = Interface.Oxide.GetLibrary<Permission>();
        protected WebRequests webrequest = Interface.Oxide.GetLibrary<WebRequests>();
        protected Oxide.Game.Rust.Libraries.Rust rust = Interface.Oxide.GetLibrary<Oxide.Game.Rust.Libraries.Rust>(null);
        protected HashSet<CSharpPlugin.PluginFieldInfo> onlinePlayerFields = new HashSet<CSharpPlugin.PluginFieldInfo>();
        public RustPlugin()
        {
            logger = Interface.GetMod().RootLogger;
            timer = new PluginTimers(this);
            cmd = Interface.Oxide.GetLibrary<Command>(null);


            var type = GetType();
            foreach (var method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var info_attributes = method.GetCustomAttributes(typeof(HookMethodAttribute), true);
                if (info_attributes.Length > 0) continue;
                // Assume all private instance methods which are not explicitly hooked could be hooks
                if (method.DeclaringType != null && method.DeclaringType.Name == type.Name) AddHookMethod(method.Name, method);
            }

            SetPluginInfo(type);
        }

        public override void HandleAddedToManager(PluginManager manager)
        {
            FieldInfo[] fields = base.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < (int)fields.Length; i++)
            {
                FieldInfo fieldInfo = fields[i];
                if ((int)fieldInfo.GetCustomAttributes(typeof(OnlinePlayersAttribute), true).Length > 0)
                {
                    CSharpPlugin.PluginFieldInfo pluginFieldInfo = new CSharpPlugin.PluginFieldInfo(this, fieldInfo);
                    if ((int)pluginFieldInfo.GenericArguments.Length != 2 || pluginFieldInfo.GenericArguments[0] != typeof(BasePlayer))
                    {
                        Puts($"The {fieldInfo.Name} field is not a Hash with a BasePlayer key! (online players will not be tracked)");
                    }
                    else if (!pluginFieldInfo.LookupMethod("Add", pluginFieldInfo.GenericArguments))
                    {
                        Puts($"The {fieldInfo.Name} field does not support adding BasePlayer keys! (online players will not be tracked)");
                    }
                    else if (!pluginFieldInfo.LookupMethod("Remove", typeof(BasePlayer)))
                    {
                        Puts($"The {fieldInfo.Name} field does not support removing BasePlayer keys! (online players will not be tracked)");
                    }
                    else if (pluginFieldInfo.GenericArguments[1].GetField("Player") == null)
                    {
                        Puts($"The {pluginFieldInfo.GenericArguments[1].Name} class does not have a public Player field! (online players will not be tracked)");
                    }
                    else if (pluginFieldInfo.HasValidConstructor(typeof(BasePlayer)))
                    {
                        this.onlinePlayerFields.Add(pluginFieldInfo);
                    }
                    else
                    {
                        Puts($"The {fieldInfo.Name} field is using a class which contains no valid constructor (online players will not be tracked)");
                    }
                }
            }
            foreach (var method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attributes = method.GetCustomAttributes(typeof(ConsoleCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    var attribute = attributes[0] as ConsoleCommandAttribute;
                    if (attribute != null)
                        cmd.AddConsoleCommand(attribute.Command, this, method.Name);
                    continue;
                }

                attributes = method.GetCustomAttributes(typeof(ChatCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    var attribute = attributes[0] as ChatCommandAttribute;
                    if (attribute != null)
                        cmd.AddChatCommand(attribute.Command, this, method.Name);
                }
            }
            if (this.onlinePlayerFields.Count > 0)
            {
                foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                {
                    this.AddOnlinePlayer(basePlayer);
                }
            }
            base.HandleAddedToManager(manager);
        }

        public override void HandleRemovedFromManager(PluginManager manager)
        {
            if (base.IsLoaded)
            {
                base.CallHook("Unloaded", null);
                base.CallHook("Unload", null);
            }
            base.HandleRemovedFromManager(manager);
        }

        protected void NextTick(Action callback)
        {
            Interface.Oxide.NextTick(callback);
        }
        [HookMethod("OnPlayerDisconnected")]
        private void base_OnPlayerDisconnected(BasePlayer player)
        {
            NextTick(() => {
                foreach (CSharpPlugin.PluginFieldInfo onlinePlayerField in this.onlinePlayerFields)
                {
                    onlinePlayerField.Call("Remove", new object[] { player });
                }
            });
        }

        [HookMethod("OnPlayerInit")]
        private void base_OnPlayerInit(BasePlayer player)
        {
            this.AddOnlinePlayer(player);
        }

        private void AddOnlinePlayer(BasePlayer player)
        {
            foreach (CSharpPlugin.PluginFieldInfo onlinePlayerField in this.onlinePlayerFields)
            {
                Type genericArguments = onlinePlayerField.GenericArguments[1];
                object obj = (genericArguments.GetConstructor(new Type[] { typeof(BasePlayer) }) != null ? Activator.CreateInstance(genericArguments, new object[] { player }) : Activator.CreateInstance(genericArguments));
                genericArguments.GetField("Player").SetValue(obj, player);
                onlinePlayerField.Call("Add", player, obj);
            }
        }

        public void SetPluginInfo(System.Type type)
        {
            Name = type.Name;
            Filename = type.Name;

            var info_attributes = type.GetCustomAttributes(typeof(InfoAttribute), true);
            if (info_attributes.Length > 0)
            {
                var info = info_attributes[0] as InfoAttribute;
                Title = info.Title;
                Author = info.Author;
                Version = info.Version;
                ResourceId = info.ResourceId;
            }

            var description_attributes = type.GetCustomAttributes(typeof(DescriptionAttribute), true);
            if (description_attributes.Length > 0)
            {
                var info = description_attributes[0] as DescriptionAttribute;
                Description = info.Description;
            }

            var method = type.GetMethod("LoadDefaultConfig", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            HasConfig = method.DeclaringType != typeof(Plugin);
        }


        protected void Puts(string format, params object[] args)
        {
            Interface.Oxide.LogInfo("[{0}] {1}", Title,(args.Length <= 0 ? format : string.Format(format, args)));
        }

        protected void PrintWarning(string format, params object[] args)
        {
            Interface.Oxide.LogWarning("[{0}] {1}", Title, args.Length <= 0 ? format : string.Format(format, args));
        }

        protected void PrintError(string format, params object[] args)
        {
            Interface.Oxide.LogError("[{0}] {1}",Title, args.Length <= 0 ? format : string.Format(format, args));
        }
    }
}
