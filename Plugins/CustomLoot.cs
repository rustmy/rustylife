using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("CustomLoot", "bazuka5801", "1.0.0")]
    public class CustomLoot : RustPlugin
    {
        Regex barrelEx = new Regex("loot-barrel|loot_barrel|loot_trash".ToLower());
        Regex componentEx = new Regex("propanetank|tarp|rope|roadsigns|glue|ducttape|riflebody|smgbody|semibody|sheetmetal|metalspring|metalpipe|metalblade|sewingkit|bleach|techparts|techtrash|gears");
        
        ItemDefinition metalpipe = ItemManager.FindItemDefinition("metalpipe");
        ItemDefinition techparts = ItemManager.FindItemDefinition("techparts");
        ItemDefinition riflebody = ItemManager.FindItemDefinition("riflebody");
        ItemDefinition semibody = ItemManager.FindItemDefinition("semibody");
        ItemDefinition smgbody = ItemManager.FindItemDefinition("smgbody");
        ItemDefinition gears = ItemManager.FindItemDefinition("gears");
        ItemDefinition metalspring = ItemManager.FindItemDefinition("metalspring");

        private Dictionary<string, int> componentAmount = new Dictionary<string, int>()
        {
            {"tarp", 1},
            {"rope", 2},
            {"sewingkit", 3},
            {"metalspring", 1},
            {"metalblade", 1},
            {"propanetank", 1},
            {"gears", 2},
            {"roadsigns", 1},
            {"sheetmetal", 1},
            {"metalpipe", 1},
            {"semibody", 1},
            {"techparts", 1},
            {"smgbody", 1},
            {"riflebody", 1},
            {"techtrash",1 }
        };
        

        List<LootContainer> handledContainers = new List<LootContainer>();

        void OnServerInitialized()
        {
            PermissionService.RegisterPermissions(this,permisions);
            var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            containers =
                containers.Where(
                    c =>
                        c.ShortPrefabName != "stocking_large_deployed" ||
                        c.ShortPrefabName != "stocking_small_deployed").ToArray();
            int i = 0;
            foreach (var container in containers)
            {
                container.SpawnLoot();
                if (container.inventory.itemList.Any(item => item.info.shortname == "metalpipe")) i++;
            }
            Puts("Populated {0} continers", containers.Length);
            Puts($"Containers with metalpipe {i}");
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return;
            var container = entity as LootContainer;
            var player = info?.InitiatorPlayer;
            if (player == null || container == null) return;
            UpdateLoot(container,player);
            handledContainers.Remove(container);
        }

        void UpdateLoot(LootContainer container, BasePlayer player)
        {
            if (handledContainers.Contains(container) || container.ShortPrefabName == "stocking_large_deployed" || container.ShortPrefabName == "stocking_small_deployed")return;
            var multiplier = GetMultiplier(player);
            AddComponents(container, player);
            foreach (var item in container.inventory.itemList.Where(i => i != null))
                if (componentEx.IsMatch(item.info.shortname))
                {
                    if (!componentAmount.ContainsKey(item.info.shortname))
                        componentAmount[item.info.shortname] = item.amount;
                    item.amount = componentAmount[item.info.shortname] * multiplier;
                }
            handledContainers.Add(container);
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is LootContainer)) return;
            var container = (LootContainer) entity;
            UpdateLoot(container, player);
        }

        int GetMultiplier(BasePlayer player)
        {
            if (PermissionService.HasPermission(player, PERM_X5))
                return 5;
            if (PermissionService.HasPermission(player, PERM_X4))
                return 4;
            return 3;
        }
        
        void AddComponents(LootContainer container,BasePlayer player)
        {
            var num = UnityEngine.Random.Range(0, 6);
            if (container == null) Puts("cont == null");
            if (num == 0 && UnityEngine.Random.Range(0, 10) == 5)
            {
                var component = metalpipe;
                if (metalpipe == null) Puts("mp == null");
                if (container.inventory.GetAmount(component.itemid, false) > 0) return;
                container.inventory.AddItem(component, 1);
            }
            if (num == 1 && UnityEngine.Random.Range(0, 15) == 5)
            {
                var component = techparts;
                if (techparts == null) Puts("tc == null");
                if (container.inventory.GetAmount(component.itemid, false) > 0) return;
                container.inventory.AddItem(component, 1);
            }
            if (num == 1 && UnityEngine.Random.Range(0, 10) == 5)
            {
                var component = gears;
                if (gears == null) Puts("gears == null");
                if (container.inventory.GetAmount(component.itemid, false) > 0) return;
                container.inventory.AddItem(component, 1);
            }
            if (num == 2 && UnityEngine.Random.Range(0, 20) == 5)
            {
                var component = semibody;
                if (semibody == null) Puts("semibody == null");
                if (container.inventory.GetAmount(component.itemid, false) > 0) return;
                container.inventory.AddItem(component, 1);
            }
            if (num == 3 && UnityEngine.Random.Range(0, 20) == 5)
            {
                var component = riflebody;
                if (riflebody == null) Puts("riflebody == null");
                if (container.inventory.GetAmount(component.itemid, false) > 0) return;
                container.inventory.AddItem(component, 1);
            }
            if (num == 4 && UnityEngine.Random.Range(0, 20) == 5)
            {
                var component = smgbody;
                if (smgbody == null) Puts("smg == null");
                if (container.inventory.GetAmount(component.itemid, false) > 0) return;
                container.inventory.AddItem(component, 1);
            }
            if (num == 5 && UnityEngine.Random.Range(0, 10) == 5)
            {
                var component = metalspring;
                if (metalspring == null) Puts("metalspring == null");
                if (container.inventory.GetAmount(component.itemid, false) > 0) return;
                container.inventory.AddItem(component, 1);
            }
        }

        const string PERM_X4 = "customloot.x4";
        const string PERM_X5 = "customloot.x5";

        public List<string> permisions = new List<string>()
        {
            PERM_X4,
            PERM_X5
        };

        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                if (player == null || string.IsNullOrEmpty(permissionName))
                    return false;

                var uid = player.UserIDString;
                if (permission.UserHasPermission(uid, permissionName))
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

    }
}
