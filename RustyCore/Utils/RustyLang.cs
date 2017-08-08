using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;

namespace RustyCore.Utils
{
    public static class RustyLang
    {
        private static Dictionary<string, Dictionary<string,string>> RegistredPlugins = new Dictionary<string, Dictionary<string, string>>();


        private static Lang lang = Interface.Oxide.GetLibrary<Lang>();
        public static void Reply(this Plugin plugin, BasePlayer player, string langKey, params object[] args)
        {
            if (!RegistredPlugins.ContainsKey( plugin.Name ))
            {
                Logger.Error( $"'{plugin.Name}' пытается вызвать RustyLang.Reply, но он не содержит список сообщений" );
                return;
            }
            if (!RegistredPlugins[plugin.Name].ContainsKey( langKey ))
            {
                Logger.Error( $"'{plugin.Name}' пытается вызвать RustyLang.Reply, но он не содержит указанное сообщение '{langKey}'" );
                return;
            }
            player.ChatMessage(string.Format(RegistredPlugins[plugin.Name][langKey], args));
        }

        public static void Broadcast( this Plugin plugin, string langKey, params object[] args )
        {
            if (!RegistredPlugins.ContainsKey( plugin.Name ))
            {
                Logger.Error( $"'{plugin.Name}' пытается вызвать RustyLang.Broadcast, но он не содержит список сообщений" );
                return;
            }
            if (!RegistredPlugins[ plugin.Name ].ContainsKey( langKey ))
            {
                Logger.Error( $"'{plugin.Name}' пытается вызвать RustyLang.Broadcast, но он не содержит указанное сообщение '{langKey}'" );
                return;
            }
            ConsoleNetwork.BroadcastToAllClients( "chat.add", 0, string.Format( RegistredPlugins[ plugin.Name ][ langKey ], args ) );
        }

        internal static void OnPluginLoaded(Plugin plugin)
        {
            var messagesField = plugin.GetType().GetField("Messages", BindingFlags.Instance | BindingFlags.NonPublic);
            var messages = messagesField?.GetValue(plugin) as Dictionary<string,string>;
            if (messagesField != null)
            {
                lang.RegisterMessages( messages, plugin, "en" );
                RegistredPlugins[ plugin.Name ] = messages = lang.GetMessages( "en", plugin );
                messagesField.SetValue(plugin, messages);
            }
        }
    }
}
