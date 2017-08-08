using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
    [Info("Custom Recycler", "bazuka5801", "1.0.0")]
    public class CustomRecycler : RustPlugin
    {
        #region CLASSES


        public class RecyclerBox : MonoBehaviour
        {
            private const int SIZE = 1;

            StorageContainer storage;
            BasePlayer player;

            public void Init(StorageContainer storage, BasePlayer player)
            {
                this.storage = storage;
                this.player = player;
                storage.inventory.onItemAddedRemoved += (item, insert) => {if (insert)RecycleItem(item);};
            }

            public bool HasRecyclable(Item slot) => slot.info.Blueprint != null;

            void RecycleItem(Item slot)
            {
                    
                bool flag = false;
                if (!HasRecyclable(slot)) return;
                float single = 0.5f;
                if (slot.hasCondition)
                {
                    single = Mathf.Clamp01(single * slot.conditionNormalized * slot.maxConditionNormalized);
                }
                int num = 1;
                if (slot.amount > 1)
                {
                    num = slot.amount;
                }
                if (slot.info.Blueprint.scrapFromRecycle > 0)
                {
                    Item item = ItemManager.CreateByName("scrap", slot.info.Blueprint.scrapFromRecycle * num, (ulong)0);
                    MoveItemToOutput(item);
                }
                slot.UseItem(num);
                foreach (ItemAmount ingredient in slot.info.Blueprint.ingredients)
                {
                    float blueprint = (float)ingredient.amount / (float)slot.info.Blueprint.amountToCreate;
                    int num1 = 0;
                    if (blueprint > 1f)
                    {
                        num1 = Mathf.CeilToInt(Mathf.Clamp(blueprint * single * UnityEngine.Random.Range(1f, 1f), 1f, ingredient.amount) * (float)num);
                    }
                    else
                    {
                        for (int j = 0; j < num; j++)
                        {
                            if (UnityEngine.Random.Range(0f, 1f) <= single)
                            {
                                num1++;
                            }
                        }
                    }
                    if (num1 > 0)
                    {
                        MoveItemToOutput(ItemManager.Create(ingredient.itemDef, num1, (ulong)0));
                    }
                }
            }

            public void MoveItemToOutput(Item newItem)
            {
                if (!newItem.MoveToContainer(player.inventory.containerMain))
                    newItem.Drop(player.GetCenter(), player.GetDropVelocity());
            }

            public static RecyclerBox Spawn(BasePlayer player)
            {
                player.EndLooting();
                var storage = SpawnContainer(player);
                var box = storage.gameObject.AddComponent<RecyclerBox>();
                box.Init(storage, player);
                return box;
            }
            

            private static StorageContainer SpawnContainer(BasePlayer player)
            {
                var position = player.transform.position - new Vector3(0,100,0);

                var storage = GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab") as StorageContainer;
                if (storage == null) return null;
                storage.transform.position = position;
                storage.panelName = "lantern";
                ItemContainer container = new ItemContainer { playerOwner = player };
                container.ServerInitialize((Item)null, SIZE);
                if ((int)container.uid == 0)
                    container.GiveUID();
                storage.inventory = container;
                if (!storage) return null;
                storage.SendMessage("SetDeployedBy", player, (SendMessageOptions)1);
                storage.Spawn();
                return storage;
            }

            private void PlayerStoppedLooting(BasePlayer player)
            {
                Interface.Oxide.RootPluginManager.GetPlugin("CustomRecycler").Call("CloseRecycler", this);
            }

            public void Close()
            {
                foreach (var item in Items)
                    item.MoveToContainer(player.inventory.containerMain);
                ClearItems();
                storage.Kill();
            }

            public void StartLoot()
            {
                storage.SetFlag(BaseEntity.Flags.Open, true, false);
                player.inventory.loot.StartLootingEntity(storage, false);
                player.inventory.loot.AddContainer(storage.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", storage.panelName);
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

            public List<Item> Items => storage.inventory.itemList.Where(i => i != null).ToList();

        }


        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            PermissionService.RegisterPermissions(this, permisions);
        }

        #endregion

        #region COMMANDS

        [ChatCommand("recycler")]
        void cmdChatRecycler(BasePlayer player, string cmd, string[] args)
        {
            if (!PermissionService.HasPermission(player, PERM_USE))
            {
                SendReply(player,"У вас нет доступа к переработчику");return;
            }
            if (InDuel(player)) return;
            OpenRecycler(player);
        }

        #endregion

        #region CORE

        void OpenRecycler(BasePlayer player)
        {
            var box = RecyclerBox.Spawn(player);
            box.StartLoot();
        }

        void CloseRecycler(RecyclerBox box)
        {
            box.Close();
        }

        #endregion

        #region EXTERNAL CALLS

        [PluginReference] Plugin Duels;

        bool InDuel(BasePlayer player) => Duels?.Call<bool>("inDuel", player) ?? false;

        #endregion


        const string PERM_USE = "customrecycler.use";
        public List<string> permisions = new List<string>()
        {
            PERM_USE
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
