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
    [Info("SortItems", "bazuka5801", "1.0.0")]
    class SortItems : RustPlugin
    {
        #region CONFIGURATION

        protected override void LoadDefaultConfig()
        {

            SaveConfig();
        }

        #endregion

        #region FIELDS

        const string permAccess = "sortitems.access";
        RCore core = Interface.Oxide.GetLibrary<RCore>();
        List<string> toggleCaptions = new List<string>() {"<-", "->"};
        List<ulong> putPlayers = new List<ulong>();
        #endregion

        #region COMMANDS

        [ConsoleCommand("sortitems.toggle")]
        void cmdToggle(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            var player = arg.Player();
            var userId = player.userID;

            if (putPlayers.Contains(userId))
            {
                putPlayers.Remove(userId);
                DestroyUI(player);
                DrawUI(player);
            }
            else
            {
                putPlayers.Add(userId);
                DestroyUI(player);
                DrawUI(player);
            }
        }
        
        [ConsoleCommand("sortitems.sort")]
        void cmdSort(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            var player = arg.Player();
            var category = arg.GetInt(0);

            var lootContainer = player.inventory.loot?.containers?.Count > 0 ? player.inventory.loot?.containers[0]:null;
            if (lootContainer == null) return;
            var playerContainer = player.inventory.containerMain;
            
            if (lootContainer == null || playerContainer == null)
            {
                DestroyUI(player);
                return;
            }

            var inputContainer = putPlayers.Contains(player.userID) ? playerContainer : lootContainer;
            var outputContainer = inputContainer == lootContainer ? playerContainer : lootContainer;

            GetItemsByCategory(inputContainer, category).ForEach(item => item.MoveToContainer(outputContainer));
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            PermissionService.RegisterPermissions(this, new List<string>() {permAccess});
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player.IsAdmin || PermissionService.HasPermission(player.userID,permAccess))
                DrawUI(player);
        }

        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            var player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return;

            if (player.IsAdmin || PermissionService.HasPermission(player.userID, permAccess))
                DestroyUI(player);
        }

        #endregion

        #region CORE

        List<Item> GetItemsByCategory(ItemContainer container, int category)
        {
            List<ItemCategory> categories = new List<ItemCategory>();
            switch (category)
            {
                case 0:
                    categories.Add(ItemCategory.Resources);
                    break;
                case 1:
                    categories.Add(ItemCategory.Weapon);
                    break;
                case 2:
                    categories.Add(ItemCategory.Ammunition);
                    break;
                case 3:
                    categories.Add(ItemCategory.Medical);
                    break;
                case 4:
                    categories.Add(ItemCategory.Attire);
                    break;
                case 5:
                    categories.Add(ItemCategory.Component);
                    break;
                case 6:
                    categories.Add(ItemCategory.Tool);
                    break;
                case 7:
                    categories.Add(ItemCategory.Construction);
                    categories.Add(ItemCategory.Items);
                    categories.Add(ItemCategory.Traps);
                    categories.Add(ItemCategory.Misc);
                    categories.Add(ItemCategory.Common);
                    categories.Add(ItemCategory.Search);
                    break;
                case 8:
                    for (int i = 0; i < 15; i++)
                        categories.Add((ItemCategory)i);
                    break;
            }
            return container.itemList.Where(item => item != null && categories.Contains(item.info.category)).ToList();
        }

        bool HasAccess(BasePlayer player) => player.IsAdmin;

        #endregion

        #region UI

        void DrawUI(BasePlayer player)
        {
            core.DrawUI(player, "SortItems", "menu", toggleCaptions[putPlayers.Contains(player.userID) ? 1 : 0]);
        }

        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI(player, "SortItems", "menu");
        }

        #endregion
    }
}
