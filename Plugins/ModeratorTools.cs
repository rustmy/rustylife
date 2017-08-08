// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Globalization;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("ModeratorTools", "bazuka5801", "1.0.0")]
    class ModeratorTools : RustPlugin
    {
        #region CONFIGURATION

        protected override void LoadDefaultConfig()
        {

            SaveConfig();
        }

        #endregion

        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();

        private const string BAN_PERM = "moderator.ban";

        Dictionary<ulong, string> bans = new Dictionary<ulong, string>();
        #endregion

        #region Commands

        [ChatCommand("cpr")]
        void cmdChatCallPlayerReview(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!PermissionService.HasPermission(player.userID, "chatplus.moder"))return;
            if (args.Length == 0)
            {
                SendReply(player, Messages["cprHelp"]);
                return;
            }

            string displayname = args[0];
            BasePlayer target = core.FindBasePlayer(displayname);
            if (target == null)
            {
                SendReply(player, Messages["playerNotFound"]);
                return;
            }
            if (!target.IsConnected)
            {
                SendReply(player, Messages["playerDisconnected"]);
                return;
            }
            if (timers.ContainsKey(target.userID))
            {
                SendReply(player, Messages["playerIsCalled"]);
                return;
            }
            EnableTimer(target);
            DrawUI(target);
            rust.BroadcastChat(null, string.Format(Messages["сallPlayerReview"], target.displayName, player.displayName));
            SendReply(target, string.Format(Messages["reviewHelp"], player.displayName));
            timer.Once(300f, () =>
            {
                player?.ChatMessage(string.Format(Messages["minutesPassed"], target ? target.displayName : ""));
            });
            Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_scream.prefab", target.transform.position);
        }

        [ChatCommand("cprc")]
        void cmdChatCallPlayerReviewCancel(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!PermissionService.HasPermission(player.userID, "chatplus.moder")) return;
            if (args.Length == 0)
            {
                SendReply(player, Messages["cprHelp"]);
                return;
            }

            string displayname = args[0];
            BasePlayer target = core.FindBasePlayer(displayname);
            if (target == null)
            {
                SendReply(player, Messages["playerNotFound"]);
                return;
            }
            if (!target.IsConnected)
            {
                SendReply(player, Messages["playerDisconnected"]);
                return;
            }
            if (!timers.ContainsKey(target.userID))
            {
                SendReply(player,Messages["playerIsNotCalled"]);
                return;
            }
            var tTimer = timers[target.userID];
            timer.Destroy(ref tTimer);
            DisableTimer(target);
        }
        [ChatCommand( "bantp" )]
        void cmdChatBanTP( BasePlayer player, string command, string[] args )
        {
            if (!PermissionService.HasPermission( player.userID, BAN_PERM ))
            {
                SendReply( player, "Недостаточно прав!" );
                return;
            }
            var bannedPlayers =
                BasePlayer.sleepingPlayerList.Where(p => ServerUsers.Get(p.userID)?.@group == ServerUsers.UserGroup.Banned ||
                                                         bans.ContainsKey(p.userID)).ToList();
            if (args.Length == 0)
            {
                string msgPlayers = "";
                for (var i = 0; i < bannedPlayers.Count; i++)
                {
                    var p = bannedPlayers[i];
                    msgPlayers += $"[<color=orange>{i}</color> ({p.userID}/{p.displayName})] ";
                }
                SendReply(player, msgPlayers);
                return;
            }
            int index;
            if (!int.TryParse(args[0], out index))
            {
                SendReply(player, "НЕВЕРНО: пример /bantp 0");
                return;
            }
            core.Teleport(player, bannedPlayers[index]);
        }
        [ChatCommand("ban")]
        void cmdChatBan(BasePlayer player, string command, string[] args)
        {
            if (!PermissionService.HasPermission(player.userID, BAN_PERM))
            {
                SendReply(player, "Недостаточно прав!");
                return;
            }
            if (args.Length < 2) return;
            var nameOrId = args[0];
            string reason = args[1];

            var  uid = nameOrId.IsSteamId() ? ulong.Parse(nameOrId) : core.FindUid(nameOrId);
            var name = core.FindDisplayname(uid);
            

            if (args.Length == 3)
            {
                var secs = core.StringToTime(args[2]);
                bans[uid] = Now().AddSeconds( secs ).ToString( TIME_FORMAT );
            }
            else bans[uid] = Now().AddSeconds(3000000000).ToString(TIME_FORMAT);
            BasePlayer.activePlayerList.FirstOrDefault(p=>p.userID == uid)?.Kick(reason);
            rust.BroadcastChat("<color=red>Rusty Life</color>",
                string.Format(Messages["banPermanent"], $"{uid}/{name}", reason));
        }
        [ChatCommand("unban")]
        void cmdChatUnban(BasePlayer player, string command, string[] args)
        {
            if (!PermissionService.HasPermission(player.userID, BAN_PERM))
            {
                SendReply(player, "Недостаточно прав!");
                return;
            }
            var uid = args[ 0 ].IsSteamId() ? ulong.Parse( args[0] ) : core.FindUid( args[ 0 ] );
            var name = core.FindDisplayname( uid );
            bans.Remove(uid);
            Server.Command("unban", uid);
            rust.BroadcastChat( "<color=red>Rusty Life</color>",
                string.Format( Messages[ "unban" ], $"{uid}/{name}" ) );
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadData();
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            PermissionService.RegisterPermissions(this, new List<string>(){ BAN_PERM });
        }

        bool? CanRemove(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return null;
            if (!entity.OwnerID.IsSteamId()) return null;
            if (PermissionService.HasPermission(player.userID, BAN_PERM))
            {
                if (bans.ContainsKey( entity.OwnerID ) ||ServerUsers.Get(entity.OwnerID)?.@group == ServerUsers.UserGroup.Banned)
                {
                    return true;
                }
            }
            return null;
        }

        List<ulong> GetBannedList()
        {
            return bans.Keys.ToList();
        }

        void Unload()
        {
            SaveData();
        }
        
        object CanClientLogin( Network.Connection connection )
        {
            string ondate;
            if (bans.TryGetValue(connection.userid, out ondate))
            {
                if (Now().CompareTo(ParseTime(ondate)) > 0)
                {
                    bans.Remove(connection.userid);
                    return null;
                }
                return  $"Вы забанены до {ondate}";
            }
            return null;
        }

        #endregion

        #region CORE

        void EnableTimer(BasePlayer player)
        {
            DisableTimer(player);
            timers[player.userID] = this.timer.Once(300f, () => DisableTimer(player));
        }

        void DisableTimer(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            Timer pTimer;
            if (timers.TryGetValue(player.userID, out pTimer))
            {
                DestroyUI(player);
            }
            timers.Remove(player.userID);
        }

        #endregion

        #region UI

        void DrawUI(BasePlayer player)
        {
            core.DrawUI(player, "ModeratorTools", "menu");
        }

        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI(player, "ModeratorTools", "menu");
        }

        #endregion

        #region DATA

        private DynamicConfigFile saveFile = Interface.Oxide.DataFileSystem.GetFile("ModeratorBans");
        void LoadData()
        {
            bans = saveFile.ReadObject<Dictionary<ulong, string>>();
        }

        void OnServerSave() => SaveData();

        void SaveData()
        {
            saveFile.WriteObject(bans);
        }

        #endregion
        DateTime ParseTime( string s ) => DateTime.ParseExact( s, TIME_FORMAT, CultureInfo.InvariantCulture );
        private const string TIME_FORMAT = "yyyy-MM-dd HH:mm";

        private DateTime Now() => DateTime.UtcNow.AddHours(3);
        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"cprHelp", "Используйте так: /cpr PLAYERNAME" },
            {"cprcHelp", "Используйте так: /cprc PLAYERNAME" },
            { "playerNotFound", "Игрок не найден!" },
            { "playerDisconnected", "Игрок не в игре!" },
            { "playerIsNotCalled", "Игрок еще не вызван!" },
            { "playerIsCalled", "Игрок уже вызван!" },
            { "сallPlayerReview", "Игрока {0} вызвал на проверку модератор {1}" },
            { "reviewHelp", "У вас есть 5 минут чтобы предоставить модератору {0} свой рабочий скайп.\r\nОтказ или игнор карается баном." },
            { "minutesPassed", "Время у игрока {0} вышло" },
            { "banPermanent", "{0} забанен\nПричина: {1}" },
            { "unban", "{0} разбанен"},
        };

        #endregion
    }
}
