using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChatManager", "bazuka5801", "1.0.0")]
    public class ChatManager : RustPlugin
    {

        private static ChatManager m_Instance;
        private const string PERM_MODER = "chatmanager.moderator";
        Dictionary<ulong, double> mutes = new Dictionary<ulong, double>();
        Dictionary<ulong, PlayerData> players = new Dictionary<ulong, PlayerData>();
        private PlayerData defaultData = new PlayerData();

        void OnServerInitialized()
        {
            m_Instance = this;
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadConfig();
            LoadData();
        }

        void Unload()
        {
            OnServerSave();
        }

        bool? OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var sender = arg.Player();
            var message = arg.GetString( 0, "text" ).Trim();
            
            if (SpamCheck( sender, message ) != null) return true;
            if (MuteCheck( sender ) != null)return true;

            if (config.CapsBlock)
            {
                message = RemoveCaps(message);
            }

            bool mute;
            var censorMessage = CensorBadWords(message, out mute );
            if (config.BadWordsBlock & mute)
            {
                Mute(sender, config.MuteBadWordsDefault);
            }

            var pData = GetPlayerData(sender);
            var prefix = config.Get(config.prefixes, pData.Prefix).Format;
            var nameColor = config.Get( config.names, pData.NameColor ).Format;
            var messageColor = config.Get( config.messages, pData.MessageColor ).Format;

            Interface.Oxide.RootLogger.Write( Oxide.Core.Logging.LogType.Info, $"[CHAT] {sender.userID}/{sender.displayName}: {message}");

            var name = Format(nameColor, sender.displayName);
            message = Format( messageColor, message);
            censorMessage = Format( messageColor, censorMessage);
            if (prefix.Length > 0)
                name = $"{prefix} {name}";
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendChat(player, name, GetPlayerData(player).Censor ? censorMessage : message, sender.userID);
            }
            return false;
        }

        void SendChat(BasePlayer player, string name, string message, ulong userId = 0)
        {
            player.SendConsoleCommand("chat.add", userId,
                string.IsNullOrEmpty(name) ? $"{message}" : $"{name}: {message}");
        }

        public string CensorBadWords( string input, out bool found )
        {
            found = false;
            string temp = input.ToLower();
            foreach (var swear in config.badWords)
            {
                var firstIndex = temp.IndexOf( swear.Key );
                if (firstIndex >= 0 && swear.Value.All( exception => temp.IndexOf( exception ) < 0 ))
                    while (firstIndex < input.Length && input[ firstIndex ] != ' ')
                    {
                        input = input.Remove( firstIndex, 1 );
                        input = input.Insert( firstIndex, "*" );
                        firstIndex++;
                        found = true;
                    }
            }
            return input;
        }
        void Mute( BasePlayer player, long time )
        {
            mutes[ player.userID ] = GrabCurrentTime() + time;
            BroadcastChat( "", string.Format( Messages[ "USER.MUTED" ], player.displayName, "нецензурная лексика", TimeToString( time ) ) );
        }
        void BroadcastChat( string name, string message, ulong userId = 0, string censormessage = "" )
        {
            if (string.IsNullOrEmpty( censormessage )) censormessage = message;
            ConsoleNetwork.BroadcastToAllClients( "chat.add", userId, string.IsNullOrEmpty( name ) ? $"{censormessage}" : $"{name}: {censormessage}" );
        }
        string RemoveCaps(string message)
        {
            var ss = message.Split( ' ' );
            for (int j = 0; j < ss.Length; j++)
            for (int i = 1; i < ss[ j ].Length; i++)
            {
                var sym = ss[ j ][ i ];
                if (char.IsLower( sym )) continue;
                ss[ j ] = ss[ j ].Remove( i, 1 );
                ss[ j ] = ss[ j ].Insert( i, char.ToLower( sym ).ToString() );
            }
            return string.Join( " ", ss );
        }

        bool? MuteCheck( BasePlayer sender)
        {
            double muteTime;
            if (mutes.TryGetValue( sender.userID, out muteTime ))
            {
                var remain = muteTime - GrabCurrentTime();
                if (remain >= 0)
                {
                    Reply( sender,  "YOU.MUTED", TimeToString( remain ) );
                    return true;
                }
                mutes.Remove( sender.userID );
            }
            return null;
        }

        public string TimeToString( double time )
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds( time );
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt( (float) elapsedTime.TotalDays );
            string s = "";

            if (days > 0) s += $"{days}дн.";
            if (hours > 0) s += $"{hours}ч. ";
            if (minutes > 0) s += $"{minutes}мин. ";
            if (seconds > 0) s += $"{seconds}сек.";
            else s = s.TrimEnd( ' ' );
            return s;
        }
        bool? SpamCheck(BasePlayer sender, string message)
        {
            if (message.Length > 500)
            {
                sender.Kick( "CHAT SPAM > 500 CHARS" );
                return false;
            }
            if (message.Length > 100)
            {
                SendReply( sender, "Запрещено отправлять столько символов" );
                return false;
            }
            return null;
        }

        #region Commands

        Dictionary<ulong, ulong> pmHistory = new Dictionary<ulong, ulong>();
        
        [ChatCommand("pm")]
        void cmdChatPM(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                Reply(player, "CMD.PM.HELP" );
                return;
            }
            var argList = args.ToList();
            argList.RemoveAt(0);
            string message = string.Join(" ", argList.ToArray());
            var reciever = BasePlayer.activePlayerList.FirstOrDefault( p => p.displayName.ToLower()
                .Contains( args[ 0 ].ToLower() ) );
            if (reciever == null)
            {
                Reply(player, "PLAYER.NOT.FOUND",args[0] );
                return;
            }

            if (GetPlayerData(reciever).BlackList.Contains(player.userID))
            {
                Reply(player, "PM.YOU.ARE.BLACK.LIST", reciever.displayName );
                return;
            }

            pmHistory[player.userID] = reciever.userID;
            pmHistory[reciever.userID] = player.userID;

            Reply(player, "PM.SENDER.FORMAT", reciever.displayName, message);
            Reply(reciever, "PM.RECEIVER.FORMAT", player.displayName, message);

            if (GetPlayerData( reciever ).PMSound)
            {
                Effect.server.Run( config.PrivateSoundMessagePath, reciever.GetNetworkPosition());
            }
        }
        [ChatCommand( "r" )]
        void cmdChatR( BasePlayer player, string command, string[] args )
        {
            if (args.Length == 0)
            {
                Reply( player, "CMD.R.HELP" );
                return;
            }
            var argList = args.ToList();
            string message = string.Join( " ", argList.ToArray() );
            ulong recieverUserId;
            
            if (!pmHistory.TryGetValue(player.userID, out recieverUserId))
            {
                Reply(player, "PM.NO.MESSAGES" );
                return;
            }
            var reciever = BasePlayer.activePlayerList.FirstOrDefault( p => p.userID == recieverUserId );
            if (reciever == null)
            {
                Reply( player, "PM.PLAYER.LEAVE");
                return;
            }

            if (GetPlayerData( reciever ).BlackList.Contains( player.userID ))
            {
                Reply( player, "PM.YOU.ARE.BLACK.LIST", reciever.displayName );
                return;
            }

            Reply( player, "PM.SENDER.FORMAT", reciever.displayName, message );
            Reply( reciever, "PM.RECEIVER.FORMAT", player.displayName, message );

            if (GetPlayerData(reciever).PMSound)
            {
                Effect.server.Run(config.PrivateSoundMessagePath, reciever.GetNetworkPosition());
            }
        }

        [ChatCommand("mute")]
        void cmdChatMute(BasePlayer player, string command, string[] args)
        {
            if (!IsModerator(player))
            {
                Reply( player, "NO.ACCESS" );
                return;
            }
            if (args.Length == 0)
            {
                Reply( player, "CMD.MUTE.HELP" );
                return;
            }
            var mutePlayer = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower().Contains(args[0].ToLower()));

            if (mutePlayer == null)
            {
                Reply( player, "PLAYER.NOT.FOUND", args[ 0 ] );
                return;
            }

            if (mutes.ContainsKey(mutePlayer.userID))
            {
                Reply(player, "USER.ALREADY.MUTED", mutePlayer.displayName, TimeToString(mutes[mutePlayer.userID]));
                return;
            }
            double time = StringToTime(args[1]);
            Mute(mutePlayer, Convert.ToInt64(time));
        }


        [ChatCommand( "unmute" )]
        void cmdChatUnMute( BasePlayer player, string command, string[] args )
        {
            if (!IsModerator( player ))
            {
                Reply( player, "NO.ACCESS" );
                return;
            }
            if (args.Length == 0)
            {
                Reply( player, "CMD.UNMUTE.HELP" );
                return;
            }
            var mutePlayer = BasePlayer.activePlayerList.FirstOrDefault( p => p.displayName.ToLower().Contains( args[ 0 ].ToLower() ) );

            if (mutePlayer == null)
            {
                Reply( player, "PLAYER.NOT.FOUND", args[ 0 ] );
                return;
            }
            if (!mutes.ContainsKey( mutePlayer.userID ))
            {
                SendReply(player, "У игрока нет мута");
                return;
            }
            mutes.Remove(mutePlayer.userID);
            BroadcastChat( "", string.Format( Messages[ "USER.UNMUTED" ],player.displayName) );
        }

        public long StringToTime( string time )
        {
            time = time.Replace( " ", "" ).Replace( "d", "d " ).Replace( "h", "h " ).Replace( "m", "m " ).Replace( "s", "s " ).TrimEnd( ' ' );
            var arr = time.Split( ' ' );
            long seconds = 0;
            foreach (var s in arr)
            {
                var n = s.Substring( s.Length - 1, 1 );
                var t = s.Remove( s.Length - 1, 1 );
                int d = int.Parse( t );
                switch (n)
                {
                    case "s":
                        seconds += d;
                        break;
                    case "m":
                        seconds += d * 60;
                        break;
                    case "h":
                        seconds += d * 3600;
                        break;
                    case "d":
                        seconds += d * 86400;
                        break;
                }
            }
            return seconds;
        }
        bool IsModerator(BasePlayer player) => PermissionService.HasPermission(player.userID, PERM_MODER);

        [ChatCommand("chat")]
        void cmdChat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                Reply( player, "CMD.CHAT.HELP" );
                return;
            }
            var playerData = GetPlayerData(player);
            switch (args[0])
            {
                case "censor":
                    if (args.Length == 1 || (args[1] != "on" && args[1] != "off"))
                    {
                        Reply(player, "CMD.CHAT.CENSOR.HELP");
                        return;
                    }
                    bool censor = args[1] == "on";
                    playerData.Censor = censor;
                    if (censor)
                    {
                        Reply(player, "CENSOR.ENABLED");
                        return;
                    }
                    else
                    {
                        Reply(player, "CENSOR.DISABLED");
                        return;
                    }
                case "ignore":
                    if (args.Length == 2 && args[1] == "list")
                    {
                        if (playerData.BlackList.Count == 0)
                        {
                            Reply(player, "IGNORE.LIST.IS.EMPTY");
                            return;
                        }
                        SendReply(player, Messages[ "IGNORE.LIST" ]+ string.Join(", ", playerData.BlackList.Select(p=> GetPlayerData(p).Name).ToArray()) );
                        return;
                    }
                    if (args.Length < 3 || (args[1] != "add" && args[1] != "remove"))
                    {
                        Reply(player, "CMD.CHAT.IGNORE.HELP");
                        return;
                    }
                    int mode = args[1] == "add" ? 1 : -1;

                    var ignorePlayer =
                        BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower()
                            .Contains(args[2].ToLower()));
                    if (ignorePlayer == null)
                    {
                        Reply(player, "PLAYER.NOT.FOUND", args[2]);
                        return;
                    }
                    if (mode == 1)
                    {
                        if (!playerData.BlackList.Contains(ignorePlayer.userID))
                        {
                            playerData.BlackList.Add(ignorePlayer.userID);
                            Reply(player, "USER.ADD.IGNORE.LIST", ignorePlayer.displayName );
                            Reply(ignorePlayer, "YOU.ADD.IGNORE.LIST", player.displayName );
                            return;
                        }
                        else
                        {
                            Reply(player, "USER.IS.IGNORE.LIST", ignorePlayer.displayName);
                            Reply(ignorePlayer, "YOU.REMOVE.IGNORE.LIST", player.displayName );
                           return;
                        }
                    }
                    else
                    {
                        if (playerData.BlackList.Contains(ignorePlayer.userID))
                            playerData.BlackList.Remove( ignorePlayer.userID );
                        Reply(player, "USER.REMOVE.IGNORE.LIST", ignorePlayer.displayName);

                        return;
                    }
                case "sound":
                    if (args.Length == 1 || (args[1] != "on" && args[1] != "off"))
                    {
                        Reply(player, "CMD.CHAT.SOUND.HELP");
                        return;
                    }
                    bool pmSound = args[1] == "on";
                    playerData.PMSound = pmSound;
                    if (pmSound)
                    {
                        Reply(player, "SOUND.ENABLED");
                        return;
                    }
                    else
                    {
                        Reply(player, "SOUND.DISABLED");
                        return;
                    }
                case "prefix":
                    var aviablePrefixes = config.prefixes
                        .Where(p => PermissionService.HasPermission(player.userID, p.Perm) &&
                                    GetPlayerData(player).Prefix != p.Perm).ToList();
                    if (aviablePrefixes.Count == 0)
                    {
                        Reply(player, "NO.AVAILABLE.PREFIXS");
                        return;
                    }
                    if (args.Length == 1)
                    {
                        Reply(player, "CMD.CHAT.PREFIX.HELP",
                            string.Join("\n", aviablePrefixes.Select(p => $"/chat prefix {p.Arg} - {p.Format}").ToArray()));
                        return;
                    }
                    var selectedPrefix = aviablePrefixes.FirstOrDefault(p => p.Arg == args[1]);
                    if (selectedPrefix == null)
                    {
                        Reply(player, "PREFIX.NOT.FOUND", args[1]);
                        return;
                    }
                    playerData.Prefix = selectedPrefix.Perm;
                    Reply(player, "PREFIX.CHANGED", selectedPrefix.Arg);
                    return;
                case "name":
                    var aviableNameColors = config.names
                        .Where( p => PermissionService.HasPermission( player.userID, p.Perm ) &&
                                     GetPlayerData( player ).NameColor != p.Perm ).ToList();
                    if (aviableNameColors.Count == 0)
                    {
                        Reply( player, "NO.AVAILABLE.COLORS" );
                        return;
                    }
                    if (args.Length == 1)
                    {
                        Reply( player, "CMD.CHAT.NAME.HELP",
                            string.Join( "\n", aviableNameColors.Select( p => $"/chat name {Format(p.Format,p.Arg)}" ).ToArray() ) );
                        return;
                    }
                    var selectedNameColor = aviableNameColors.FirstOrDefault( p => p.Arg == args[ 1 ] );
                    if (selectedNameColor == null)
                    {
                        Reply( player, "COLOR.NOT.FOUND", args[ 1 ] );
                        return;
                    }
                    playerData.NameColor = selectedNameColor.Perm;
                    Reply( player, "NAME.COLOR.CHANGED", selectedNameColor.Arg );
                    return;
                case "message":
                    var aviableMessageColors = config.messages
                        .Where( p => PermissionService.HasPermission( player.userID, p.Perm ) &&
                                     GetPlayerData( player ).MessageColor != p.Perm ).ToList();
                    if (aviableMessageColors.Count == 0)
                    {
                        Reply( player, "NO.AVAILABLE.COLORS" );
                        return;
                    }
                    if (args.Length == 1)
                    {
                        Reply( player, "CMD.CHAT.MESSAGE.HELP",
                            string.Join( "\n", aviableMessageColors.Select( p => $"/chat message {Format( p.Format, p.Arg )}" ).ToArray() ) );
                        return;
                    }
                    var selectedMessageColor = aviableMessageColors.FirstOrDefault( p => p.Arg == args[ 1 ] );
                    if (selectedMessageColor == null)
                    {
                        Reply( player, "COLOR.NOT.FOUND", args[ 1 ] );
                        return;
                    }
                    playerData.MessageColor = selectedMessageColor.Perm;
                    Reply( player, "MESSAGE.COLOR.CHANGED", selectedMessageColor.Arg );
                    return;

            }
        }

        void Reply(BasePlayer player, string langKey, params string[] args)
        {
            SendReply(player, Format(Messages[langKey], args));
        }

        string Format(string input, params string[] args)
        {
            string ret = input;
            for (int argIndex = 0; argIndex < args.Length; argIndex++)
                ret = ret.Replace($"{{{argIndex}}}", args[argIndex]);
            return ret;
        }

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract( new DateTime( 1970, 1, 1, 0, 0, 0 ) ).TotalSeconds;

        #endregion

        #region Data

        public class PlayerData
        {
            public string Prefix = "chatmanager.default";
            public string NameColor = "chatmanager.default";
            public string MessageColor = "chatmanager.default";
            public bool Censor = true;
            public bool PMSound = true;
            public string Name = "";
            public List<ulong> BlackList = new List<ulong>();
        }

        PlayerData GetPlayerData( BasePlayer player )
        {
            var data = GetPlayerData(player.userID);
            data.Name = player.displayName;
            return data;
        }
        PlayerData GetPlayerData( ulong userId )
        {
            PlayerData config;
            if (players.TryGetValue(userId, out config))
            {
                if (config.Prefix != "chatmanager.default" &&!PermissionService.HasPermission(userId, config.Prefix)) config.Prefix = "chatmanager.default";
                if (config.NameColor != "chatmanager.default" && !PermissionService.HasPermission( userId, config.NameColor )) config.NameColor = "chatmanager.default";
                if (config.MessageColor != "chatmanager.default" && !PermissionService.HasPermission( userId, config.MessageColor )) config.MessageColor = "chatmanager.default";
                return config;
            }
            config = new PlayerData();
            players[ userId ] = config;
            return config;
        }

        DynamicConfigFile mutes_File = Interface.Oxide.DataFileSystem.GetFile( "ChatManager_Mutes" );
        DynamicConfigFile players_File = Interface.Oxide.DataFileSystem.GetFile( "ChatManager_Players" );

        void LoadData()
        {
            mutes = mutes_File.ReadObject<Dictionary<ulong, double>>();
            players = players_File.ReadObject<Dictionary<ulong, PlayerData>>();
        }

        void OnServerSave()
        {
            mutes_File.WriteObject( mutes );
            players_File.WriteObject( players.Where( p => !p.Value.Equals( defaultData ) ).ToDictionary( p => p.Key, p => p.Value ) );
        }

        #endregion

        #region Localization

        private Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"CMD.CHAT.HELP", "ДОСТУПНЫЕ КОМАНДЫ:\n/chat censor - цензура в чате\n/chat prefix - доступные префиксы\n/chat name - доступные цвета имени\n/chat message - доступные цвета сообщений\n/chat ignore - черный список\n/chat sound - звук при получении ЛС" },
            {"CMD.CHAT.HELP.PERMISSION", "\n/chat admin - режим администратора\n/chat moderator - режим модератора"},
            {"CMD.CHAT.PREFIX.HELP", "ДОСТУПНЫЕ ПРЕФИКСЫ:\n{0}" },
            {"NO.AVAILABLE.PREFIXS", "У вас нет доступных префиксов" },
            {"PREFIX.RESET", "Префикс успешно удален" },
            {"PREFIX.NOT.FOUND", "Префикс с названием \"{0}\" не найден" },
            {"NO.ACCESS.THIS.PREFIX", "У вас нет доступа к данному префиксу" },
            {"PREFIX.CHANGED", "Префикс изменен на {0}" },
            {"CMD.CHAT.NAME.HELP", "ДОСТУПНЫЕ ЦВЕТА:\n{0}" },
            {"NO.AVAILABLE.COLORS", "У вас нет доступных цветов" },
            {"NAME.COLOR.RESET", "Цвет успешно сброшен" },
            {"COLOR.NOT.FOUND", "Цвет с названием \"{0}\" не найден" },
            {"NO.ACCESS.THIS.COLOR", "У вас нет доступа к данному цвету"},
            {"NO.NAME.COLOR.CHANGED", "Вы не можете изменить цвет имени игрока, для начала установите один из доступных префиксов" },
            {"NAME.COLOR.CHANGED", "Цвет имени успешно изменен на {0}" },
            {"CMD.CHAT.MESSAGE.HELP", "ДОСТУПНЫЕ ЦВЕТА:\n{0}" },
            {"NO.MESSAGE.COLOR.CHANGED", "Вы не можете изменить цвет сообщений, для начала установите один из доступных префиксов" },
            {"MESSAGE.COLOR.CHANGED", "Цвет чат сообщений успешно изменен на {0}" },
            {"CMD.CHAT.SOUND.HELP", "Используйте /chat sound on или /chat sound off чтобы включить или выключить звуковое оповещение при получении ЛС" },
            {"SOUND.ENABLED", "Вы включили звуковое оповещение при получении ЛС" },
            {"SOUND.DISABLED", "Вы выключили звуковое оповещение при получении ЛС" },
            {"CMD.MUTE.HELP", "Используйте /mute \"имя игрока\" <длительность> <причина> чтобы заблокировать чат игроку" },
            {"USER.ALREADY.MUTED", "Игрок \"{0}\" уже заблокирован.\nОсталось: {1}"},
            {"TIME.FORMAT.ERROR", "Некорректный формат времени.\nИспользуйте: #d дни #h часы #m минуты #s секунды.\nПример 1 час 30 минут: 1h30m" },
            {"USER.MUTED", "Игроку \"{0}\" заблокировали чат.\nДлительность: {2}" },
            {"USER.MUTED.LOG", "\"{0}\" заблокировал чат \"{1}/{2}\" длительность \"{3}\"" },
            {"CMD.UNMUTE.HELP", "Используйте /unmute \"имя игрока\" чтобы разблокировать чат игроку" },
            {"USER.UNMUTED", "Игроку \"{0}\" разблокировали чат" },
            {"USER.UNMUTED.LOG", "\"{0}\" разблокировал чат \"{1}/{2}\"" },
            {"YOU.MUTED", "Ваш чат заблокирован!\nОсталось: {0}" },
            {"CMD.MUTE.ALL.HELP", "Используйте /muteall on или /muteall off чтобы заблокировать или разблокировать общий чат" },
            {"MUTE.ALL.ENABLED", "Общий чат заблокирован" },
            {"MUTE.ALL.DISABLED", "Общий чат разблокирован" },
            {"FLOOD.PROTECTION", "Защита от флуда, подождите {0}" },
            {"CMD.CHAT.IGNORE.HELP", "СПИСОК КОМАНД:\n/chat ignore add \"имя игрока\" - добавить в черный список\n/chat ignore remove \"имя игрока\" - удалить из черного списка\n/chat ignore list - показать черный список"},
            {"USER.IS.IGNORE.LIST", "Игрок \"{0}\" уже находится в черном списке" },
            {"USER.ADD.IGNORE.LIST", "Вы добавили игрока \"{0}\" в черный список" },
            {"YOU.ADD.IGNORE.LIST", "Игрок \"{0}\" добавил вас в черный список" },
            {"IGNORE.LIST.IS.EMPTY", "Черный список пуст" },
            {"USER.REMOVE.IGNORE.LIST", "Вы удалили игрока \"{0}\" из черного списка" },
            {"YOU.REMOVE.IGNORE.LIST", "Игрок \"{0}\" удалил вас из черного списка" },
            {"IGNORE.LIST", "ЧЁРНЫЙ СПИСОК:\n" },
            {"CMD.PM.HELP", "Используйте /pm \"имя игрока\" \"сообщение\" чтобы отправить ЛС игроку" },
            {"PM.SENDER.FORMAT", "<color=#e664a5>ЛС для {0}</color>: {1}"},
            {"PM.RECEIVER.FORMAT",	"<color=#e664a5>ЛС от {0}</color>: {1}" },
            {"PM.NO.MESSAGES", "Вы не получали личных сообщений" },
            {"PM.PLAYER.LEAVE", "Игрок с которым вы переписывались вышел с сервера" },
            {"PM.YOU.ARE.BLACK.LIST", "Вы не можете отправить ЛС игроку \"{0}\", он добавил вас в черный список" },
            {"CMD.R.HELP", "Используйте /r \"сообщение\" чтобы ответить но последнее ЛС" },
            {"CMD.CHAT.CENSOR.HELP", "Используйте /chat censor on или /chat censor off чтобы включить или выключить показ нецензурные слов в чате" },
            {"CENSOR.ENABLED", "Вы включили цензуру в чате" },
            {"CENSOR.DISABLED", "Вы выключили цензуру в чате" },
            {"NO.ACCESS", "У вас нет доступа к этой команде" },
            {"PLAYER.NOT.FOUND", "Игрок \"{0}\" не найден" },
            {"MULTIPLE.PLAYERS.FOUND", "НАЙДЕНО НЕСКОЛЬКО ИГРОКОВ:\n{0}"},
        };

        #endregion

        #region Configuration

        public class ChatPrivilege
        {
            [JsonProperty( "Привилегия" )]
            public string Perm;
            [JsonProperty( "Аргумент" )]
            public string Arg;
            [JsonProperty( "Формат" )]
            public string Format;
        }

        public class ChatConfig
        {
            [JsonProperty( "Выключить заглавные буквы в чате" )]
            public bool CapsBlock;
            [JsonProperty( "Автоматически блокировать чат за нецензурную лексику" )]
            public bool BadWordsBlock;
            [JsonProperty( "Воспроизводить звук при получении личного сообщения" )]
            public bool PrivateSoundMessage;
            [JsonProperty( "Полный путь к звуковому файлу" )]
            public string PrivateSoundMessagePath;
            [JsonProperty( "Длительность блокировки чата по умолчанию(в секундах)" )]
            public int MuteDefault;
            [JsonProperty( "Длительность блокировки чата за нецензурную лексику(в секундах)" )]
            public int MuteBadWordsDefault;
            [JsonProperty( "Префиксы" )]
            public List<ChatPrivilege> prefixes;
            [JsonProperty( "Имена" )]
            public List<ChatPrivilege> names;
            [JsonProperty( "Сообщения" )]
            public List<ChatPrivilege> messages;
            [JsonProperty( "Список начальных букв нецензурных слов или слова целиком | список исключений" )]
            public Dictionary<string, List<string>> badWords;

            public void RegisterPerms()
            {
                PermissionService.RegisterPermissions(prefixes.Select(p => p.Perm).ToList());
                PermissionService.RegisterPermissions(names.Select(p => p.Perm ).ToList());
                PermissionService.RegisterPermissions(messages.Select(p => p.Perm ).ToList());
                PermissionService.RegisterPermissions(new List<string>() {PERM_MODER});
            }

            public ChatPrivilege Get(List<ChatPrivilege> list, string perm)
            {
                return list.FirstOrDefault(p => p.Perm == perm);
            }
        }

        private DynamicConfigFile configFile = Interface.Oxide.DataFileSystem.GetFile("ChatConfig");
        private ChatConfig config;
        public new void LoadConfig()
        {
            if (!configFile.Exists())
            {
                configFile.WriteObject(config = new ChatConfig()
                {
                    CapsBlock = true,
                    PrivateSoundMessage = true,
                    PrivateSoundMessagePath = "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab",
                    prefixes = new List<ChatPrivilege>()
                    {
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.default",
                            Arg = "default",
                            Format = "",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.admin",
                            Arg = "admin",
                            Format = "<color=#a5e664>[Админ]</color>",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.premium",
                            Arg = "premium",
                            Format = "<color=#a5e664>[Премиум]</color>",
                        }
                    },
                    names = new List<ChatPrivilege>()
                    {
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.default",
                            Arg = "default",
                            Format = "<color=#ffffff>{0}</color>",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.purple",
                            Arg = "purple",
                            Format = "<color=#a5e664>{0}</color>",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.orange",
                            Arg = "orange",
                            Format = "<color=#e6a564>{0}</color>",
                        }
                    },
                    messages = new List<ChatPrivilege>()
                    {
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.default",
                            Arg = "default",
                            Format = "<color=#ffffff>{0}</color>>",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.blue",
                            Arg = "blue",
                            Format = "<color=#64a5e6>{0}</color>>",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.black",
                            Arg = "black",
                            Format = "<color=#000000>{0}</color>>",
                        }
                    },
                    badWords = new Dictionary<string, List<string>>()
                    {
                        { "ебля", new List<string>() },
                        { "сука", new List<string>() },
                        { "пидор", new List<string>() },
                    }
                });
            }
            else
            {
                config = configFile.ReadObject<ChatConfig>();
            }
            config.RegisterPerms();
        }
        #endregion

        #region PermissionService

        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission( ulong uid, string permissionName )
            {
                return !string.IsNullOrEmpty( permissionName ) && permission.UserHasPermission( uid.ToString(), permissionName );
            }

            public static void RegisterPermissions(List<string> permissions )
            {
                if (permissions == null) throw new ArgumentNullException( "commands" );

                foreach (var permissionName in permissions.Where( permissionName => !permission.PermissionExists( permissionName ) ))
                {
                    permission.RegisterPermission( permissionName, m_Instance );
                }
            }
        }

        #endregion
    }
}
