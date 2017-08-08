using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom Quarry","bazuka5801","1.0.0")]
    public class CustomQuarry:RustPlugin
    {

        Dictionary<MiningQuarry, int> fuelConsumable = new Dictionary<MiningQuarry, int>();
        List<MiningQuarry> quaries = new List<MiningQuarry>();
        
        Dictionary<ulong, List<MiningQuarry>> playersQuarries = new Dictionary<ulong, List<MiningQuarry>>();
        
        Dictionary<string, string> itemsLoaclization = new Dictionary<string, string>()
        {
            {"hq.metal.ore", "МВК руда"},
            {"metal.ore", "Железная руда"},
            {"sulfur.ore", "Серная руда"},
            {"stones", "Камни"},
            {"metal.fragments", "Метал. фраг." }
        };

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            PermissionService.RegisterPermissions(this,permisions);
            quaries = UnityEngine.Object.FindObjectsOfType<MiningQuarry>().ToList();
            foreach (var quarry in quaries)
                AddPlayerQuarry(quarry.OwnerID, quarry);
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!(entity is MiningQuarry)) return;
            var mine = (MiningQuarry) entity;
            quaries.Add(mine);
            AddPlayerQuarry(mine.OwnerID, mine);
        }
        void OnEntityKill(BaseNetworkable ent)
        {
            if (!(ent is MiningQuarry)) return;
            var mine = (MiningQuarry)ent;
            quaries.Remove(mine);
            RemovePlayerQuarry(mine.OwnerID, mine);
        }
        void OnItemUse(Item item, int amount)
        {
            if (item.info.shortname != "lowgradefuel") return;
            MiningQuarry quarry = item?.parent?.entityOwner?.GetParentEntity() as MiningQuarry;
            if (quarry != null)
            {
                var uid = quarry.OwnerID;
                if (uid == 0) return;
                int fuelDiv = 1;
                if (PermissionService.HasPermission(uid, PERM_FUEL_DIV_4))
                    fuelDiv = 4; else if (PermissionService.HasPermission(uid, PERM_FUEL_DIV_2)) fuelDiv = 2;
                if (!fuelConsumable.ContainsKey(quarry))
                    fuelConsumable[quarry] = 0;
                var fuel = fuelConsumable[quarry];
                fuel++;
                if (fuel < fuelDiv)
                    item.amount += 1;
                else fuel = 0;
                fuelConsumable[quarry] = fuel;
            }

        }

        void AddPlayerQuarry(ulong userId, MiningQuarry quarry)
        {
            if (userId == 0) return;
            List<MiningQuarry> playerquarries;
            if (!playersQuarries.TryGetValue(userId, out playerquarries))
                playersQuarries[userId] = playerquarries = new List<MiningQuarry>();
            playerquarries.Add(quarry);
        }

        void RemovePlayerQuarry(ulong userId, MiningQuarry quarry)
        {
            if (userId == 0) return;
            List<MiningQuarry> playerquarries;
            if (playersQuarries.TryGetValue(userId, out playerquarries))
            {
                playerquarries.Remove(quarry);
                if (playerquarries.Count == 0)
                    playersQuarries.Remove(userId);
            }
        }

        private FieldInfo linkedDeposit = typeof(MiningQuarry).GetField("_linkedDeposit",
            BindingFlags.Instance | BindingFlags.NonPublic);

        #endregion

        #region COMMANDS

        [ChatCommand("mine")]
        void cmdChatMine(BasePlayer player, string cmd, string[] args)
        {
            var mines = quaries.Where(q => q.OwnerID == player.userID).ToList();
            if (mines.Count == 0)
            {
                player.ChatMessage("У вас нет горнорудных карьеров");
                return;
            }
            var msg = new StringBuilder();
            msg.Append("<color=red><size=16>Custom Quarry</size></color>\n");
            int i = 1;
            foreach (var mine in mines)
            {
                List<string> items = new List<string>();
                foreach (var item in ((ResourceDepositManager.ResourceDeposit)linkedDeposit.GetValue(mine))._resources)
                    items.Add($"{itemsLoaclization[item.type.shortname]}");

                var container = mine.hopperPrefab.instance.GetComponent<StorageContainer>().inventory;

                Dictionary<string, int> itemsCount = new Dictionary<string, int>();
                foreach (var item in container.itemList.Where(it => it!=null))
                {
                    var name = itemsLoaclization.ContainsKey(item.info.shortname)
                        ? itemsLoaclization[item.info.shortname]
                        : item.name;
                    if (name == null)continue;
                    if (!itemsCount.ContainsKey(name))
                        itemsCount.Add(name, 0);
                    itemsCount[name] += item.amount;
                }
                var count = string.Join("\n", itemsCount.Select(p => $"    <color=yellow>{p.Value}</color>x{p.Key}").ToArray());

                msg.Append($"<color=orange><size=14>{i}: ({string.Join(", ",items.ToArray())}) {mine.transform.position} Осталось <color=red>{GetFuel(mine)}</color> топлива\nНакопал:\n\r{count}</size></color>\n");
                i++;
            }
            player.ChatMessage(msg.ToString());
        }


        int GetFuel(MiningQuarry mine)
        {
            return mine.fuelStoragePrefab.instance.GetComponent<StorageContainer>()
                .inventory.GetAmount(28178745, true);
        }

        #endregion

        #region API

        Dictionary<Vector3, int> GetPlayerQuarries(ulong userId)
        {
            var dict = new Dictionary<Vector3,int>();
            List<MiningQuarry> playerQuarries;
            if (playersQuarries.TryGetValue(userId, out playerQuarries))
                for (int i = playerQuarries.Count - 1; i >= 0; i--)
                {
                    var q = playerQuarries[i];
                    if (q == null)
                    {
                        playerQuarries.RemoveAt(i);
                        continue;
                    }
                    var t = q.transform;
                    if (t == null)
                    {
                        playerQuarries.RemoveAt(i);
                        continue;
                    }
                    dict.Add(t.position, GetFuel(q));
                }
            return dict.Count > 0 ? dict : null;
        }

        #endregion

        #region PERMISSION SERVICE
        const string PERM_FUEL_DIV_2 = "customquarry.fueldiv2";
        const string PERM_FUEL_DIV_4 = "customquarry.fueldiv4";

        public List<string> permisions = new List<string>()
        {
            PERM_FUEL_DIV_2,
            PERM_FUEL_DIV_4
        };

        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission(ulong uid, string permissionName)
            {
                if (string.IsNullOrEmpty(permissionName))
                    return false;
                
                if (permission.UserHasPermission(uid.ToString(), permissionName))
                    return true;

                return false;
            }

            public static void RegisterPermissions(Plugin owner, List<string> permissions)
            {
                if (owner == null) throw new ArgumentNullException("owner");
                if (permissions == null) throw new ArgumentNullException("commands");

                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }
        #endregion
    }
}
