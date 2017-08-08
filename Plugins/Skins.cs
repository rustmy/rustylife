// Reference: Oxide.Core.RustyCore
// Reference: Rust.Workshop

using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using System.Collections;
using Oxide.Core.Configuration;
using Rust;

namespace Oxide.Plugins
{
    [Info("Skins", "bazuka5801", "1.0.0")]
    class Skins : RustPlugin
    {
        #region CLASSES

        class SkinBox : MonoBehaviour
        {
            LootableCorpse corpse;
            ItemContainer container;

            bool insert = false;
            bool removed = false;
            bool canAccept = true;
            Item insertItem;
            int page = 0;
            ItemDefinition def;
            List<List<ulong>> skins;
            BasePlayer player;
            bool ui = false;
            int ammo = -1;
            List<Item> mods;
            private Item item;
            public void Init(BasePlayer player)
            {
                this.player = player;
            }

            void Awake()
            {
                corpse = GetComponent<LootableCorpse>();
                container = corpse.containers[0];
                
                container.onItemAddedRemoved += (item, b) =>
                {
                    if (b == false)
                    {
                        if (!removed)
                        {
                            removed = true;
                            return;
                        }
                        container.itemList.ToList().ForEach(p =>
                        {
                            removed = false;
                            p.RemoveFromContainer();
                            p.Remove();
                        });

                        if (ammo > 0)
                        {
                            ((BaseProjectile) item.GetHeldEntity()).primaryMagazine.contents = ammo;
                            ammo = -1;
                        }
                        if (mods != null)
                        {
                            foreach (var mod in mods)
                                mod.MoveToContainer(item.contents);
                            mods = null;
                        }
                        insert = false;
                        removed = false;
                        canAccept = true;
                        if (ui && player != null)
                        {
                            instance.DestroyUI(player);
                            ui = false;
                        }
                    }
                    else
                    {
                        if (!insert)
                        {
                            page = 0;
                            def = item.info;
                            item.RemoveFromContainer();
                            insert = true;
                            skins = instance.GetSkins(def);
                            if (skins.Count > 1)
                                if (!ui)
                                {
                                    instance.DrawUI(player);
                                    ui = true;
                                }
                                else if (ui)
                                {
                                    instance.DestroyUI(player);
                                    ui = false;
                                }
                            container.capacity = 19;

                            var heldEntity = item.GetHeldEntity() as BaseProjectile;
                            if (heldEntity?.primaryMagazine != null)
                            {
                                ammo = heldEntity.primaryMagazine.contents;
                                heldEntity.primaryMagazine.contents = 0;
                            }
                            item.amount = 1;

                            if (item.contents != null && item.contents.itemList.Count > 0)
                            {
                                mods = new List<Item>();
                                for (int i = item.contents.itemList.Count - 1; i >= 0; i--)
                                {
                                    var mod = item.contents.itemList[i];
                                    if (mod == null)continue;
                                    mod.RemoveFromContainer();
                                    mods.Add(mod);
                                }
                            }
                            for (int i = 0; i < skins[0].Count; i++)
                            {
                                var skin = skins[0][i];
                                insertItem = InventoryUtils.CloneItem(item,skin);
                                insertItem.MoveToContainer(container, i, false);
                            }
                            this.item = item;
                            insertItem = null;
                            canAccept = false;
                            removed = true;
                        }
                    }
                };
                container.canAcceptItem += item =>
                {
                    if (insertItem != item && instance.GetSkins(item.info)[0].Count <= 1)
                        return false;
                    return canAccept || insertItem == item;
                };
            }

            public void NextPage()
            {
                if (skins.Count - 1 > page)
                {
                    container.itemList.ToList().ForEach(p =>
                    {
                        removed = false;
                        p.RemoveFromContainer();
                        p.Remove();
                    });
                    foreach (var skin in skins[++page])
                    {
                        insertItem = InventoryUtils.CloneItem(item, skin);
                        insertItem.MoveToContainer(container, -1, false);
                    }
                }
            }
            public void LastPage()
            {
                if (page > 0)
                {
                    container.itemList.ToList().ForEach(p =>
                    {
                        removed = false;
                        p.RemoveFromContainer();
                        p.Remove();
                    });
                    foreach (var skin in skins[--page])
                    {
                        insertItem = InventoryUtils.CloneItem(item, skin);
                        insertItem.MoveToContainer(container, -1, false);
                    }
                }
            }

