using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RespawnKit", "bazuka5801", "1.0.0")]
    class RespawnKit : RustPlugin
    {
        #region CLASSES

        class KitItem
        {
            [JsonIgnore]
            private ItemDefinition _info;
            
            public ItemDefinition Info() => _info ?? (_info = ItemManager.FindItemDefinition(_sortname));

            [JsonProperty("shortname")]
            private string _sortname;
            [JsonProperty("amount")]
            public int Amount;
            [JsonProperty("skin")]
            public ulong Skin;

            public KitItem() { }

            public KitItem(string shortname, int amount, ulong skin)
            {
                this._sortname = shortname;
                this.Amount = amount;
                this.Skin = skin;
            }
        }

        class RespawnKitData
        {
            public List<KitItem> wear = new List<KitItem>();
            public List<KitItem> belt = new List<KitItem>();
        }
        
        #endregion

        #region FIELDS

        [PluginReference]
        private Plugin Duels;

        private RespawnKitData kit;

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadData();
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;
            if (InDuel(player)) return;
            GiveKit(player);
        }

        #endregion

        #region CORE

        void GiveKit(BasePlayer player)
        {
            player.inventory.Strip();
            GiveKit(player, kit.wear, player.inventory.containerWear);
            GiveKit(player, kit.belt, player.inventory.containerBelt);
        }

        void GiveKit(BasePlayer player, List<KitItem> items, ItemContainer container)
        {
            foreach (var item in items)
                ItemManager.Create(item.Info(), item.Amount, item.Skin).MoveToContainer(container);
        }

        void InitializeKit()
        {
            kit = new RespawnKitData();
            kit.wear.Add(new KitItem("hat.cap", 1, 0));
            kit.wear.Add(new KitItem("shirt.tanktop", 1, 0));
            kit.wear.Add(new KitItem("pants.shorts", 1, 0));
            kit.wear.Add(new KitItem("attire.hide.boots", 1, 0));

            kit.belt.Add(new KitItem("stonehatchet", 1, 0));
            kit.belt.Add(new KitItem("stone.pickaxe", 1, 0));
            kit.belt.Add(new KitItem("building.planner", 1, 0));
            kit.belt.Add(new KitItem("hammer", 1, 0));
            kit.belt.Add(new KitItem("spear.wooden", 1, 0));
        }

        #endregion

        #region DATA

        private DynamicConfigFile RespawnKitDataFile = Interface.Oxide.DataFileSystem.GetFile("RespawnKit");

        void LoadData()
        {
            kit = RespawnKitDataFile.ReadObject<RespawnKitData>();
            if (kit == null || kit.belt.Count == 0)
            {
                InitializeKit();
                SaveData();
            }
        }

        void SaveData()
        {
            RespawnKitDataFile.WriteObject(kit);
        }

        #endregion

        #region DUEL

        bool InDuel(BasePlayer player) => Duels?.Call<bool>("inDuel", player) ?? false;

        #endregion
    }
}
