using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RustyCore.Utils
{
    public static class StorageBox
    {
        private static Dictionary<string, LootableCorpse> boxes = new Dictionary<string, LootableCorpse>();
        

        public static void Create(Plugin plugin, BasePlayer player, string name, int size)
        {
            var entity = GameManager.server.CreateEntity("assets/prefabs/player/player_corpse.prefab") as BaseCorpse;
            if (entity == null) return;
            entity.parentEnt = null;
            entity.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, player.transform.position.z);
            entity.CancelInvoke("RemoveCorpse");

            var corpse = entity as LootableCorpse;
            if (corpse == null) return;

            ItemContainer container = new ItemContainer { playerOwner = player };
            container.ServerInitialize(null, size);
            if ((int)container.uid == 0)
                container.GiveUID();

            corpse.containers = new ItemContainer[1];
            corpse.containers[0] = container;
            corpse.containers[0].playerOwner = player;

            corpse.playerName = name;
            corpse.lootPanelName = "generic";
            corpse.playerSteamID = 0;
            corpse.enableSaving = false;

            corpse.Spawn();
            corpse.GetComponentInChildren<Rigidbody>().useGravity = false;

            Destroy(plugin,player);

            boxes.Add(GetName(plugin, player), corpse);
        }

        public static void Destroy(Plugin plugin, BasePlayer player)
        {
            string key = GetName(plugin, player);
            LootableCorpse corpse;
            if (boxes.TryGetValue(key, out corpse))
            {
                if (corpse != null && !corpse.IsDestroyed)
                {
                    corpse.children = null;
                    corpse.Kill();
                }
                boxes.Remove(GetName(plugin,player));
            }
        }

        public static void StartLooting(Plugin plugin, BasePlayer player)
        {
            LootableCorpse corpse;
            if (!boxes.TryGetValue(GetName(plugin, player), out corpse) || corpse == null || corpse.IsDestroyed)
            {
                throw new InvalidOperationException(
                    $"StartLooting: StorageBox for {plugin.Name}:{player.displayName} not Found");
            }

            player.inventory.loot.Clear();

            var panel = corpse.lootPanelName;
            corpse.lootPanelName = "generic";
            corpse.SetFlag(BaseEntity.Flags.Open, true, false);
            player.inventory.loot.StartLootingEntity(corpse, false);
            player.inventory.loot.AddContainer(corpse.containers[0]);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic");
            corpse.SendNetworkUpdate();
        }

        public static T AddComponent<T>(Plugin plugin, BasePlayer player) where T : Component
        {
            LootableCorpse corpse;
            if (!boxes.TryGetValue(GetName(plugin, player), out corpse) || corpse == null || corpse.IsDestroyed)
            {
                throw new InvalidOperationException(
                    $"AddBehaviour: StorageBox for {plugin.Name}:{player.displayName} not Found");
            }
            return corpse.gameObject.AddComponent<T>();
        }

        private static string GetName(Plugin plugin, BasePlayer player) => plugin.Name + player.userID;
    }
}
