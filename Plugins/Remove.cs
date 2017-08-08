// Reference: Oxide.Core.RustyCore

using Oxide.Core;
using RustyCore;
using RustyCore.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Remove", "bazuka5801", "1.0.0")]
    class Remove : RustPlugin
    {
        #region CONFIGURATION

        int resetTime;
        float refundPercent;
        float refundStoragePercent;
        bool friendRemove;
        bool cupboardRemove;
        bool selfRemove;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Время действия режима удаления", out resetTime, 40);
            Config.GetVariable("Процент возвращаемых ресурсов", out refundPercent, 0.5f);
            Config.GetVariable("Процент выпадающих вещей с удаляемых ящиков", out refundStoragePercent, 0.9f);
            Config.GetVariable("Разрешить удаление объектов друзей без авторизации в шкафу", out friendRemove, true);
            Config.GetVariable("Разрешить удаление объектов при наличии авторизации в шкафу", out cupboardRemove, true);
            Config.GetVariable("Разрешить удаление собственных объектов без авторизации в шкафу", out selfRemove, true);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();
        

        List<ulong> activePlayers = new List<ulong>();
        
        #endregion

        #region CLANS PLUGIN REFERENCE

        [PluginReference] Plugin Clans;

        bool HasFriend(ulong uid1, ulong uid2)
        {
            var clan1 = Clans.Call("GetClanOf", uid1) as string;
            if (string.IsNullOrEmpty(clan1)) return false;
            var clan2 = Clans.Call("GetClanOf", uid2) as string;
            if (string.IsNullOrEmpty(clan2)) return false;
            return clan1 == clan2;
        }

        #endregion

        #region COMMANDS

        [ChatCommand("remove")]
        void cmdRemove(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            BuildingUpgrade?.Call("ToggleRemove", player);
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
        }

        [PluginReference] Plugin NoEscape;

        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!activePlayers.Contains(player.userID)) return null;
            if (info == null) return null;
            var entity = info?.HitEntity;
            if (entity == null) return null;
            if ((!(entity is DecayEntity) && !(entity is Signage)) && !entity.ShortPrefabName.ContainsAny("shelves", "quarry","ladder")) return null;
            if (!entity.OwnerID.IsSteamId()) return null ;

            if (NoEscape != null)
            {
                var time = (double) NoEscape.Call("ApiGetTime", player.userID);
                if (time > 0)
                {
                    SendReply(player, string.Format(Messages["raidremove"], core.TimeToString(time)));
                    return null;
                }
            }

            if (friendRemove && HasFriend(player.userID, entity.OwnerID))
            {
                RemoveEntity(player, entity);
                return true;
            }

            if (selfRemove && entity.OwnerID == player.userID)
            {
                RemoveEntity(player, entity);
                return true;
            }

            var ret = Interface.Call("CanRemove", player, entity);
            if (ret is string)
            {
                SendReply(player, (string)ret);
                return null;
            }
            if (ret is bool && (bool) ret)
            {
                RemoveEntity( player, entity );
                return true;
            }

            if (cupboardRemove)
            {
                var cupboard = player.GetBuildingPrivilege();
                if (cupboard != null && cupboard.authorizedPlayers.Select(p => p.userid).Contains(player.userID))
                {
                    RemoveEntity(player,entity);
                    return true;
                }
            }


            SendReply(player, "<color=#ffcc00><size=16>Для ремува авторизуйтесь в шкафу!</size></color>");
            return null;
        }

        #endregion

        #region CORE

        void RemoveEntity(BasePlayer player, BaseEntity entity)
        {
            RefundHelper.Refund(player, entity, 0.5f);
            entity.Kill();
            UpdateTimer(player);
        }
        


        #endregion

        #region UI

        void DrawUI(BasePlayer player, int seconds)
        {
            core.DrawUI(player, "Remove", "menu", seconds);
        }
        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI(player, "Remove", "menu");
        }
        #endregion

        #region API

        void ActivateRemove(ulong userId)
        {
            if (!activePlayers.Contains(userId))
            {
                activePlayers.Add(userId);
            }
        }

        void DeactivateRemove(ulong userId)
        {
            if (activePlayers.Contains(userId))
            {
                activePlayers.Remove(userId);
            }
        }

        #endregion

        #region DATA

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"raidremove", "Ремув во время рейда запрещён!\nОсталось {0}" }
        };

        #endregion

        #region Building Upgrade

        [PluginReference] private Plugin BuildingUpgrade;

        void UpdateTimer(BasePlayer player) => BuildingUpgrade.Call("UpdateTimer", player);

        #endregion
    }
}
