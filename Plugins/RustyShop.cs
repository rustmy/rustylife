// Reference: Oxide.Core.RustyCore

using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RustyShop", "bazuka5801", "1.0.0")]
    class RustyShop : RustPlugin
    {
        #region CLASSES

        class ShopItem
        {
            public string DisplayName;
            public string URL;
            public int ID;
            public int Amount;
            public ulong Skin;
            public int Cost;
        }

        class ShopPlayer
        {
            public int Points;
            public long TotalOnlineSeconds;
        }

        #endregion

        #region CONFIGURATION

        int hourPoints;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Кол-во поинтов за час", out hourPoints, 15);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();
        Dictionary<ulong, ShopPlayer> players;
        List<ShopItem> items;
        CuiElementContainer itemsUI;
        List<ulong> subs = new List<ulong>();

        #endregion

        #region COMMANDS

        [ChatCommand("s")]
        void cmdChatS(BasePlayer player)
        {
            if (subs.Contains(player.userID))
            {
                subs.Remove(player.userID);
                DestroyUI(player);
            }
            else
            {
                if (InDuel(player)) return;
                subs.Add(player.userID);
                DrawUI(player);
            }
        }

        [ConsoleCommand("rustyshop.buy")]
        void cmdRustyShopBuy(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            var player = arg.Player();
            var shopPlayer = players[player.userID];
            var itemIndex = int.Parse(arg.Args[0]);
            var buyItem = items[itemIndex];

            if (shopPlayer.Points < buyItem.Cost)
            {
                SendReply(player, "Недостаточно средств!");
                return;
            }
            if (InDuel(player)) return;

            shopPlayer.Points -= buyItem.Cost;

            var item = ItemManager.CreateByItemID(buyItem.ID, buyItem.Amount, buyItem.Skin);
            var container = player.inventory.containerMain;
            if (!item.MoveToContainer(container))
                item.Drop(container.dropPosition, container.dropVelocity);

            DestroyUI(player);
            DrawUI(player);
        }

        #endregion

        #region OXIDE HOOKS

        void Loaded()
        {
            LoadData();
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            CommunityEntity.ServerInstance.StartCoroutine(LoadImages());
            foreach (var player in BasePlayer.activePlayerList)
                AddShopPlayer(player.userID);

            timer.Every(1f, () => BasePlayer.activePlayerList.ForEach(p =>
            {
                var time = players[p.userID].TotalOnlineSeconds;
                if (++time%3600 == 0)
                    players[p.userID].Points += hourPoints;
                players[p.userID].TotalOnlineSeconds = time;
            }));
        }

        void Unload()
        {
            SaveData();
        }

        void OnPlayerConnected(Network.Message packet)
        {
            var userId = packet.connection.userid;
            AddShopPlayer(userId);
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            subs.Remove(player.userID);
        }

        #endregion

        #region CORE

        void AddShopPlayer(ulong userId)
        {
            if (!players.ContainsKey(userId))
                players[userId] = new ShopPlayer() { Points = 15, TotalOnlineSeconds = 0 };
        }

        #endregion

        #region UI

        void CreateItemsUI()
        {
            float gap = 0.014f;
            float width = 0.15f;
            float height = 0.17f;
            float startxBox = gap;
            float startyBox = 1f - height - 0.05f;

            float xmin = startxBox;
            float ymin = startyBox;
            itemsUI = new CuiElementContainer();
            int i = 0;
            var mainParent = itemsUI.Add(new CuiPanel() {Image = {Color = "0 0 0 0"}}, "rustyshop.items",
                "rustyshop.items.customui");

            foreach (var item in items)
            {
                var min = $"{xmin} {ymin}";
                var max = $"{xmin + width} {ymin + height}";
                var panelname = itemsUI.Add(new CuiPanel()
                {
                    Image = {Color = "0 0 0 0"},
                    RectTransform = {AnchorMin = min, AnchorMax = max}
                }, mainParent);


                itemsUI.Add(new CuiButton()
                {
                    Button = {Command = $"rustyshop.buy {i}", Color = "0.3 0.5 0.4 1"},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.25"},
                    Text =
                    {
                        Text =
                            $"<color=orange><color=#00ccff>{item.Amount}</color> за <color=#ffcc00>{item.Cost}</color> р.</color>",
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter
                    }
                }, panelname);

                itemsUI.Add(new CuiElement()
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panelname,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = ImageStorage.FetchPng(item.DisplayName),
                            Sprite = "assets/content/textures/generic/fulltransparent.tga"
                        },
                        new CuiRectTransformComponent() {AnchorMin = "0 0.25", AnchorMax = "1 1"}
                    }
                });

                xmin += width + gap;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + gap;
                }
                i++;
            }
        }

        void DrawUI(BasePlayer player)
        {
            var shopPlayer = players[player.userID];
                foreach (var we in itemsUI)
                    CuiHelper.DestroyUi(player, we.Name);
                core.DrawUIWithEx(player, "RustyShop", "menu", itemsUI, shopPlayer.Points,
                    core.TimeToString(shopPlayer.TotalOnlineSeconds));
        }

        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI(player, "RustyShop", "menu");
        }
        IEnumerator LoadImages()
        {
            foreach (var item in items)
                yield return CommunityEntity.ServerInstance.StartCoroutine(ImageStorage.Store(item.DisplayName, item.URL));
            CreateItemsUI();
        }

        #endregion

        #region EXTERNAL CALLS

        [PluginReference] Plugin Duels;

        bool InDuel(BasePlayer player) => Duels?.Call<bool>("inDuel", player) ?? false;

        #endregion

        #region DATA

        private DynamicConfigFile itemsFile = Interface.Oxide.DataFileSystem.GetFile("RustyShop/Items");
        private DynamicConfigFile playersFile = Interface.Oxide.DataFileSystem.GetFile("RustyShop/Players");

        void OnServerSave()
        {
            SaveData();
        }

        void LoadData()
        {
            items = itemsFile.ReadObject<List<ShopItem>>();
            players = playersFile.ReadObject<Dictionary<ulong, ShopPlayer>>();
        }

        void SaveData()
        {
            Puts("SAVE PLAYERS!");
            playersFile.WriteObject(players);
        }

        #endregion
    }
}
