// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RepairTool", "bazuka5801", "1.0.0")]
    class RepairTool : RustPlugin
    {
        #region CONFIGURATION

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Радиус починки", out radius, 30f);
            Config.GetVariable("Перезарядка (в секундах)", out cooldownSeconds, 300);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        const string REPAIR_TOOL_PERM = "repairtool.access";

        private float radius;
        private int cooldownSeconds;

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        Dictionary<ulong, int> cooldowns = new Dictionary<ulong, int>();

        private int repairLayer = LayerMask.GetMask("Construction", "Prevent Building", "Deployed");

        private ItemDefinition stones = ItemManager.FindItemDefinition("stones");

        #endregion


        #region COMMANDS

        [ChatCommand("repair")]
        void cmdChatRepair(BasePlayer player)
        {
            if (!PermissionService.HasPermission(player.userID, REPAIR_TOOL_PERM))
            {
                SendReply(player, "<color=#fec384>У вас нет доступа к данной команде!\nДля разблокировки приобретите услугу в магазине <color=#d2722d>rustylife.ru</color></color>");
                return;
            }
            if (cooldowns.ContainsKey(player.userID))
            {
                SendReply(player, string.Format(Messages["cooldown"], cooldowns[player.userID]));
                return;
            }
            List<ulong> owners = GetClanMembers(player.userID);
            List<DecayEntity> blocks = new List<DecayEntity>();
            Vis.Entities(player.GetNetworkPosition(), radius, blocks, repairLayer, QueryTriggerInteraction.Ignore);

            Dictionary<ItemDefinition, int> repairCost = new Dictionary<ItemDefinition, int>();
            foreach (var block in blocks)
            {
                if (owners.Contains(block.OwnerID) && (block.ShortPrefabName.ContainsAny("foundation", "gates", "wall.external.high")))
                {
                    var cost = block.RepairCost(GetRepairFraction(block));
                    foreach (var item in cost)
                    {
                        if (!repairCost.ContainsKey(item.itemDef))
                            repairCost[item.itemDef] = 0;
                        repairCost[item.itemDef] += (int)item.GetAmount();
                    }
                    if (block.ShortPrefabName.ContainsAny("gates", "wall.external.high") && block.health < block.MaxHealth())
                    {
                        if (!repairCost.ContainsKey(stones))
                            repairCost[stones] = 0;
                        repairCost[stones] += 500;
                    }
                }
            }
            cooldowns[player.userID] = cooldownSeconds;
            if (repairCost.Count == 0)
            {
                SendReply(player, Messages["damagedObjectNotFound"]);
                return;
            }

            foreach (var cost in repairCost)
            {
                var amount = player.inventory.GetAmount(cost.Key.itemid);
                if (amount < cost.Value)
                {
                    SendReply(player, string.Format(Messages["insufficientResources"], cost.Value - amount, cost.Key.displayName.english));
                    return;
                }
            }

            foreach (var block in blocks)
            {
                block.health = block.MaxHealth();
                block.SendNetworkUpdate();
            }

            var msg = "<color=#ffcc00><size=16>Ремонт:\n";

            foreach (var cost in repairCost)
            {
                List<Item> items = new List<Item>();
                player.inventory.Take(items, cost.Key.itemid, cost.Value);
                foreach (var item in items)
                    item.Remove();
                msg += $"<color=#ff2200>{cost.Value}</color> x {cost.Key.displayName.english}\n";
            }

            msg += "</size></color>";

            SendReply(player, msg);
        }
        public float GetRepairFraction(BaseCombatEntity blockToRepair)
        {
            return 1f - blockToRepair.health / blockToRepair.MaxHealth();
        }
        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            PermissionService.RegisterPermissions(this, new List<string>() { REPAIR_TOOL_PERM });
            timer.Every(1f, RepairToolTimerHandler);
        }

        #endregion

        #region CORE

        void RepairToolTimerHandler()
        {
            List<ulong> uids = cooldowns.Keys.ToList();
            for (int i = uids.Count - 1; i >= 0; i--)
            {
                var userid = uids[i];
                var time = cooldowns[userid];
                if (--time < 0)
                    cooldowns.Remove(userid);
                else cooldowns[userid] = time;
            }
        }

        #endregion


        #region CLANS

        [PluginReference]
        private Plugin Clans;

        List<ulong> GetClanMembers(ulong member)
        {
            List<ulong> members = new List<ulong>();
            if (Clans != null)
                members.AddRange((List<ulong>)Clans.Call("ApiGetMembers", member));
            if (members.Count == 0)
                members.Add(member);
            return members;
        }

        #endregion

        #region DATA

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "cooldown", "Ремонт на перезарядке! Осталось: {0}сек."},
            {"damagedObjectNotFound", "<color=ffcc00><size=16>Не удалось найти повреждённые объекты!</size></color>" },
            { "insufficientResources", "Недостаточно: {0} x {1}" }
        };

        #endregion
    }
}
