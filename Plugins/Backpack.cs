// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using UnityEngine;
using Physics = UnityEngine.Physics;
using RustyCore;

namespace Oxide.Plugins
{
    [Info("Backpack", "bazuka5801","1.1.0")]
    public class Backpack : RustPlugin
    {

        #region Classes

        public class BackpackBox : MonoBehaviour
        {
            public static readonly List<int> sizes = new List<int>() {6,15,30};

            StorageContainer storage;
            BasePlayer owner;

            public void Init(StorageContainer storage, BasePlayer owner)
            {
                this.storage = storage;
                this.owner = owner;
            }
            
            public static BackpackBox Spawn(BasePlayer player,  int size = 1)
            {
                player.EndLooting();
                var storage = SpawnContainer(player,size,false);
                var box = storage.gameObject.AddComponent<BackpackBox>();
                box.Init(storage, player);
                return box;
            }

            static int rayColl = LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "Resource", "World", "Water", "Default", "Prevent Building");

            public static StorageContainer SpawnContainer (BasePlayer player, int size, bool die)
            {
                var pos = player.transform.position;
                if (die)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(new Ray(player.GetCenter(), Vector3.down), out hit, 1000, rayColl, QueryTriggerInteraction.Ignore))
                    {
                        pos = hit.point;
                    }
                }
                else
                {
                    pos -= new Vector3(0, 100, 0);
                }
                return SpawnContainer(player, size, pos);
            }

            private static StorageContainer SpawnContainer(BasePlayer player, int size, Vector3 position)
            {
                var storage = GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab") as StorageContainer;
                if(storage == null) return null;
                storage.transform.position = position;
                storage.panelName = "largewoodbox";
                ItemContainer container = new ItemContainer { playerOwner = player };
                container.ServerInitialize((Item)null, sizes[size]);
                if((int)container.uid == 0)
                    container.GiveUID();
                storage.inventory = container;
                if(!storage) return null;
                storage.SendMessage("SetDeployedBy", player, (SendMessageOptions)1);
                storage.Spawn();
                return storage;
            }

            private void PlayerStoppedLooting(BasePlayer player)
            {
                Interface.Oxide.RootPluginManager.GetPlugin("Backpack").Call("BackpackHide", player.userID);
            }

            public void Close()
            {
                ClearItems();
                storage.Kill();
            }

            public void StartLoot()
            {
                storage.SetFlag(BaseEntity.Flags.Open, true, false);
                owner.inventory.loot.StartLootingEntity(storage, false);
                owner.inventory.loot.AddContainer(storage.inventory);
                owner.inventory.loot.SendImmediate();
                owner.ClientRPCPlayer(null, owner, "RPC_OpenLootPanel", storage.panelName);
                storage.DecayTouch();
                storage.SendNetworkUpdate();
            }

            public void Push(List<Item> items)
            {
                for (int i = items.Count - 1; i >= 0; i--)
                    items[i].MoveToContainer(storage.inventory);
            }

            public void ClearItems()
            {
                storage.inventory.itemList.Clear();
            }

            public List<Item> GetItems => storage.inventory.itemList.Where(i => i != null).ToList();

        }

        #endregion

        #region VARIABLES

        private RCore core = Interface.Oxide.GetLibrary<RCore>();

        //public List<BackpackBox> spawnedBackpacks = new List<BackpackBox>();
        public Dictionary<ulong, BackpackBox> openedBackpacks = new Dictionary<ulong, BackpackBox>();
        public Dictionary<ulong, List<SavedItem>> savedBackpacks;
        public Dictionary<ulong, BaseEntity> visualBackpacks = new Dictionary<ulong, BaseEntity>();
        private List<ulong> guiCache = new List<ulong>();

        #endregion

        #region DATA

        DynamicConfigFile backpacksFile = Interface.Oxide.DataFileSystem.GetFile("Backpack_Data");

        void LoadBackpacks()
        {
            try
            {
                savedBackpacks = backpacksFile.ReadObject<Dictionary<ulong, List<SavedItem>>>();
            }
            catch (Exception)
            {
                savedBackpacks = new Dictionary<ulong, List<SavedItem>>();
            }
        }

        void SaveBackpacks() => backpacksFile.WriteObject(savedBackpacks);

        #endregion

        #region OXIDE HOOKS
        void OnEntityDeath(BaseCombatEntity ent, HitInfo info) { 
            if (!(ent is BasePlayer)) return;
            var player = (BasePlayer) ent;
            if (InDuel(player)) return;
            BackpackHide(player.userID);
            
                List<SavedItem> savedItems;
                List<Item> items = new List<Item>();
                if (savedBackpacks.TryGetValue(player.userID, out savedItems))
                {
                    items = RestoreItems(savedItems);
                    savedBackpacks.Remove(player.userID);
                }
                if (items.Count <= 0) return;
                var container = BackpackBox.SpawnContainer(player, GetBackpackSize(player), true);
                if (container == null) return;
                for (int i = items.Count - 1; i >= 0; i--)
                    items[i].MoveToContainer(container.inventory);
                timer.Once(300f, () =>
                {
                    if (container != null && !container.IsDestroyed)
                        container.Kill();
                });
                Effect.server.Run("assets/bundled/prefabs/fx/dig_effect.prefab", container.transform.position);
            
        }

        void Loaded()
        {
            LoadBackpacks();
            PermissionService.RegisterPermissions(this, permisions);
        }

        void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList) DrawUI(player);
        }

        void Unload()
        {
            var keys = openedBackpacks.Keys.ToList();
            for (int i = openedBackpacks.Count - 1; i >= 0; i--)
                BackpackHide(keys[i]);
            SaveBackpacks();
        }

        void OnPreServerRestart()
        {
            foreach (var dt in Resources.FindObjectsOfTypeAll<StashContainer>())
                dt.Kill();
            foreach (var ent in Resources.FindObjectsOfTypeAll<TimedExplosive>().Where(ent => ent.name == "backpack"))
                ent.KillMessage();
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            DrawUI(player);
        }

        void OnPlayerAspectChanged(BasePlayer player)
        {
            DrawUI(player);
        }
        #endregion

        #region FUNCTIONS

        void BackpackShow(BasePlayer player)
        {
            if (BackpackHide(player.userID)) return;

            if (player.inventory.loot?.entitySource != null) return;
            
            timer.Once(0.1f, () =>
            {
                if (!player.IsOnGround())return;
                List<SavedItem> savedItems;
                List<Item> items = new List<Item>();
                if (savedBackpacks.TryGetValue(player.userID, out savedItems))
                    items = RestoreItems(savedItems);
                var backpackSize = GetBackpackSize(player);
                BackpackBox box = BackpackBox.Spawn(player, backpackSize);
                openedBackpacks.Add(player.userID, box);
                if (items.Count > 0)
                    box.Push(items);
                box.StartLoot();
            });
        }

        int GetBackpackSize(BasePlayer player)
        {
            for (int i = permisions.Count-1; i >= 0; i--)
                if (PermissionService.HasPermission(player, permisions[i]) && (i == 0 || PermissionService.HasPermission(player, permisions[i-1])))
                    return i+1;
                else if (i > 0 && PermissionService.HasPermission(player, permisions[i])) return i;
            return 0;
        }
        

        [HookMethod("BackpackHide")]
        bool BackpackHide(ulong playerId)
        {
            BackpackBox box;
            if (!openedBackpacks.TryGetValue(playerId, out box)) return false;
            openedBackpacks.Remove(playerId);
            if (box == null) return false;
            var items = SaveItems(box.GetItems);
            if (items.Count > 0)
            {
                savedBackpacks[playerId] = SaveItems(box.GetItems);
                //SpawnVisual(BasePlayer.FindByID(player));
            }
            else { savedBackpacks.Remove(playerId); //RemoveVisual(BasePlayer.FindByID(player));
            }
            box.Close();
            var player = BasePlayer.FindByID(playerId);
            if (player)
                DrawUI(player);
            return true;
        }


        #endregion

        #region UI

        void DrawUI(BasePlayer player)
        {
            if (!guiCache.Contains( player.userID ))
            {
                guiCache.Add( player.userID );
            }
            var backpackSize = GetBackpackSize( player );

            List<SavedItem> savedItems;
            List<Item> items = new List<Item>();
            if (!savedBackpacks.TryGetValue( player.userID, out savedItems ))
                savedItems = new List<SavedItem>();
            int backpackCount = savedItems?.Count ?? 0;
            core.DrawUI( player, "Backpack", "menu", backpackCount, BackpackBox.sizes[ backpackSize ] );

        }

        void DestroyUI(BasePlayer player)
        {
            //if (guiCache.Contains(player.userID))
            //{
            //    guiCache.Remove(player.userID);
            //    core.DestroyUI(player, "Backpack", "menu");
            //}
        }

        #endregion

        #region COMMANDS

        [ChatCommand("backpack.show")]
        void cmdBackpackShow(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            /*if (!player.IsAdmin)
            {
                player.ChatMessage("Рюкзак на тех работах, не беспокойтесь, ваши вещи не пропадут!");
                return;
            }*/
            BackpackShow(player);
        }

        [ConsoleCommand("backpack.gui.show")]
        void cmdOnBackPackShowClick(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (player.inventory.loot?.entitySource != null)
            {
                BackpackBox bpBox;
                if (openedBackpacks.TryGetValue(player.userID, out bpBox) &&
                    bpBox.gameObject == player.inventory.loot.entitySource.gameObject)
                {
                    return;
                }
                player.EndLooting();
                NextTick(() => BackpackShow(player));
                return;
            }
            else
            {
                BackpackShow(player);
            }
        }

        #endregion

        #region ITEM EXTENSION

        public class SavedItem
        {
            public string shortname;
            public int itemid;
            public float condition;
            public float maxcondition;
            public int amount;
            public int ammoamount;
            public string ammotype;
            public int flamefuel;
            public ulong skinid;
            public bool weapon;
            public List<SavedItem> mods;
        }

        List<SavedItem> SaveItems(List<Item> items) => items.Select(SaveItem).ToList();

        SavedItem SaveItem(Item item)
        {
            SavedItem iItem = new SavedItem
            {
                shortname = item.info?.shortname,
                amount = item.amount,
                mods = new List<SavedItem>(),
                skinid = item.skin
            };
            if (item.info == null) return iItem;
            iItem.itemid = item.info.itemid;
            iItem.weapon = false;
            if (item.hasCondition)
            {
                iItem.condition = item.condition;
                iItem.maxcondition = item.maxCondition;
            }
            FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();
            if(flameThrower != null)
                iItem.flamefuel = flameThrower.ammo;
            if (item.info.category.ToString() != "Weapon") return iItem;
            BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon == null) return iItem;
            if (weapon.primaryMagazine == null) return iItem;
            iItem.ammoamount = weapon.primaryMagazine.contents;
            iItem.ammotype = weapon.primaryMagazine.ammoType.shortname;
            iItem.weapon = true;
            if (item.contents != null)
                foreach (var mod in item.contents.itemList)
                    if (mod.info.itemid != 0)
                        iItem.mods.Add(SaveItem(mod));
            return iItem;
        }

        List<Item> RestoreItems(List<SavedItem> sItems)
        {
            return sItems.Select(sItem =>
            {
                if (sItem.weapon) return BuildWeapon(sItem);
                return BuildItem(sItem);
            }).Where(i => i != null).ToList();
        }

        Item BuildItem(SavedItem sItem)
        {
            if (sItem.amount < 1) sItem.amount = 1;
            Item item = ItemManager.CreateByItemID(sItem.itemid, sItem.amount, sItem.skinid);
            if (item.hasCondition)
            {
                item.condition = sItem.condition;
                item.maxCondition = sItem.maxcondition;
            }
            FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();
            if(flameThrower)
                flameThrower.ammo = sItem.flamefuel;
            return item;
        }

        Item BuildWeapon(SavedItem sItem)
        {
            Item item = ItemManager.CreateByItemID(sItem.itemid, 1, sItem.skinid);
            if (item.hasCondition)
            {
                item.condition = sItem.condition;
                item.maxCondition = sItem.maxcondition;
            }
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                var def = ItemManager.FindItemDefinition(sItem.ammotype);
                weapon.primaryMagazine.ammoType = def;
                weapon.primaryMagazine.contents = sItem.ammoamount;
            }

            if (sItem.mods != null)
                foreach (var mod in sItem.mods)
                    item.contents.AddItem(BuildItem(mod).info, 1);
            return item;
        }

        #endregion

        #region EXTERNAL CALLS

        [PluginReference] Plugin Duels;

        bool InDuel(BasePlayer player) => Duels?.Call<bool>("inDuel", player) ?? false;

        #endregion
        

        public List<string> permisions = new List<string>()
        {
            "backpack.size1",
            "backpack.size2"
        };

        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                if(player == null || string.IsNullOrEmpty(permissionName))
                    return false;

                var uid = player.UserIDString;
                if(permission.UserHasPermission(uid, permissionName))
                    return true;

                return false;
            }

            public static void RegisterPermissions(Plugin owner, List<string> permissions)
            {
                if(owner == null) throw new ArgumentNullException("owner");
                if(permissions == null) throw new ArgumentNullException("commands");

                foreach(var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }
    }
}
