// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingBlockGUI", "bazuka5801", "1.0.0")]
    class BuildingBlockGUI : RustPlugin
    {
        #region FIELDS
        
        RCore core = Interface.Oxide.GetLibrary<RCore>();

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            foreach (var player in BasePlayer.activePlayerList)
                UseBuildingImage(player);
        }

        void OnPlayerInit(BasePlayer player)
        {
            UseBuildingImage(player);
        }

        void OnPlayerTeleported(BasePlayer player)
        {
            timer.Once(1f, () => UseBuildingImage(player));
        }

        void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        {
            if (!(trigger is BuildPrivilegeTrigger)) return;
            BasePlayer player = entity as BasePlayer;
            if (player == null) return;
            timer.Once(0.1f, () => UseBuildingImage(player));
        }
        void OnEntityLeave(TriggerBase trigger, BaseEntity entity)
        {
            if (!(trigger is BuildPrivilegeTrigger)) return;
            BasePlayer player = entity as BasePlayer;
            if (player == null) return;
            timer.Once(0.1f, () => UseBuildingImage(player));
        }

        void OnCupboardAuthorize(BuildingPrivlidge privlidge, BasePlayer player)
        {
            timer.Once(0.1f, () => UseBuildingImage(player));
        }

        void OnCupboardClearList(BuildingPrivlidge privlidge, BasePlayer player)
        {
            timer.Once(0.1f, () => UseBuildingImage(player));
            foreach (var p in privlidge.authorizedPlayers.Select(p=>BasePlayer.FindByID(p.userid)).Where(p=>p!=null))
                timer.Once(0.1f,()=>UseBuildingImage(p));
        }

        void OnCupboardDeauthorize(BuildingPrivlidge privlidge, BasePlayer player)
        {
            timer.Once(0.1f, () => UseBuildingImage(player));
        }

        List<ulong> hasUi = new List<ulong>();

        void UseBuildingImage(BasePlayer player)
        {
            var privlidge = player.GetBuildingPrivilege();
            if (privlidge == null || privlidge.authorizedPlayers.Count(p => p.userid == player.userID) > 0)
            {
                if (hasUi.Contains(player.userID))
                {
                    DestroyUI(player);
                    hasUi.Remove(player.userID);
                }
            }
            else
            {
                if (!hasUi.Contains(player.userID))
                    hasUi.Add(player.userID);
                DrawUI(player);
            }
        }

        #endregion

        #region UI


        void DrawUI(BasePlayer player)
        {
            core.DrawUI(player, "BuildingBlockGUI", "menu");
        }
        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI(player, "BuildingBlockGUI", "menu");
        }

        #endregion
    }
}
