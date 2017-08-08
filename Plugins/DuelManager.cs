// Reference: Oxide.Core.RustyCore

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using RustyCore;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DuelManager", "bazuka5801", "2.3.1")]
    class DuelManager : RustPlugin
    {
        static DuelManager Instance;
        RCore core = Interface.Oxide.GetLibrary<RCore>();

        [PluginReference] Plugin Duels;

        [PluginReference] Plugin Trade;


        #region VARIABLES

        // DATA
        readonly DynamicConfigFile statsDB = Interface.Oxide.DataFileSystem.GetFile("DuelManager_statsDB");

        // VARS
        Dictionary<ulong, PlayerStats> stats;

        Dictionary<BasePlayer, int> playersRank = new Dictionary<BasePlayer, int>();

        void UpdatePlayerRank(BasePlayer player)
        {
            playersRank[player] = GetStats(player).Rank();
            playersRank = playersRank.OrderByDescending(p => p.Value).ToDictionary(p => p.Key, p => p.Value);
        }

        #endregion

        #region CLASSES

        class PlayerStats
        {
            public int Kills;
            public int Death;
            public int DuelCount => DuelWin + DuelLose;
            public int DuelWin;
            public int DuelLose;
            public List<int> Weapons = new List<int>();

            public int Rank()
            {
                return (int) ((DuelWin + DuelLose/2)*2*KD());
            }

            public float KD()
            {
                return (float) Kills/(Death == 0 ? 1 : Death);
            }

            public float DuelWinPercent()
            {
                return (float) DuelWin/(DuelLose == 0 ? 1 : DuelLose);
            }

            public int DuelsCount()
            {
                return DuelWin + DuelLose;
            }

            public string FavoriteWeapon()
            {
                if (Weapons == null || Weapons.Count <= 0) return "---";
                var weapons = new Dictionary<int, int>();
                foreach (var i in Weapons)
                {
                    if (!weapons.ContainsKey(i))
                        weapons[i] = 0;
                    weapons[i]++;
                }
                var index = weapons.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
                return Instance.GetWeaponName(index);
            }
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            Instance = this;
            LoadStats();
            foreach (var p in BasePlayer.activePlayerList)
                UpdatePlayerRank(p);
            CalcTopRank();
        }

        void Unload()
        {
            SaveStats();
        }

        void OnPlayerInit(BasePlayer player)
        {
            UpdatePlayerRank(player);
            CalcTopRank();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            playersRank.Remove(player);
            CalcTopRank();
        }
        #endregion

        #region PLAYER DUEL HOOKS

        [HookMethod("CommonDuelEnded")]
        void CommonDuelEnded(BasePlayer winner, BasePlayer looser, bool draw)
        {
            if(draw) return;

            PlayerStats winnerStats = GetStats(winner);
            PlayerStats looserStats = GetStats(looser);

            winnerStats.DuelWin++;
            looserStats.DuelLose++;
            CalcTopRank();
            UpdatePlayerRank(winner);
            UpdatePlayerRank(looser);
        }

        [HookMethod("PlayerDuelDeath")]
        void PlayerDuelDeath(BasePlayer player)
        {
            GetStats(player).Death++;
            CalcTopRank();
            UpdatePlayerRank(player);
        }

        [HookMethod("PlayerDuelKill")]
        void PlayerDuelKill(BasePlayer player)
        {
            GetStats(player).Kills++;
            CalcTopRank();
            UpdatePlayerRank(player);
        }

        [HookMethod("PlayerDuelChooseWeapon")]
        void PlayerDuelChooseWeapon(BasePlayer player, int weapon)
        {
            GetStats(player).Weapons.Add(weapon);
            CalcTopRank();
            UpdatePlayerRank(player);
        }

        #endregion

        #region FUNCTIONS

        void LoadStats() => stats = statsDB.ReadObject<Dictionary<ulong, PlayerStats>>();
        void SaveStats() => statsDB.WriteObject(stats);
        PlayerStats GetStats(BasePlayer player) => GetStats(player.userID);
        PlayerStats GetStats(ulong uid)
        {
            PlayerStats playerStats;
            if(!stats.TryGetValue(uid, out playerStats))
                playerStats = stats[uid] = new PlayerStats();
            return playerStats;
        }

        void GetAvatar(ulong uid, Action<string> callback)
        {

            string url = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=443947CEA9A0CC4F4868BFD1AA33E972&" +
                "steamids=" + uid;
            webrequest.EnqueueGet(url,
                (i, json) => callback?.Invoke((string)JObject.Parse(json)["response"]["players"][0]["avatarfull"]),
                this);
        }

        string GetWeaponName(int index)
        {
            return Duels == null ? "---" : ((string[])Duels.Call("GetNamesOfWeapons"))[index];
        }

        #endregion


        #region UI

        void DuelManagerUI_Draw(BasePlayer player)
        {
            DuelManagerUI_ChoosePlayer(player, player);
        }

        void DuelManagerUI_ChoosePlayer(BasePlayer player, BasePlayer selected)
        {
            GetAvatar(selected.userID, avatar =>
            {
                var playerStats = GetStats(selected);
                core.DrawUI(player, "DuelManager","menu", new object[]
                {
                    avatar,
                    playerStats.Rank(),
                    playerStats.KD().ToString("F2"),
                    playerStats.DuelWinPercent().ToString("F2"),
                    playerStats.Kills,
                    playerStats.Death,
                    playerStats.DuelCount,
                    playerStats.FavoriteWeapon()
                });
                DuelManagerUI_DrawPlayers(player, selected.userID);
            });
        }

        void DuelManagerUI_DrawPlayers(BasePlayer player, ulong selected)
        {
            CuiHelper.DestroyUi(player, "duel_manager_playerlist_container");
            float gap = 0.01f;
            float width = 0.12f;
            float height = 0.03f;
            float startxBox = 0.03f;
            float startyBox = 1f - height - 0.05f;

            float xmin = startxBox;
            float ymin = startyBox;

            var container = new CuiElementContainer();

            var mainPanel = container.Add(new CuiPanel() {Image = {Color = "0 0 0 0"}}, "duel_manager_playerlist","duel_manager_playerlist_container");

            int i = 0;
            foreach(var pPair in playersRank)
            {
                var p = pPair.Key;
                var min = $"{xmin} {ymin}";
                var max = $"{xmin + width} {ymin + height}";


                if(p.userID == selected)
                    container.Add(new CuiElement()
                    {
                        Name = "case_"+i,
                        Parent = mainPanel,
                        Components =
                        {
                            new CuiImageComponent() {Color = "0 0 0 0"},
                            new CuiRectTransformComponent() {AnchorMin = min, AnchorMax = max},
                            new CuiOutlineComponent() {Color = "0.91 0.42 0.17 1", Distance = "2 -2"}
                        }
                    });
                var panel = container.Add(new CuiPanel()
                {
                    Image = { Color = SetColor(GetRankColor(pPair.Value)) },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                }, mainPanel);

                container.Add(
                    new CuiButton()
                    {
                        Button = { Command = "duelmanager.chooseplayer " + p.userID, Color = "0 0 0 0" },
                        Text = { Text = p.displayName, Align = TextAnchor.MiddleCenter }
                    }, panel);

                xmin += width + gap;
                if(xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + gap;
                }
                i++;
            }

            CuiHelper.AddUi(player, container);
        }
        

        void DuelManagerUI_Destroy(BasePlayer player)
        {
            core.DestroyUI(player, "DuelManager","menu");
        }

        readonly Dictionary<ulong, string> selectedPlayers = new Dictionary<ulong, string>();
        [ConsoleCommand("duelmanager.chooseplayer")]
        void cmdDuelManagerChoosePlayer(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var p = (BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0])) ?? player;
            selectedPlayers[player.userID] = p.displayName;
            DuelManagerUI_ChoosePlayer(player, p);
        }

        [ConsoleCommand("duelmanager.ask")]
        void cmdDuelManagerAsk(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if(!selectedPlayers.ContainsKey(player.userID)) return;
            if(player.userID.ToString() == selectedPlayers[player.userID]) return;
            Duels.Call("duelChat", player, "", new string[] {"ask", selectedPlayers[player.userID]});
            DuelManagerUI_Destroy(player);
        }
        [ConsoleCommand("duelmanager.ask.team")]
        void cmdDuelManagerAskTeam(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if(!selectedPlayers.ContainsKey(player.userID)) return;
            if(player.userID.ToString() == selectedPlayers[player.userID]) return;
            Duels.Call("duelChat", player, "", new string[] { "clan", selectedPlayers[player.userID] });
            DuelManagerUI_Destroy(player);
        }
        [ConsoleCommand("duelmanager.trade")]
        void cmdDuelManagerTrade(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!selectedPlayers.ContainsKey(player.userID)) return;
            if (player.userID.ToString() == selectedPlayers[player.userID]) return;
            Trade?.Call("cmdTrade", player, "", new string[] {selectedPlayers[player.userID]});
            DuelManagerUI_Destroy(player);
        }

        [ConsoleCommand("duelmanager.close")]
        void cmdDuelManagerClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DuelManagerUI_Destroy(player);
        }

        private Color left = ColorEx.Parse("0.23 0.23 0.23 1");
        private Color right = ColorEx.Parse("0.92 0.70 0 1");
        Color GetRankColor(int rank)
        {
            return Color.Lerp(left, right, (float)rank/GetTopRank());
        }


        private int topRank = 1;

        void CalcTopRank()
        {
            if (playersRank.Count > 0)
            topRank = playersRank.Values.Max();
        }
        int GetTopRank()
        {
            return topRank;
        }

        string SetColor(Color color) => $"{color.r} {color.g} {color.b} {color.a}";

        #endregion

        #region CHAT COMMANDS

        [ChatCommand("duels")]
        void cmdChatDuels(BasePlayer player, string command, string[] args)
        {
            DuelManagerUI_Draw(player);
        }

        #endregion


    }
}
