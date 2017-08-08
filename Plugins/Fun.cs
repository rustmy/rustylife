using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Fun", "bazuka5801", "1.0.0")]
    public class Fun : RustPlugin
    {
        #region CLASSES

        public class CustomItem
        {
            public string Id;
            public int Amount;

            public CustomItem(string id, int amount)
            {
                this.Id = id;
                this.Amount = amount;
            }
        }

        class WeaponSet
        {
            public List<CustomItem> belt = new List<CustomItem>();
            public List<CustomItem> main = new List<CustomItem>();
            public List<string> wear = new List<string>();
        }

        #endregion

        #region VARIABLES

        int MagazineScale = 3;

        float healAmount = 50f; // Instant heal amount

        WeaponSet kit;

        DynamicConfigFile funKitFile = Interface.Oxide.DataFileSystem.GetFile("FunSet");

        #endregion


        /// <summary>
        /// Called after the server startup has been completed and is awaiting connections
        /// </summary>
        void OnServerInitialized()
        {
            rust.RunServerCommand("unload ZLevelsRemastered");
            SetupWeapons();
            LoadKit();
        }

        /// <summary>
        /// Called when a plugin is being unloaded
        /// </summary>
        void Unload()
        {
            UnloadWeapons();
        }


        /// <summary>
        /// Called when the player attempts to equip an item
        /// </summary>
        /// <param name="inventory">Player Inventory</param>
        /// <param name="item">Item</param>
        /// <returns></returns>
        object CanEquipItem(PlayerInventory inventory, Item item)
        {
            if (item.info.shortname == "rocket.launcher") return false;
            return null;
        }

        /// <summary>
        /// Called right before the condition of the item is modified
        /// </summary>
        /// <param name="item">Item</param>
        /// <param name="amount">Amount</param>
        void OnLoseCondition(Item item, ref float amount)
        {
            if (item != null)
            {
                if (item.hasCondition)
                    item.RepairCondition(amount);
            }
        }

        /// <summary>
        /// Called right before a Syringe or Medkit item is used
        /// </summary>
        /// <param name="item">Item</param>
        /// <param name="target">Target</param>
        /// <returns></returns>
        object OnHealingItemUse(HeldEntity item, BasePlayer target)
        {
            if (item is MedicalTool && item.ShortPrefabName.Contains("syringe"))
            {
                target.health = target.health + healAmount;
                return true;
            }
            return null;
        }

        /// <summary>
        /// Called after any networked entity has spawned (including trees)
        /// </summary>
        /// <param name="entity">Entity</param>
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            // Remove player corpse
            if (entity is BaseCorpse)
            {
                timer.Once(1f, entity.KillMessage);
            }
        }

        #region KIT

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player?.inventory?.containerMain == null)
            {
                timer.Once(0.5f, () => OnPlayerRespawned(player));
                return;
            }
            if (player.net?.connection == null)return;

            GiveKit(player);
            player.health = 100;
            player.metabolism.calories.Increase(500);
            player.metabolism.hydration.Increase(500);
            player.SendNetworkUpdateImmediate();
        }

        void LoadKit()
        {
            try
            {
                kit = funKitFile.ReadObject<WeaponSet>() ?? new WeaponSet();
            }
            catch
            {
                kit = new WeaponSet();
            }
        }

        [ChatCommand("sv")]
        void cmdChatSaveItems(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            kit = new WeaponSet();
            player.inventory.containerBelt.itemList.Sort((i, j) => i.position.CompareTo(j.position));
            player.inventory.containerMain.itemList.Sort((i, j) => i.position.CompareTo(j.position));
            player.inventory.containerWear.itemList.Sort((i, j) => i.position.CompareTo(j.position));

            foreach (var item in player.inventory.containerBelt.itemList)
                kit.belt.Add(item == null
                    ? new CustomItem("", 0)
                    : new CustomItem(item.info.itemid.ToString(), item.amount));

            foreach (var item in player.inventory.containerMain.itemList)
                kit.main.Add(item == null
                    ? new CustomItem("", 0)
                    : new CustomItem(item.info.itemid.ToString(), item.amount));

            foreach (var item in player.inventory.containerWear.itemList)
                kit.wear.Add(item?.info.itemid.ToString() ?? "");

            funKitFile.WriteObject(kit);
            player.ChatMessage("Save completed");
        }

        void GiveKit(BasePlayer player)
        {
            player.inventory.Strip();
            for (int j = 0; j < kit.belt.Count; j++)
            {
                var customItem = kit.belt[j];
                if (customItem.Id == "")
                    continue;
                var item = ItemManager.CreateByItemID(int.Parse(customItem.Id), customItem.Amount);
                item.MoveToContainer(player.inventory.containerBelt, j, false);
            }
            for (int j = 0; j < kit.main.Count; j++)
            {
                var customItem = kit.main[j];
                if (customItem.Id == "")
                    continue;
                var item = ItemManager.CreateByItemID(int.Parse(customItem.Id), customItem.Amount);
                item.MoveToContainer(player.inventory.containerMain, j, false);
            }
            for (int j = 0; j < kit.wear.Count; j++)
            {
                var customItem = kit.wear[j];
                if (customItem == "")
                    continue;
                var item = ItemManager.CreateByItemID(int.Parse(customItem));
                item.MoveToContainer(player.inventory.containerWear, j, false);
            }
        }

        #endregion

        #region WEAPONS

        void SetupWeapons()
        {
            foreach (var baseProjectile in GetWeapons())
            {
                var magazine = baseProjectile.primaryMagazine;
                if (magazine != null)
                {
                    magazine.definition.builtInSize *= MagazineScale;
                    magazine.capacity = magazine.definition.builtInSize;
                    magazine.contents = magazine.capacity;
                }
            }
        }

        void UnloadWeapons()
        {
            foreach (var baseProjectile in GetWeapons())
            {
                var magazine = baseProjectile.primaryMagazine;
                if (magazine != null)
                {
                    magazine.definition.builtInSize /= MagazineScale;
                    magazine.capacity = magazine.definition.builtInSize;
                    magazine.contents = magazine.capacity;
                }
            }
        }

        List<BaseProjectile> GetWeapons()
        {
            List<BaseProjectile> list = new List<BaseProjectile>();
            foreach (var def in ItemManager.itemList)
            {
                if (def.shortname != "pistol.eoka" && def.shortname != "grenade.beancan")
                {
                    var modEntity = def.GetComponent<ItemModEntity>();
                    if (modEntity != null && modEntity.entityPrefab != null)
                    {
                        var prefab = modEntity.entityPrefab.Get();
                        BaseProjectile baseProjectile = prefab.GetComponent<BaseProjectile>();
                        if (baseProjectile != null) list.Add(baseProjectile);
                    }
                }
            }
            return list;
        }

        #endregion

        #region RAID AMMO

        private List<string> damageTypes = new List<string>()
        {
            Rust.DamageType.Bullet.ToString(),
            Rust.DamageType.Blunt.ToString(),
            Rust.DamageType.Stab.ToString(),
            Rust.DamageType.Slash.ToString(),
            Rust.DamageType.Explosion.ToString(),
            Rust.DamageType.Heat.ToString(),
        };

        private List<string> prefabs = new List<string>()
        {
            "door.hinged",
            "door.double.hinged",
            "window.bars",
            "floor.ladder.hatch",
            "floor.frame",
            "wall.frame",
            "shutter",
            "external"
        };

        /// <summary>
        /// Alternatively, modify the hitInfo object to change the damage
        /// </summary>
        /// <param name="entity">victim</param>
        /// <param name="hitInfo">hitInfo has all kinds of useful things in it, such as hitInfo.Weapon, hitInfo.damageAmount or hitInfo.damageType</param>
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || hitInfo.WeaponPrefab == null || hitInfo.Initiator == null || !IsEntityBlocked(entity))
            {
                return;
            }

            if (hitInfo.Initiator.transform == null)
            {
                return;
            }

            if (damageTypes.Contains(hitInfo.damageTypes.GetMajorityDamageType().ToString()))
            {
                hitInfo.damageTypes.Set(hitInfo.damageTypes.GetMajorityDamageType(),18);
            }
        }

        public bool IsEntityBlocked(BaseCombatEntity entity)
        {
            if (entity is BuildingBlock)
            {
                if (((BuildingBlock)entity).grade == BuildingGrade.Enum.Twigs)
                {
                    return false;
                }
                return true;
            }

            var prefabName = entity.ShortPrefabName;

            foreach (string p in prefabs)
                if (prefabName.IndexOf(p) != -1)
                    return true;

            return false;
        }
        bool IsDamageBlocking(Rust.DamageType dt)
        {
            switch (dt)
            {
                case Rust.DamageType.Bullet:
                case Rust.DamageType.Stab:
                case Rust.DamageType.Explosion:
                case Rust.DamageType.ElectricShock:
                case Rust.DamageType.Heat:
                    return true;
            }
            return false;
        }
        private List<string> GetDefaultDamageTypes()
        {
            return new List<string>()
            {
                Rust.DamageType.Bullet.ToString(),
                Rust.DamageType.Blunt.ToString(),
                Rust.DamageType.Stab.ToString(),
                Rust.DamageType.Slash.ToString(),
                Rust.DamageType.Explosion.ToString(),
                Rust.DamageType.Heat.ToString(),
            };
        }


        
        #endregion
    }
}
