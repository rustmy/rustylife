// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
using RustyCore.Utils;
using RustyCore;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
    [Info("RustyChat", "bazuka5801","1.0.0")]
    class RustyChat : RustPlugin
    {
        #region CLASSES

        private class PlayerChatConfig
        {
            [JsonProperty("name")]
            public string Name;
            [JsonProperty("prefix")]
            public string Prefix;
            [JsonProperty("msg")]
            public string Message;

            public string MessagePermissions;

            public PlayerChatConfig(PlayerChatConfig c)
            {
                Name = c.Name;
                Prefix = c.Prefix;
                Message = c.Message;
            }

            [JsonConstructor]
            public PlayerChatConfig(string name, string prefix, string message)
            {
                Name = name;
                Prefix = prefix;
                Message = message;
            }

            public bool Equals(PlayerChatConfig c)
            {
                return c.Name == Name && c.Prefix == Prefix && c.Message == Message;
            }
        }

        #endregion

        #region CONFIGURATION

        Dictionary<string, object> names = new Dictionary<string, object>();
        Dictionary<string, object> prefixes = new Dictionary<string, object>();
        Dictionary<string, object> messages = new Dictionary<string, object>();

        List<string> namesKeys;
        List<string> prefixesKeys;
        List<string> messagesKeys;
        bool connectMessage;
        int autoMuteMin;
        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Автоблокировка чата (в минутах)", out autoMuteMin, 1);
            Config.GetVariable("Список имён (permission:nickname)", out names, new Dictionary<string, object>() { { "<color=cyan>cyan</color>", "<color=cyan>{0}</color>" } });
            Config.GetVariable("Список префиксов (permission:prefix)", out prefixes, new Dictionary<string, object>() { { "<color=cyan>cyan</color>", "<color=cyan>[Игрок]</color>" } });
            Config.GetVariable("Список сообщений (permission:message)", out messages, new Dictionary<string, object>() { { "<color=green>green</color>", "<color=green>{0}</color>" } });
            Config.GetVariable("Выводить сообщение когда игрок подключился", out connectMessage, true);
            SaveConfig();
            namesKeys = names.Keys.ToList();
            prefixesKeys = prefixes.Keys.ToList();
            messagesKeys = messages.Keys.ToList();
            defaultChatConfig = new PlayerChatConfig("default", "default", "default");
        }

        #endregion

        #region FIELDS

        private RCore core = Interface.Oxide.GetLibrary<RCore>();

        PlayerChatConfig defaultChatConfig;

        DynamicConfigFile mutes_File = Interface.Oxide.DataFileSystem.GetFile("RustyChat_Mutes");
        DynamicConfigFile players_File = Interface.Oxide.DataFileSystem.GetFile("RustyChat_Players");

        Dictionary<ulong, double> mutes = new Dictionary<ulong, double>();
        Dictionary<ulong, PlayerChatConfig> players = new Dictionary<ulong, PlayerChatConfig>();
        Dictionary<ulong, int> floods = new Dictionary<ulong, int>();

        Dictionary<string, string> clearedNamesPerms = new Dictionary<string, string>();
        Dictionary<string, string> clearedPrefixesPerms = new Dictionary<string, string>();
        Dictionary<string, string> clearedMessagesPerms = new Dictionary<string, string>();

        const string PERM_CHAT_NAME_PREFIX = "chatplus.";
        const string PERM_CHAT_PREFIX_PREFIX = "chatplus.";
        const string PERM_CHAT_MESSAGE_PREFIX = "chatplus.";
        const string PERM_MUTE = "rustychat.mute";
        #endregion

        #region COMMANDS

        [ChatCommand("chat")]
        void cmdChatChat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0) return;
            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "name":
                        Reply(player, string.Format(Messages["getname"], GetName(player.userID),GetNames(player.userID)));
                        break;
                    case "prefix":
                        Reply(player, string.Format(Messages["getprefix"], GetPrefix(player.userID), GetPrefixes(player.userID)));
                        break;
                    case "message":
                        Reply(player, string.Format(Messages["getmessage"], GetMessage(player.userID), GetMessages(player.userID)));
                        break;
                }
            }
            if (args.Length == 2)
            {
                var value = args[1];
                switch (args[0])
                {
                    case "name":
                        if (!GetNames(player.userID).Contains(value))
                        {
                            Reply(player, Messages["invalidname"]);
                            return;
                        }
                        GetConfig(player.userID).Name = value;
                        Reply(player, string.Format(Messages["setname"], GetName(player.userID)));
                        break;
                    case "prefix":
                        if (!GetPrefixes(player.userID).Contains(value))
                        {
                            Reply(player, Messages["invalidprefix"]);
                            return;
                        }
                        GetConfig(player.userID).Prefix = value;
                        Reply(player, string.Format(Messages["setprefix"], GetPrefix(player.userID)));
                        break;
                    case "message":
                        if (!GetMessages(player.userID).Contains(value))
                        {
                            Reply(player, Messages["invalidmessage"]);
                            return;
                        }
                        GetConfig(player.userID).Message = value;
                        Reply(player, string.Format(Messages["setmessage"], GetMessage(player.userID)));
                        break;
                }
            }
        }


        [ChatCommand("mutelist")]
        void cmdChatMuteList(BasePlayer player, string command, string[] args)
        {
            if (!PermissionService.HasPermission(player.userID, PERM_MUTE))
            {
                Reply(player, "У вас нет доступа к этой команде!");
                return;
            }
            var msg = new StringBuilder();
            foreach (var mute in mutes)
            {
                var name = core.FindDisplayname(mute.Key);
                msg.Append($"{name} на {mute.Value}\n");
            }
        }

        [ChatCommand("mute")]
        void cmdChatMute(BasePlayer player, string command, string[] args)
        {
            if (!PermissionService.HasPermission(player.userID, PERM_MUTE))
            {
                Reply(player, "У вас нет доступа к этой команде!");
                return;
            }
            if (args.Length < 2)
            {
                Reply(player,"Неправильно! Пример: /mute вася 15m");
                return;
            }
            var mutePlayer = core.FindBasePlayer(args[0]);
            if (mutePlayer == null)
            {
                Reply(player,"Игрок не найден!");
                return;
            }
            Mute( mutePlayer, core.StringToTime( args[ 1 ] ), args.Length == 3 ? args[ 2 ] : "нарушение правил сервера" );
        }

        [ConsoleCommand("mute")]
        void cmdMute(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;

            var args = arg.Args;

            if (args.Length < 2)
            {
                SendReply(arg, "Неправильно! Пример: /mute вася 15m");
                return;
            }

            var mutePlayer = core.FindBasePlayer(args[0]);
            if (mutePlayer == null)
            {
                SendReply(arg, "Игрок не найден!");
                return;
            }
            Mute(mutePlayer, core.StringToTime(args[1]),args.Length == 3 ? args[2] : "нарушение правил сервера" );
            SendReply(arg, $"{mutePlayer.displayName} получил мут на {arg.Args[1]}");
        }

        [ChatCommand("unmute")]
        void cmdChatUnmute(BasePlayer player, string command, string[] args)
        {
            if (!PermissionService.HasPermission(player.userID, PERM_MUTE))
            {
                Reply(player, "У вас нет доступа к этой команде!");
                return;
            }
            if (args.Length != 1)
            {
                Reply(player, "Неправильно! Пример: /unmute вася");
                return;
            }
            var unmutePlayer = core.FindBasePlayer(args[0]);
            if (unmutePlayer == null)
            {
                Reply(player, "Игрок не найден!");
                return;
            }
            mutes.Remove(unmutePlayer.userID);
            unmutePlayer.ChatMessage("Ваш чат разблокирован!");
        }

        #endregion


        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            LoadData();
            lang.RegisterMessages(Messages,this, "en");
            Messages = lang.GetMessages("en", this);

            List<string> permissions = namesKeys.Select(name => PERM_CHAT_NAME_PREFIX + RemoveColorTags(name)).ToList();
            permissions.AddRange(prefixesKeys.Select(prefix => PERM_CHAT_PREFIX_PREFIX + RemoveColorTags(prefix)));
            permissions.AddRange(messagesKeys.Select(message => PERM_CHAT_MESSAGE_PREFIX + RemoveColorTags(message)));
            permissions.Add(PERM_MUTE);
            PermissionService.RegisterPermissions(this, permissions);

            foreach (var name in namesKeys)
            {
                clearedNamesPerms[name] = PERM_CHAT_NAME_PREFIX+RemoveColorTags(name);
            }
            foreach (var prefix in prefixesKeys)
            {
                clearedPrefixesPerms[prefix] = PERM_CHAT_PREFIX_PREFIX + RemoveColorTags(prefix);
            }
            foreach (var message in messagesKeys)
            {
                clearedMessagesPerms[message] = PERM_CHAT_MESSAGE_PREFIX + RemoveColorTags(message);
            }

            timer.Every(1f, () =>
            {
                List<ulong> toDelete = new List<ulong>();
                var curTime = GrabCurrentTime();
                foreach (var mute in mutes)
                {
                    if (mute.Value - curTime < 0)
                        toDelete.Add(mute.Key);
                }
                toDelete.ForEach(p => mutes.Remove(p));

                toDelete.Clear();

                toDelete.AddRange(floods.Keys.ToList().Where(flood => --floods[flood] < 0));
                toDelete.ForEach(p => floods.Remove(p));
            });
        }

        void Unload()
        {
            OnServerSave();
        }

        void OnPlayerInit(BasePlayer player)
        {
            if(connectMessage)
                BroadcastChat(null,string.Format(Messages["connectMessage"], player.displayName), player.userID);
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var sender = arg.Player();
            var message = arg.GetString(0, "text").Trim();

            if (message.Length > 500)
            {
                sender.Kick("Иди нахуй со своим спамом в чат, уже пофиксили) азазаза затралил)");
                return false;
            }
            if (message.Length > 100)
            {
                SendReply(sender, "Запрещено отправлять столько символов");
                return false;
            }
            message = RemoveCaps(message);

            message = message.Replace("лагает", "хорошо работает").Replace("гавно", "хороший");

            int floodTime;
            if (floods.TryGetValue(sender.userID, out floodTime))
            {
                floodTime++;
                SendReply(sender,string.Format(Messages["floodTimeout"], floodTime));
                floods[sender.userID] = floodTime;
                return true;
            }
            else
            {
                floods[sender.userID] = 3;
            }

            double muteTime;
            if (mutes.TryGetValue(sender.userID, out muteTime))
            {
                var remain = muteTime - GrabCurrentTime();
                if (remain >= 0)
                {
                    Reply(sender, string.Format(Messages["muteself"], core.TimeToString(remain)));
                    return true;
                }
                mutes.Remove(sender.userID);
            }

            bool mute;
            var censorMessage = message.CensorBadWords(out mute);
            /*if (mute)
                Mute(sender, 60*autoMuteMin);
            else
            {
                ClearMessage(message).CensorBadWords(out mute);
                if (mute)
                    Mute(sender, 60 * autoMuteMin);
            }*/
            var prefix = prefixes[GetPrefix(sender.userID)].ToString();
            var name = names[GetName(sender.userID)].ToString().Replace("{0}", sender.displayName);
            if (!PermissionService.HasPermission(sender.userID,clearedNamesPerms[GetName(sender.userID)]))
            {
                name = names[namesKeys.Find(p => p.Contains("default"))].ToString();
                GetConfig(sender.userID).Name = "default";
            }
            if (!PermissionService.HasPermission(sender.userID,clearedPrefixesPerms[GetPrefix(sender.userID)]))
            {
                prefix = prefixes[prefixesKeys.Find(p => p.Contains("default"))].ToString();
                GetConfig(sender.userID).Prefix = "default";
            }
            if (!PermissionService.HasPermission(sender.userID,clearedMessagesPerms[GetMessage(sender.userID)]))
            {
                GetConfig(sender.userID).Message = "default";
            }

            Interface.Oxide.RootLogger.Write(LogType.Info, $"[CHAT] " + (string.IsNullOrEmpty(sender.displayName) ? $"{message}" : $"{sender.displayName}: {censorMessage}"));
            censorMessage = messages[GetMessage(sender.userID)].ToString().Replace("{0}", censorMessage);
            if (prefix.Length > 0)
                name = $"{prefix} {name}";
            BroadcastChat(name, message, sender.userID,censorMessage);

            return true;
        }
        public static string RemoveCaps(string s)
        {
            var ss = s.Split(' ');
            for (int j = 0; j < ss.Length; j++)
                for (int i = 1; i < ss[j].Length; i++)
                {
                    var sym = ss[j][i];
                    if (char.IsLower(sym))continue;
                    ss[j] = ss[j].Remove(i,1);
                    ss[j] =ss[j].Insert(i, char.ToLower(sym).ToString());
                }
            return string.Join(" ", ss);
        }
        string ClearMessage(string message)
        {
            string badChars = ".$,*";
            for (int i = message.Length - 1; i >= 0; i--)
            {
                var c = message[i];
                if (badChars.Contains(c))
                    message = message.Remove(i, 1);
            }
            return message;
        }

        #endregion

        #region CORE

        void Mute(BasePlayer player, long time, string reason)
        {
            mutes[player.userID] = GrabCurrentTime() + time;
            BroadcastChat("", string.Format(Messages["mute"], player.displayName, core.TimeToString(time),reason));
        }

        string GetName(ulong userId) => namesKeys.Find(p=>p.Contains(GetConfig(userId).Name));
        string GetPrefix(ulong userId) => prefixesKeys.Find(p => p.Contains(GetConfig(userId).Prefix));
        string GetMessage(ulong userId) => messagesKeys.Find(p => p.Contains(GetConfig(userId).Message));

        string GetNames(ulong userId)
            =>
            string.Join(", ",
                namesKeys.Where(p => p != GetName(userId) && PermissionService.HasPermission(userId, PERM_CHAT_NAME_PREFIX+RemoveColorTags(p)))
                    .ToArray());
        string GetPrefixes(ulong userId)
            =>
            string.Join(", ",
                prefixesKeys.Where(p => p != GetPrefix(userId) && PermissionService.HasPermission(userId, PERM_CHAT_PREFIX_PREFIX+RemoveColorTags(p)))
                    .ToArray());
        string GetMessages(ulong userId)
            =>
            string.Join(", ",
                messagesKeys.Where(p => p != GetMessage(userId) && PermissionService.HasPermission(userId, PERM_CHAT_MESSAGE_PREFIX+RemoveColorTags(p)))
                    .ToArray());

        void BroadcastChat(string name, string message, ulong userId = 0, string censormessage = "")
        {
            if (string.IsNullOrEmpty(censormessage)) censormessage = message;
            ConsoleNetwork.BroadcastToAllClients("chat.add", userId, string.IsNullOrEmpty(name) ? $"{censormessage}" : $"{name}: {censormessage}");
        }

        void Reply(BasePlayer player, string message)=> player.ChatMessage(message);

        #endregion

        #region API

        double IsMutePlayer(ulong userId)
        {
            double unixSeconds;
            return mutes.TryGetValue(userId,out unixSeconds) ? unixSeconds : 0;
        }

        #endregion

        #region DATA

        void LoadData()
        {
            mutes = mutes_File.ReadObject<Dictionary<ulong, double>>();
            players = players_File.ReadObject<Dictionary<ulong, PlayerChatConfig>>();
        }

        void OnServerSave()
        {
            mutes_File.WriteObject(mutes);
            players_File.WriteObject(players.Where(p => !p.Value.Equals(defaultChatConfig)).ToDictionary(p=>p.Key,p=>p.Value));
        }

        PlayerChatConfig GetConfig(ulong userId)
        {
            PlayerChatConfig config;
            if (players.TryGetValue(userId, out config))
                return config;
            config = new PlayerChatConfig(defaultChatConfig);
            players[userId] = config;
            return config;
        }

        #endregion

        #region LOCALIZATION
        
        Dictionary<string,string> Messages = new Dictionary<string, string>()
        {
            { "mute", "<color=orange>Игроку <color=#ffcc00>{0}</color>\n заблокирован чат на {1}</color>\n{2}"},
            { "muteself", "<color=#ffcc00>Ваш чат заблокирован на {0}</color>" },

            { "getname", "<color=orange>Ваш ник: {0}\nДостпуные ники: {1}</color>" },
            { "getprefix", "<color=orange>Ваш префикс: {0}\nДостпуные префиксы: {1}</color>" },
            { "getmessage", "<color=orange>Ваше сообщение: {0}\nДостпуные сообщения: {1}</color>" },

            { "setname", "<color=orange>Установлен новый ник: {0}</color>" },
            { "setprefix", "<color=orange>Установлен новый префикс: {0}</color>" },
            { "setmessage", "<color=orange>Установлен новое сообщение: {0}</color>" },

            { "invalidname", "<color=orange>Такого ника не существует</color>" },
            { "invalidprefix", "<color=orange>Такого префикса не существует</color>" },
            { "invalidmessage", "<color=orange>Такого сообщения не существует</color>" },

            { "connectMessage", "<color=orange><size=14><color=#ccff00>{0}</color> подключился к игре</size></color>" },
            { "floodTimeout", "<color=orange>Вы отправляете сообщения слишком часто!\nПопробуйте через {0}сек.</color>"  }
        };

        #endregion

        #region HELPERS

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        

        

        string RemoveColorTags(string perm)
        {
            return Regex.Replace(Regex.Replace(perm, "\\<\\/color\\>", "", RegexOptions.IgnoreCase), "\\<color=.+?\\>", "",RegexOptions.IgnoreCase);
        }

        #endregion
    }
}