            void OnDestroy()
            {
                instance.boxes.Remove(this);
                if (ui)
                {
                    instance.DestroyUI(player);
                    ui = false;
                }
            }

            private void PlayerStoppedLooting(BasePlayer player)
            {
                if (container.itemList.Count > 0)
                    container.itemList[0].MoveToContainer(player.inventory.containerMain);
                corpse.Kill();
            }
        }

        #endregion

        #region CONFIGURATION
        string boxTitle;
        Dictionary<string, List<ulong>> WorkshopSkins;
        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Имя ящика", out boxTitle, "Перетащите предмет");
            Config["Дополнительные WorkShop скины"] =
                WorkshopSkins =
                    GetConfig("Дополнительные WorkShop скины", new Dictionary<string, object>() { { "rifle.ak", new List<object>() { 654502185 } } })
                        .ToDictionary(p => p.Key, p => ((List<object>) p.Value).Select(o=>ulong.Parse(o.ToString())).ToList());
            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue)
            => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion

        #region FIELDS

        static Skins instance;
        static RCore core = Interface.Oxide.GetLibrary<RCore>();

        private static readonly Dictionary<string, List<List<ulong>>> skinsCache = new Dictionary<string, List<List<ulong>>>();
        private readonly Dictionary<string,List<ulong>> skins = new Dictionary<string, List<ulong>>();
        private Dictionary<SkinBox,BasePlayer> boxes = new Dictionary<SkinBox, BasePlayer>();
        private List<ulong> PlayersActivated = new List<ulong>();

        #endregion

        #region COMMANDS
        
