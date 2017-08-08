// Reference: Oxide.Core.RustyCore

using System;
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Configuration;
using System.Reflection;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
    [Info("RustyLoot", "bazuka5801", "1.0.0")]
    class RustyLoot : RustPlugin
    {
        #region CONFIGURATION

        class LootItem
        {
            /// <summary>
            /// Короткое имя предмета в игре
            /// </summary>
            public string shortname;

            /// <summary>
            /// Кол-во
            /// </summary>
            public int amount = 1;

            /// <summary>
            /// Шанс выпадения из кейса
            /// </summary>
            public float chance = 1;

            public LootItem()
            {
            }
        }

        class LootCase
        {
            /// <summary>
            /// Коллекция предметов
            /// </summary>
            public List<LootItem> items;
            public bool set = false;
            public LootItem GetRandom()
            {
                float value = items.Sum(t => t.chance);
                float randomValue = UnityEngine.Random.Range(0f, value);
                for (int j = 0; j < items.Count; j++)
                {
                    randomValue -= items[j].chance;
                    if (randomValue <= 0.000001)
                    {
                        return items[j];
                    }
                }
                Interface.Oxide.RootLogger.Write(LogType.Error, $"[{nameof(RustyLoot)}] GetRandom: randomValue if positiveValue: {randomValue}");
                return null;
            }

            public List<LootItem> GetSet(int capacity)
            {
                int max = Math.Min(capacity, items.Count);
                var list = items.ToList();
                while (list.Count > max) list.Remove(list.Last());
                return list;
            }
        }

        class LootStorage
        {
            /// <summary>
            /// Кейсы ящика
            /// Key: имя кейса
            /// Value: шанс выпадения
            /// </summary>
            public Dictionary<string, float> lootCases = new Dictionary<string, float>();

            public LootStorage() { }

            public List<LootItem> GetRandomItems(int capacity, out bool set)
            {
                List<LootItem> items = new List<LootItem>();
                var ccase = RustyLoot.lootCases[GetRandomCase()];
                if (ccase.set)
                {
                    set = true;
                    return ccase.GetSet(capacity);
                }
                set = false;
                for (int i = 0; i < capacity; i++)
                {
                    var lootCase = RustyLoot.lootCases[GetRandomCase()];
                    while (lootCase.set) lootCase = RustyLoot.lootCases[GetRandomCase()];
                    var lootItem = lootCase.GetRandom();
                    items.Add(lootItem);
                }
                return items;
            }

            public string GetRandomCase()
            {
                float value = lootCases.Sum(t => t.Value);
                float randomValue = UnityEngine.Random.Range(0f, value);
                var lootCasesKeys = lootCases.Keys.ToList();

                for (int j = 0; j < lootCasesKeys.Count; j++)
                {
                    string lootCase = lootCasesKeys[j];
                    float chance = lootCases[lootCase];
                    randomValue -= chance;
                    if (randomValue <= 0.000001)
                    {
                        return lootCase;
                    }
                }
                Interface.Oxide.RootLogger.Write(LogType.Error, $"[{nameof(RustyLoot)}] GetCase: randomValue if positiveValue: {randomValue}");
                return null;
            }
        }

        #endregion

        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();
        List<string> lootPrefabs;

        const string PERM_X2 = "customloot.x5";
        const int MULTIPLIER_DEFAULT = 1;

        private static Dictionary<string, LootStorage> lootConfig;
        private static Dictionary<string, LootCase> lootCases;

        private Dictionary<string, ItemDefinition> itemDefinitions;


        List<LootContainer> handledContainers = new List<LootContainer>();

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadData();
            lootPrefabs = GetLootPrefabs();
            PermissionService.RegisterPermissions(this, new List<string>() { PERM_X2 });
            itemDefinitions = ItemManager.itemList.ToDictionary(item => item.shortname, item => item);
            ConfigVerify();
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return;
            var container = entity as LootContainer;
            var player = info?.InitiatorPlayer;
            if (player == null || container == null) return;
            PopulateLoot(container, GetMultiplier(player));
            handledContainers.Remove(container);
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is LootContainer)) return;
            var container = (LootContainer)entity;
            PopulateLoot(container, GetMultiplier(player));
        }
        #endregion

        #region CORE

        int GetMultiplier(BasePlayer player)
        {
            if (PermissionService.HasPermission(player.userID, PERM_X2))
                return 2;
            return MULTIPLIER_DEFAULT;
        }

        void PopulateLoot(LootContainer loot, int mul = 1)
        {
            if (handledContainers.Contains(loot) || loot.ShortPrefabName == "stocking_large_deployed" || loot.ShortPrefabName == "stocking_small_deployed") return;
            handledContainers.Add(loot);
            var inv = loot.inventory;
            for (int i = inv.itemList.Count - 1; i >= 0; i--)
            {
                inv.itemList[i].Remove();
            }
            inv.itemList.Clear();
            LootStorage lootStorage;
            if (lootConfig.TryGetValue(loot.ShortPrefabName, out lootStorage))
            {
                if (loot.ShortPrefabName.Contains("barrel"))
                {
                    loot.inventory.capacity = 2;
                }
                if (loot.ShortPrefabName.Contains("crate_normal"))
                {
                    loot.inventory.capacity = 4;
                }
                bool set;
                foreach (var lootItem in lootStorage.GetRandomItems(inv.capacity, out set))
                {
                    var def = itemDefinitions[lootItem.shortname];
                    var amount = Math.Min(set ? lootItem.amount : lootItem.amount * mul, def.stackable);
                    inv.AddItem(def, amount);
                }
            }
            else
            {
                loot.PopulateLoot();
            }
        }

        private bool IgnoreContainer( LootContainer loot)
        {
            if (handledContainers.Contains(loot)) return false;
            handledContainers.Add( loot );
            return true;
        }

        #region LOOT PREFABS

        void ConfigVerify()
        {
            var lootConfigKeys = lootConfig.Keys.ToList();
            foreach (var lootContainer in lootPrefabs.Except(lootConfigKeys))
            {
                Puts($"RUST Added new loot container: {lootContainer}");
            }
            LootConfigFile.WriteObject(lootConfig);
            Dictionary<ItemCategory, List<string>> l = new Dictionary<ItemCategory, List<string>>();
            foreach (var item in ItemManager.itemList)
            {
                if (!l.ContainsKey(item.category))
                    l[item.category] = new List<string>();
                l[item.category].Add(item.shortname);
            }

            foreach (var ds in l)
            {
                Interface.Oxide.DataFileSystem.GetFile(string.Concat("itemlist/", ds.Key.ToString(), ".txt")).WriteObject(ds.Value);
            }
        }

        private FieldInfo preProcessedField = typeof(GameManager).GetField("preProcessed",
           BindingFlags.Instance | BindingFlags.NonPublic);

        private FieldInfo prefabListField = typeof(PrefabPreProcess).GetField("prefabList",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private string[] igoneredPrefabs = new[]
        {
            "prefabs/building", "deployable", "prefabs/player", "prefabs/fx",
            "ui/lootpanels", "content/ui", "content/unimplemented", "prefabs/weapon",
            "prefabs/misc/xmas", "radtown/dmloot"
        };
        public List<string> GetLootPrefabs()
        {
            var prefabPreProcess = (PrefabPreProcess)preProcessedField.GetValue(GameManager.server);
            var prefabs = (Dictionary<string, GameObject>)prefabListField.GetValue(prefabPreProcess);
            return prefabs.Where(prefab => !prefab.Key.ContainsAny(igoneredPrefabs))
                          .Select(prefab => prefab.Value.GetComponent<LootContainer>()?.ShortPrefabName)
                          .Where(prefab => prefab != null).ToList();
        }

        /*[ChatCommand("spawn")]
        void cmdSpawn(BasePlayer player, string command, string[] args)
        {
            UnityEngine.Object.FindObjectsOfType<LootContainer>().ToList().ForEach(p=>p.Kill());
            for (int i = 0; i < 100; i++)
            {
                
            var prefab = args[0];
            var entity = GameManager.server.CreateEntity(prefab, player.GetNetworkPosition()+Vector3.up*i);
            entity.Spawn();
            }
        }*/

        #endregion

        float GetRandom(float min, float max) => UnityEngine.Random.Range(min, max);

        #endregion

        #region DATA

        private static readonly DynamicConfigFile LootConfigFile = Interface.Oxide.DataFileSystem.GetFile("Core/LootConfig");
        private static readonly DynamicConfigFile LootCasesFile = Interface.Oxide.DataFileSystem.GetFile("Core/LootCases");

        void LoadData()
        {
            lootConfig = LootConfigFile.ReadObject<Dictionary<string, LootStorage>>();
            lootCases = LootCasesFile.ReadObject<Dictionary<string, LootCase>>();
            if (lootCases.Count == 0)
            {
                lootCases.Add("mycase", new LootCase() { items = new List<LootItem>() { new LootItem() { shortname = "wood", amount = 100, chance = 1f } } });

                LootCasesFile.WriteObject(lootCases);
            }
        }


        #endregion

    }
}
