// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using RustyCore;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RandomCase", "bazuka5801", "1.0.0")]
    public class RandomCase : RustPlugin
    {
        static RandomCase instance;

        public class CaseDefinition
        {
            public string Type;
            public string Name;
            public int CoolDown;
            public List<CaseItem> Items;

            public CaseItem Open() => Items.GetRandom();
        }

        public class CaseItem
        {
            public string Name;
            public int Min;
            public int Max;

            public int GetRandom() => UnityEngine.Random.Range(Min, Max + 1);
        }

        public class Case
        {
            public string Type;
            public int CoolDown;
            public int Amount;

            public CaseDefinition info()
            {
                if (!cases.ContainsKey(Type))
                {
                    Interface.Oxide.LogWarning($"[{nameof(RandomCase)}] TYPE '{Type}' not contains in the Dictionary<string, CaseDefinition>!");
                    return null;
                }
                return cases[Type];
            }
        }

        public class CasePlayer
        {
            public List<Case> CasesQueue = new List<Case>();
            public List<string> Inventory = new List<string>();
           
            public void OnTimer(ulong steamid, int delay)
            {
            
                List<Case> remove = new List<Case>();
                for (var i = CasesQueue.Count - 1; i >= 0; i--)
                {
                    var c = CasesQueue[i];
                    c.CoolDown -= delay;
                    if (c.Amount <= 0)
                    {
                        remove.Add(c);
                        continue;
                    }
                    if (c.CoolDown <= 0)
                    {
                        if (instance.GiveCase(steamid, c.Type, c))
                            remove.Add(c);
                    }
                }
                remove.ForEach(c => CasesQueue.Remove(c));
            }
        }

        private RCore core = Interface.Oxide.GetLibrary<RCore>();
        public static Dictionary<string, CaseDefinition> cases = new Dictionary<string, CaseDefinition>();
        public Dictionary<ulong, CasePlayer> players = new Dictionary<ulong, CasePlayer>();
        const int TIMEOUT = 60;

        void OnServerInitialized()
        {
            instance = this;
            LoadData();
            timer.Every(TIMEOUT, TimerHandle);
        }

        void Unload()
        {
            SaveData();
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            CasePlayer casePlayer;
            if (players.TryGetValue(player.userID,out casePlayer) && casePlayer.Inventory.Count > 0)
                SendReply(player, "У вас есть не открытые кейсы!\nЧтобы их открыть наберите команду <color=#fee3b4>/case</color>");
        }

        #region COMMANDS

        /// <summary>
        /// randomcasegive 2281337 resources 27
        /// 2281337 - steamid
        /// resources - caseType
        /// 27 - amount
        /// </summary>
        /// <param name="arg"></param>
        [ConsoleCommand("randomcasegive")]
        void cmdRandomCaseGive(ConsoleSystem.Arg arg)
        {
            BasePlayer player;
            if (arg.Connection != null)
            {
                player = arg.Connection.player as BasePlayer;
                if (player != null && player.net.connection.authLevel != 2) return;
            }
            var uid = ulong.Parse(arg.Args[0]);
            CasePlayer casePlayer;
            if (!players.TryGetValue(uid, out casePlayer))
            {
                casePlayer = new CasePlayer();
                players.Add(uid,casePlayer);
            }

            for (int i = 1; i < arg.Args.Length; i += 2)
            {
                var type = arg.Args[i];
                var amount = int.Parse(arg.Args[i+1]);
                Case cCase = casePlayer.CasesQueue.FirstOrDefault(c => c.Type == type);
                if (cCase == null)
                {
                    cCase = new Case() {Amount = amount, Type = type, CoolDown = 0};
                    casePlayer.CasesQueue.Add(cCase);
                }
                else cCase.Amount += amount;

                GiveCase(uid, type);
            }
        }


        [ConsoleCommand("randomcase.dropuser")]
        void cmdDropUser(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            ulong userId = arg.GetUInt64(0);

            CasePlayer casePlayer;
            if (!players.TryGetValue(userId, out casePlayer))
            {
                Puts("У игрока нет кейсов!");
                return;
            }

            var commandMsg = $"randomcasegive {userId}";

            foreach (var ccase in casePlayer.Inventory)
                commandMsg += $" \"{ccase}\" 1";
            foreach (var ccase in casePlayer.CasesQueue)
                commandMsg += $" \"{ccase.Type}\" {ccase.Amount - 1}";

            players.Remove(userId);
            Puts($"Очищены кейсы {userId}\nДля переноса используйте следующую команду:\n{commandMsg}");
        }

        [ChatCommand("case")]
        void cmdChatCase(BasePlayer player)
        {
            CasePlayer casePlayer;
            if (!players.TryGetValue(player.userID, out casePlayer))
            {
                SendReply(player, "У вас нет кейсов");
                base.SendReply(player, "    <size=16><color=#fee3b4>Вы Автоматически будете получать кейсы</color></size>\n╔══════════════════════════════╗\n <size=15>                   Ресурсы Взрывчатка Оружие</size>\n<size=16><color=#fee3b4>            после покупки</color></size> <size=16><color=#fee3b4>Привелегии GOD</color></size>\n        в нашем магазине <size=16><color=#fee3b4>rustylife.gamestores.ru</color></size>\n╚══════════════════════════════╝");
                return;
            }
            foreach (var ccase in casePlayer.CasesQueue)
            {
                SendReply(player,
                    "Кейсов ' " + ccase.Type + " ' осталось еще: " + ccase.Amount +
                    " .\n До следующего: " + GetTime(ccase.CoolDown));
            }
            DrawCases(player);
        }

        void SendReply(BasePlayer player, string msg)
        {
            base.SendReply(player,$"<size=16><color=#fee3b4>{msg}</color></size>");
        }

        #endregion

        #region CORE

        void TimerHandle()
        {
            foreach (var rCase in players)
                rCase.Value.OnTimer(rCase.Key,TIMEOUT);
            SaveData();
        }

        bool GiveCase(ulong userID, string ccase, Case ccase1 = null)
        {
            CasePlayer casePlayer;
            if (players.TryGetValue(userID, out casePlayer))
            {
                var caseQueue = ccase1 ?? casePlayer.CasesQueue.Find(c => c.Type == ccase);
                if (caseQueue == null) return false;
                caseQueue.Amount--;
                if (caseQueue.info() == null)
                {
                    casePlayer.CasesQueue.Remove(caseQueue);
                    return false;
                }
                caseQueue.CoolDown = caseQueue.info().CoolDown;
                casePlayer.Inventory.Add(ccase);

                var player = BasePlayer.FindByID(userID);
                Puts($"{(player == null ? userID.ToString() : player.displayName)} получил кейс с {ccase}");
                if (player != null)
                    player.ChatMessage($"<size=16><color=#fee3b4>Вы получили кейс c {ccase}</color></size>");

                if (caseQueue.Amount == 0)
                {
                    instance.Puts($"{(player == null ? userID.ToString() : player.displayName)} перестал получать кейс с {ccase}");
                    return true;
                }
            }
            return false;
        }

        bool OpenCase(BasePlayer player, string ccase)
        {
            CasePlayer casePlayer;
            if (players.TryGetValue(player.userID, out casePlayer)
                && casePlayer.Inventory.Contains(ccase))
            {
                if (!CanTake(player))
                {
                    SendReply(player,"У вас переполнен инвентарь!");
                    return false;
                }
                casePlayer.Inventory.Remove(ccase);
                var item = cases[ccase].Open();
                var amount = item.GetRandom();
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "inv.giveplayer", player.userID, item.Name, amount);
                SaveData();
                return true;
            }
            return false;
        }

        bool CanTake(BasePlayer player)=> !player.inventory.containerMain.IsFull() || !player.inventory.containerBelt.IsFull();

        string GetTime(int seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);

            //here backslash is must to tell that colon is
            //not the part of format, it just a character that we want in output
            return $"{time.TotalHours:####} ч. {time.Minutes:####} мин.";
        }

        #endregion

        #region UI
        
        void DrawCases(BasePlayer player)
        {
            CasePlayer casePlayer;
            if (!players.TryGetValue(player.userID, out casePlayer))
            {
                SendReply(player, "У вас нет кейсов");
                SendReply(player, "    <size=16><color=#fee3b4>Вы Автоматически будете получать кейсы</color></size>\n╔══════════════════════════════╗\n <size=15>                   Ресурсы Взрывчатка Оружие</size>\n<size=16><color=#fee3b4>            после покупки</color></size> <size=16><color=#fee3b4>Привелегии GOD</color></size>\n        в нашем магазине <size=16><color=#fee3b4>rustylife.gamestores.ru</color></size>\n╚══════════════════════════════╝");
                return;
            }
            core.DrawUI(player, "RandomCase", "menu");
            CuiHelper.DestroyUi(player, "casescontainer");

            var container = new CuiElementContainer();

            float gap = 0f;
            float width = 1f;
            float height = 0.33f;
            float startxBox = 0.0f;
            float startyBox = 1f - height;

            float xmin = startxBox;
            float ymin = startyBox;

            int i = 0;

            var mainPanel = container.Add(new CuiPanel() { Image = { Color = "0 0 0 0" } }, "casesc", "casescontainer" );

            Dictionary<string,int> casesCollapsed = new Dictionary<string, int>();
            foreach (var ccase in casePlayer.Inventory)
            {
                if (!casesCollapsed.ContainsKey(ccase))
                    casesCollapsed[ccase] = 0;
                casesCollapsed[ccase]++;
            }

            foreach (var ccase in casesCollapsed)
            {
                container.Add(new CuiButton()
                {
                    Button = { Command = $"randomcase.open {ccase.Key}",Color = "0.1 0.1 0.1 0.7" },
                    RectTransform = {
                        AnchorMin = xmin + " " + ymin,
                        AnchorMax = (xmin + width) + " " + (ymin + height),
                        OffsetMax = "-5 -7",
                        OffsetMin = "5 0"
                    }, Text = { Text = $"{ccase.Value} x {cases[ccase.Key].Name}", Align = TextAnchor.MiddleCenter,Color ="1 1 1 1", FontSize = 18}
                }, mainPanel);
                xmin += width + gap;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + gap;
                }
                i++;
            }
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("randomcase.open")]
        void cmdOpenCase(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            var player = arg.Player();
            var ccase = arg.Args[0];
            if (OpenCase(player, ccase))
            {
                DrawCases(player);
            }
        }


        [ConsoleCommand("casesmenuclose")]
        void cmdCloseMenu(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            var player = arg.Player();
            core.DestroyUI(player, "RandomCase", "menu");
        }

        #endregion

        #region DATA

        DynamicConfigFile cases_File = Interface.Oxide.DataFileSystem.GetFile("RandomCase_Cases");
        DynamicConfigFile players_File = Interface.Oxide.DataFileSystem.GetFile("RandomCase_Players");

        void LoadData()
        {
            try
            {
                cases = cases_File.ReadObject<Dictionary<string, CaseDefinition>>();
            }
            catch
            {
                cases = new Dictionary<string, CaseDefinition>();
            }
            try
            {
                players = players_File.ReadObject<Dictionary<ulong, CasePlayer>>();
            }
            catch
            {
                players = new Dictionary<ulong, CasePlayer>();
            }
        }

        void SaveData()
        {
            players_File.WriteObject(players);
        }



        #endregion
    }
}