        [ChatCommand("skin")]
        void cmdChatSkin(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 1)
            {
                var option = args[0];
                if (option == "on" || !PlayersActivated.Contains(player.userID))
                {
                    PlayersActivated.Add(player.userID);
                    ItemSkinsCommand(player, true);
                    SendReply(player, Messages["skinsOn"]);
                } else
                if (option == "off" || PlayersActivated.Contains(player.userID))
                {
                    PlayersActivated.Remove(player.userID);
                    ItemSkinsCommand(player, false);
                    SendReply(player, Messages["skinsOff"]);
                }
                return;
            }
            if (!PlayersActivated.Contains(player.userID))
            {
                SendReply(player, Messages["skinsCurrentlyDisabled"]);
                return;
            }
            timer.Once(0.1f, () =>
            {
                StorageBox.Create(this, player, boxTitle, 18);
                var sb = StorageBox.AddComponent<SkinBox>(this, player);
                boxes.Add(sb, player);
                sb.Init(player);
                StorageBox.StartLooting(this, player);
            });
        }

        [ConsoleCommand("skins.left")]
        void cmdLeft(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var skinBox = FindSkinBoxByPlayer(player);
            if (skinBox == null) return;
            skinBox.LastPage();
        }
        [ConsoleCommand("skins.right")]
        void cmdRight(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var skinBox = FindSkinBoxByPlayer(player);
            if (skinBox == null) return;
            skinBox.NextPage();
        }

        SkinBox FindSkinBoxByPlayer(BasePlayer player)
        {
            return (from v in boxes where v.Value == player select v.Key).FirstOrDefault();
        }
        #endregion

        #region OXIDE HOOKS

        void Loaded()
        {
            instance = this;
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            LoadData();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            foreach (var def in ItemManager.itemList)
                if (def != null)
                    GetSkins(def);
            foreach (var player in BasePlayer.activePlayerList) OnPlayerInit(player);
        }

        void Unload()
        {
            SaveData();
            boxes.Keys.ToList().ForEach(p => p.GetComponent<BaseEntity>().Kill());
        }
        void OnPlayerConnected(Network.Message packet)
        {
            var player = packet.Player();
            if (player)
                OnPlayerInit(player);
        }
        void OnPlayerInit(BasePlayer player)
        {
            ItemSkinsCommand(player, PlayersActivated.Contains(player.userID));
        }

        // RANDOM ITEM SKIN

        private readonly List<int> randomizedTasks = new List<int>();

        void OnItemCraft(ItemCraftTask task)
        {
            if (task.skinID != 0) return;

            List<ulong> skins;
            if (!this.skins.TryGetValue(task.blueprint.targetItem.shortname,out skins))return;
            task.skinID = (int)skins.GetRandom();
            randomizedTasks.Add(task.taskUID);
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (!randomizedTasks.Contains(task.taskUID)) return;
            if (task.amount == 0)
            {
                randomizedTasks.Remove(task.taskUID);
                return;
            }
            List<ulong> skins;
            if (!this.skins.TryGetValue(task.blueprint.targetItem.shortname, out skins)) return;
            task.skinID = (int)skins.GetRandom();
            item.skin = skins.GetRandom();
            Puts($"{item.skin} {task.skinID}");
        }

        #endregion

        #region CORE

        void ItemSkinsCommand(BasePlayer player, bool value)
        {
            player.SendConsoleCommand($"itemskins {(value ? 1 : 0)}");
        }

        ulong GetRandomSkin(ItemDefinition def)
        {
            List<ulong> skins;
            return !this.skins.TryGetValue(def.shortname, out skins) ? 0 : skins.GetRandom();
        }


        private List<List<ulong>> GetSkins(ItemDefinition def)
        {
            List<List<ulong>> skinsPages;
            if (skinsCache.TryGetValue(def.shortname, out skinsPages)) return skinsPages;
            var skins = new List<ulong> { 0 };
            skins.AddRange(ItemSkinDirectory.ForItem(def).Select(skin => (ulong)skin.id));
            skins.AddRange(Rust.Workshop.Approved.All.Where(s=>ItemManager.itemList.Find(p=>p.name == s.Skinnable.ItemName)?.shortname== def.shortname).Select(skin => skin.WorkshopdId));
            List<ulong> workshopSkins;
            if (WorkshopSkins.TryGetValue(def.shortname,out workshopSkins))
                skins.AddRange(workshopSkins);
            this.skins[def.shortname] = skins.ToList();
            skinsPages = new List<List<ulong>>();
            for (int i = 0; i < PagesCount(skins); i++)
                skinsPages.Add(GetPage(skins,i,18));
            skinsCache.Add(def.shortname, skinsPages);
            return skinsPages;
        }

        int PagesCount(IList<ulong> list) => Mathf.CeilToInt((float)list.Count/18);

        List<ulong> GetPage(IList<ulong> list, int page, int pageSize)
        {
            return list.Skip((page) * pageSize).Take(pageSize).ToList();
        }


        #endregion

        #region UI

        void DrawUI(BasePlayer player)
        {
            core.DrawUI(player, "Skins", "menu");
        }
        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI(player, "Skins", "menu");
        }
        #endregion

        #region DATA

        private readonly DynamicConfigFile PlayersActivatedFile = Interface.Oxide.DataFileSystem.GetFile("Skins.PlayersActivated");

        void OnServerSave() => SaveData();

        void LoadData()
        {
            PlayersActivated = PlayersActivatedFile.ReadObject<List<ulong>>();
        }

        void SaveData()
        {
            PlayersActivatedFile.WriteObject(PlayersActivated);
        }

        #endregion

        #region MESSAGES

        Dictionary<string,string> Messages = new Dictionary<string, string>()
        {
            {"skinsCurrentlyDisabled", "У вас выключены скины!\nЧтобы включить используйте /skin on\nЧтобы выключить используйте /skin off" },
            { "skinsOff", "Скины отключены!\nЧтобы включить используйте /skin on\nЧтобы выключить используйте /skin off" },
            { "skinsOn", "Скины включены!\nЧтобы включить используйте /skin on\nЧтобы выключить используйте /skin off" },
        };

        #endregion
    }
}
