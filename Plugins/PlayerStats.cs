// Reference: Oxide.Core.RustyCore
using UnityEngine;
using System;
using System.IO;
using System.Globalization;
using System.Linq;
using Oxide.Game.Rust.Cui;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using RustyCore;
using RustyCore.Utils;

namespace Oxide.Plugins
{
    [Info("Player Stats", "vaalberith", "1.0.0")]
    class PlayerStats : RustPlugin
    {
        //коэффициенты

        float kills = 2;
        float deaths = 1;
        float resGather = 0.005f;
        float resQuarry = 0.001f;
        float level = 20;
        int topCount = 20;
        int timeInHours = 24 * 7;

        private RCore core = Interface.Oxide.GetLibrary<RCore>();
        // общая структура данных
        readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("PlayerStats");
        readonly DynamicConfigFile topFile = Interface.Oxide.DataFileSystem.GetFile("PlayerTopList");
        readonly DynamicConfigFile timingFile = Interface.Oxide.DataFileSystem.GetFile("PlayerTopTiming");
        int time;

        Dictionary<ulong, PlayerData> data = new Dictionary<ulong, PlayerData>(); //человек-данные

        class PlayerData
        {
            public string name = "Неизвестно";
            public Gather gather; //кирка/топор
            public Quarry quarry; //горнодобывающий
            public Pvp pvp; //убийства/смерти
            public int minutes = 0;
            public PlayerData() { }
        }

        //
        class Gather
        {
            public int wood = 0;
            public int stone = 0;
            public int metalOre = 0;
            public int sulfurOre = 0;
            public int hqmetalOre = 0;
            public Gather() { }
        }

        class Quarry
        {
            public int stone = 0;
            public int metalOre = 0;
            public int metal = 0;
            public int sulfurOre = 0;
            public int hqmetalOre = 0;
            public int hqmetal = 0;
            public Quarry() { }
        }

        class Pvp
        {
            public int duelWin = 0;
            public int kills = 0;
            public int deaths = 0;
            public Pvp() { }
        }

        void CreatePlayerData(ulong id)
        {
            if(data.ContainsKey(id)) return;
            PlayerData i = new PlayerData();
            i.gather = new Gather();
            i.quarry = new Quarry();
            i.pvp = new Pvp();
            BasePlayer player = BasePlayer.FindByID(id) ?? BasePlayer.FindSleeping(id);
            if(player != null) i.name = player.displayName;
            data.Add(id, i);
        }

        // Хуки для заполнения данных_________________________________________________________________________

        //топор/кирка

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if(player == null) return;
            if(!data.ContainsKey(player.userID))
            {
                CreatePlayerData(player.userID);
            }
            switch(item.info.shortname)
            {
                case "wood":
                    data[player.userID].gather.wood += item.amount;
                    break;
                case "stones":
                    data[player.userID].gather.stone += item.amount;
                    break;
                case "metal.ore":
                    data[player.userID].gather.metalOre += item.amount;
                    break;
                case "sulfur.ore":
                    data[player.userID].gather.sulfurOre += item.amount;
                    break;
                case "hq.metal.ore":
                    data[player.userID].gather.hqmetalOre += item.amount;
                    break;
            }
        }

        //горнодобывающий

        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            BasePlayer player = BasePlayer.FindByID(quarry.OwnerID) ?? BasePlayer.FindSleeping(quarry.OwnerID);
            if(player == null) return;
            if(!data.ContainsKey(player.userID))
            {
                CreatePlayerData(player.userID);
            }
            switch(item.info.shortname)
            {
                case "stones":
                    data[player.userID].quarry.stone += item.amount;
                    break;
                case "metal.ore":
                    data[player.userID].quarry.metalOre += item.amount;
                    break;
                case "metal.fragments":
                    data[player.userID].quarry.metal += item.amount;
                    break;
                case "sulfur.ore":
                    data[player.userID].quarry.sulfurOre += item.amount;
                    break;
                case "hq.metal.ore":
                    data[player.userID].quarry.hqmetalOre += item.amount;
                    break;
                case "metal.refined":
                    data[player.userID].quarry.hqmetal += item.amount;
                    break;
            }
        }

        // pvp

        void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if(victim == null) return;
            BasePlayer vict = victim.ToPlayer();
            if(vict == null) return;
            //killer
            if (info == null) return;
            BasePlayer killer = info.InitiatorPlayer;
            
            if (InEvent(vict) || InDuel(vict)) return;
            if (killer != null)
            {
                if (InDuel(killer))
                    return;
            }
            //жертва
            if (!data.ContainsKey(vict.userID))
            {
                CreatePlayerData(vict.userID);
            }
            if(!InDuel(vict)) data[vict.userID].pvp.deaths += 1;

            if (killer != null && killer != vict)
            {
                if(InEvent(killer) || InDuel(killer)) return;
                if(!data.ContainsKey(killer.userID))
                {
                    CreatePlayerData(killer.userID);
                }
                data[killer.userID].pvp.kills++;
            }
        }

        // [хуки]______________________________________________________________________________________________


        void SaveData()
        {
            dataFile.WriteObject(data);
        }
        void OnPluginLoaded(Plugin name)
        {
            if (name.ToString() == "ExtPlugin" && name.Author == "Sanlerus, Moscow.OVH")
            {
                rust.RunServerCommand("reload ZLevelsRemastered");
                rust.RunServerCommand("reload GatherAdvanced");
                Unsubscribe("OnDispenserGather");
                Subscribe("OnDispenserGather");
            }
        }

        void OnServerSave()
        {
            SaveData();
        }
        

        void OnServerInitialized()
        {
            CommunityEntity.ServerInstance.StartCoroutine(LoadImages());
        }

        IEnumerator LoadImages()
        {
            yield return ImageStorage.Store("stat", "http://i.imgur.com/iZUSVKg.png");
            yield return ImageStorage.Store("stat2", "http://i.imgur.com/Cyj0NZS.png");

        }


        void Loaded()
        {
            data = dataFile.ReadObject<Dictionary<ulong, PlayerData>>();
            time = timingFile.ReadObject<int>();
            timer.Every(3600, timingHandle);
            timer.Every(60, playtimeHandle);
        }

        void timingHandle()
        {
            time = time - 1;
            if(time <= 0)
            {
                PrintWarning("Отчет ТОП сохранен!");
                writeTop();
                time = timeInHours;
            }
            timingFile.WriteObject(time);
        }

        void playtimeHandle()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                if(!data.ContainsKey(player.userID))
                {
                    CreatePlayerData(player.userID);
                }
                data[player.userID].minutes++;
            }
        }

        void Unload()
        {
            SaveData();
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "StatsParent");
                CuiHelper.DestroyUi(player, "TopParent");
            }
        }

        [ChatCommand("stat")]
        void showGui(BasePlayer player, string cmd, string[] args)
        {
            if(!data.ContainsKey(player.userID))
            {
                CreatePlayerData(player.userID);
            }
            drawWindow(player);
        }

        [ConsoleCommand("stat")]
        void drawstatConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if(player != null)
            {
                if(!data.ContainsKey(player.userID))
                {
                    CreatePlayerData(player.userID);
                }
                drawWindow(player);
            }
        }

        [ConsoleCommand("drawTop")]
        void drawtopConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if(player != null) drawTopMenu(player);
        }

        //функции_____________________________________________________________________
        [PluginReference]
        Plugin EventManager;

        [PluginReference]
        Plugin Duels;

        bool InEvent(BasePlayer player)
        {
            try
            {
                bool result = (bool)EventManager?.Call("isPlaying", new object[] { player });
                return result;
            }
            catch
            {
                return false;
            }
        }

        bool InDuel(BasePlayer player)
        {
            try
            {
                bool result = (bool)Duels?.Call("inDuel", new object[] { player });
                return result;
            }
            catch
            {
                return false;
            }
        }

        Dictionary<ulong, string> startGui = new Dictionary<ulong, string>();

        void drawWindow(BasePlayer player)
        {
            IOrderedEnumerable<KeyValuePair<ulong, PlayerData>> items = from pair in data orderby pair.Value.pvp.kills descending select pair;
            int i = 1;
            string topPvp = "   Убийства:\n\n";
            foreach(KeyValuePair<ulong, PlayerData> pair in items)
            {
                string name = pair.Value.name;
                if(name == null) name = "Неизвестный";
                topPvp = topPvp + i.ToString() + ") " + name + " - " + pair.Value.pvp.kills.ToString() + "\n";
                i++;
                if(i > 5) break;
            }

            items = from pair in data orderby pair.Value.pvp.deaths descending select pair;
            i = 1;
            topPvp = topPvp + "\n   Смерти:\n\n";
            foreach(KeyValuePair<ulong, PlayerData> pair in items)
            {
                string name = pair.Value.name;
                if(name == null) name = "Неизвестный";
                topPvp = topPvp + i.ToString() + ") " + name + " - " + pair.Value.pvp.deaths.ToString() + "\n";
                i++;
                if(i > 5) break;
            }

            items = from pair in data orderby pair.Value.pvp.duelWin descending select pair;
            i = 1;
            /*topPvp = topPvp + "\n   Победы в дуэлях:\n\n";
            foreach(KeyValuePair<ulong, PlayerData> pair in items)
            {
                string name = pair.Value.name;
                if(name == null) name = "Неизвестный";
                topPvp = topPvp + i.ToString() + ") " + name + " - " + pair.Value.pvp.duelWin.ToString() + "\n";
                i++;
                if(i > 5) break;
            }*/

            items = from pair in data orderby (pair.Value.gather.wood + pair.Value.gather.stone + pair.Value.gather.metalOre + pair.Value.gather.sulfurOre + pair.Value.gather.hqmetalOre) descending select pair;
            i = 1;
            string topPve = "* Добыча\n\n   Руками:\n\n";
            foreach(KeyValuePair<ulong, PlayerData> pair in items)
            {
                string name = pair.Value.name;
                if(name == null) name = "Неизвестный";
                topPve = topPve + i.ToString() + ") " + name + " - " + (pair.Value.gather.wood + pair.Value.gather.stone + pair.Value.gather.metalOre + pair.Value.gather.sulfurOre +
                pair.Value.gather.hqmetalOre).ToString() + "\n";
                i++;
                if(i > 5) break;
            }

            items = from pair in data
                    orderby (pair.Value.quarry.stone + pair.Value.quarry.metal + pair.Value.quarry.metalOre + pair.Value.quarry.sulfurOre +
pair.Value.quarry.hqmetal + pair.Value.quarry.hqmetalOre) descending
                    select pair;
            i = 1;
            topPve = topPve + "\n   Карьер:\n\n";
            foreach(KeyValuePair<ulong, PlayerData> pair in items)
            {
                string name = pair.Value.name;
                if(name == null) name = "Неизвестный";
                topPve = topPve + i.ToString() + ") " + name + " - " + (pair.Value.quarry.stone + pair.Value.quarry.metal + pair.Value.quarry.metalOre + pair.Value.quarry.sulfurOre +
                pair.Value.quarry.hqmetal + pair.Value.quarry.hqmetalOre).ToString() + "\n";
                i++;
                if(i > 5) break;
            }

            items = from pair in data orderby pair.Value.minutes descending select pair;
            i = 1;
            string topTime = "\n\n * Время на сервере:\n\n";
            foreach(KeyValuePair<ulong, PlayerData> pair in items)
            {
                string name = pair.Value.name;
                if(name == null) name = "Неизвестный";
                topTime = topTime + i.ToString() + ") " + name + " - " + prettyTime(pair.Value.minutes) + "\n";
                i++;
                if(i > 5) break;
            }
            


            string text = data[player.userID].name + "\n\n   * PvP\n   Убийств: " + data[player.userID].pvp.kills.ToString() + "\n   Смертей: " + data[player.userID].pvp.deaths.ToString() + "\n   Побед в дуэли: " + data[player.userID].pvp.duelWin.ToString() +
            "\n\n   * Добыто руками (" + (data[player.userID].gather.wood + data[player.userID].gather.stone + data[player.userID].gather.sulfurOre +
            data[player.userID].gather.metalOre + data[player.userID].gather.hqmetalOre).ToString() + ")\n   Дерево: " + data[player.userID].gather.wood.ToString() +
            "\n   Камень: " + data[player.userID].gather.stone.ToString() + "\n   Железная руда: " + data[player.userID].gather.metalOre.ToString() + "\n   Серная руда: " + data[player.userID].gather.sulfurOre.ToString() +
            "\n   Мет. выс. к-ва: " + data[player.userID].gather.hqmetalOre.ToString() + "\n\n   * Добыто карьером (" + (data[player.userID].quarry.stone + data[player.userID].quarry.metal + data[player.userID].quarry.metalOre +
            data[player.userID].quarry.sulfurOre + data[player.userID].quarry.hqmetal + data[player.userID].quarry.hqmetalOre).ToString() + ")\n   Камень: " + data[player.userID].quarry.stone.ToString() +
            "\n   Железная руда: " + data[player.userID].quarry.metalOre.ToString() + "\n   Железо: " + data[player.userID].quarry.metal.ToString() + "\n   Серная руда: " +
            data[player.userID].quarry.sulfurOre.ToString() + "\n   Мет. руда высок. к-ва: " + data[player.userID].quarry.hqmetalOre.ToString() +
            "\n   Мет. высок. к-ва: " + data[player.userID].quarry.hqmetal.ToString() + "\n\n    "/*+"\n\n   * Подобрал с земли\n   Дерево: "+data[player.userID].pickup.wood.ToString()+"\n   Камень: "+
            data[player.userID].pickup.stone.ToString()+"\n   Железная руда: "+data[player.userID].pickup.metalOre+"\n   Серная руда: "+data[player.userID].pickup.sulfurOre+
            "\n   Ткань: "+data[player.userID].pickup.cloth*/;

            CuiHelper.DestroyUi(player, "StatsParent");
            startGui[player.userID] = CuiHelper.GetGuid();

            cui.createparentcurs("StatsParent", "0 0 0 0", "0.05 0.1", "0.95 0.95");

            cui.createimg("StatImg", "StatsParent", ImageStorage.FetchPng("stat"), "0 0", "1 1");

            cui.createtext("Text", "StatsParent", "", 24, "0.5 0.9", "0.8 0.99", TextAnchor.MiddleLeft);

            //cui.createbox("Box1", "StatsParent","0.3 0.6 0.3 0.5","0.02 0.05", "0.3 0.95");
            cui.createtext("StatsText", "StatsParent", "<color=#81feff>" + text + "</color>", 18, "0.06 0.1", "0.27 0.95", TextAnchor.MiddleLeft);

            //cui.createbox("Box2", "StatsParent","0.6 0.3 0.3 0.5","0.32 0.35", "0.61 0.88");
            cui.createtext("StatsTextTop", "StatsParent", "<color=#d24a43>" + topPvp + "</color>", 12, "0.36 0.36", "0.63 0.87", TextAnchor.MiddleLeft);

            //cui.createbox("Box3", "StatsParent","0.6 0.3 0.3 0.5","0.62 0.35", "0.99 0.88");
            cui.createtext("StatsTextTop", "StatsParent", "<color=#74b65f>" + topPve/*+topExp*/+ topTime + "</color>", 15, "0.6499 0.02", "0.9999 0.87", TextAnchor.MiddleLeft);

            //cui.createbox("Box4", "StatsParent","0.6 0.3 0.3 0.5","0.62 0.05", "0.99 0.34");
            //cui.createtext("StatsTextTop", "StatsParent", topExp, 18, "0.63 0.06", "0.98 0.33",TextAnchor.MiddleLeft);

            cui.createtext("StatsExitText", "StatsParent", "", 20, "0.32 0.18", "0.61 0.32", TextAnchor.MiddleCenter);
            cui.createbutton("StatsExitButton", "StatsParent", "drawTop", "StatsParent", "0 0 0 0", "0.32 0.18", "0.61 0.32");

            cui.createtext("StatsExitText", "StatsParent", "", 20, "0.32 0.05", "0.61 0.15", TextAnchor.MiddleCenter);
            cui.createbutton("StatsExitButton", "StatsParent", "", "StatsParent", "0 0 0 0", "0.32 0.05", "0.61 0.15");

            CuiHelper.AddUi(player, cui.elements);
            cui.elements.Clear();
        }

        string prettyTime(int time)
        {
            string timepretty;
            if(time < 60) timepretty = time.ToString() + " минут(ы)";
            else timepretty = (time / 60).ToString() + " час(а) " + (time % 60).ToString() + " минут(ы)";
            return timepretty;
        }

        topList calcSum()
        {
            topList a = new topList();
            a.list = new List<string>();
            var culture = new CultureInfo("en-GB");
            a.list.Add(DateTime.Now.ToString(culture));
            IOrderedEnumerable<KeyValuePair<ulong, PlayerData>> items = from pair in data
                                                                        orderby
(kills * pair.Value.pvp.kills - deaths * pair.Value.pvp.deaths + resGather * (pair.Value.gather.wood + pair.Value.gather.stone + pair.Value.gather.metalOre + pair.Value.gather.sulfurOre + pair.Value.gather.hqmetalOre) +
resQuarry * (pair.Value.quarry.stone + pair.Value.quarry.metal + pair.Value.quarry.metalOre + pair.Value.quarry.sulfurOre + pair.Value.quarry.hqmetal + pair.Value.quarry.hqmetalOre) + level)
descending
                                                                        select pair;
            int i = 1;
            a.arr = "";
            foreach(KeyValuePair<ulong, PlayerData> pair in items)
            {
                string name = pair.Value.name;
                if(name == null) name = "Неизвестный";
                string newstring = i.ToString() + ") " + name + " - " + (kills * pair.Value.pvp.kills - deaths * pair.Value.pvp.deaths + resGather * (pair.Value.gather.wood + pair.Value.gather.stone + pair.Value.gather.metalOre + pair.Value.gather.sulfurOre + pair.Value.gather.hqmetalOre) +
                resQuarry * (pair.Value.quarry.stone + pair.Value.quarry.metal + pair.Value.quarry.metalOre + pair.Value.quarry.sulfurOre + pair.Value.quarry.hqmetal + pair.Value.quarry.hqmetalOre) + level).ToString() + "\n";
                a.arr = a.arr + newstring;
                newstring = newstring.Replace("\n", "");
                a.list.Add(newstring);
                i++;
                if(i > topCount) break;
            }
            return a;
        }

        class topList
        {
            public List<string> list;
            public string arr;
        }

        void drawTopMenu(BasePlayer player)
        {

            topList topdata = calcSum();
            string topTop = topdata.arr;

            float points = kills * data[player.userID].pvp.kills - deaths * data[player.userID].pvp.deaths + resGather * (data[player.userID].gather.wood + data[player.userID].gather.stone + data[player.userID].gather.metalOre + data[player.userID].gather.sulfurOre + data[player.userID].gather.hqmetalOre) +
            resQuarry * (data[player.userID].quarry.stone + data[player.userID].quarry.metal + data[player.userID].quarry.metalOre + data[player.userID].quarry.sulfurOre + data[player.userID].quarry.hqmetal + data[player.userID].quarry.hqmetalOre) + level;



            CuiHelper.DestroyUi(player, "TopParent");
            startGui[player.userID] = CuiHelper.GetGuid();

            cui.createparentcurs("TopParent", "0 0 0 0", "0.05 0.1", "0.95 0.95");

            cui.createimg("StatImg2", "TopParent", ImageStorage.FetchPng("stat2"), "0 0", "1 1");

            cui.createtext("TopText", "TopParent", "", 24, "0.3 0.8", "0.9 0.95", TextAnchor.MiddleLeft);

            //cui.createbox("BoxTop", "TopParent","0.3 0.6 0.3 0.5","0.02 0.05", "0.38 0.75");
            cui.createtext("TopText", "TopParent", "<color=#81feff>" + topTop + "</color>", 17, "0.06 0.08", "0.37 0.72", TextAnchor.MiddleLeft);

            //cui.createtext("TopTimeText", "TopParent", "<color=black>"+topTime+"</color>", 18, "0.6 0.1", "0.99 0.74",TextAnchor.MiddleLeft);

            //cui.createbox("MeTop", "TopParent","0.3 0.6 0.3 0.5","0.52 0.6", "0.86 0.75");
            cui.createtext("MeText", "TopParent", "<color=#81feff>" + data[player.userID].name + " - " + points.ToString() + "</color>", 18, "0.53 0.61", "0.85 0.74", TextAnchor.MiddleLeft);

            cui.createtext("TopExitText", "TopParent", "", 20, "0.55 0.05", "0.75 0.15", TextAnchor.MiddleCenter);
            cui.createbutton("TopExitButton", "TopParent", "", "TopParent", "0 0 0 0", "0.55 0.05", "0.75 0.15");

            cui.createtext("TopbackText", "TopParent", "", 20, "0.55 0.2", "0.75 0.3", TextAnchor.MiddleCenter);
            cui.createbutton("TopbackButton", "TopParent", "stat", "TopParent", "0 0 0 0", "0.55 0.2", "0.75 0.3");

            CuiHelper.AddUi(player, cui.elements);
            cui.elements.Clear();
        }

        void writeTop()
        {
            topList topdata = calcSum();
            topFile.WriteObject(topdata.list);
        }


        //cache



        void debugtestcons(ConsoleSystem.Arg arg)
        {
            if(arg.Args == null) return;
            ConsoleSystem.Run(ConsoleSystem.Option.Server , arg.Args[0]);
        }

        void debugtestcons2(ConsoleSystem.Arg arg)
        {
            if(arg.Args == null) return;
            if(arg.Args.Length != 4) return;
            BasePlayer target = findPlayer(arg.Args[0]);
            if(target == null) return;
            float a;
            for(int i = 1; i < 4; i++)
                if(!Single.TryParse(arg.Args[i], out a)) return;
            core.Teleport(target, new Vector3(System.Convert.ToSingle(arg.Args[1]), System.Convert.ToSingle(arg.Args[2]), System.Convert.ToSingle(arg.Args[3])));
        }
        

        BasePlayer findPlayer(string name)
        {
            List<BasePlayer> list = new List<BasePlayer>();
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                if(player.displayName.ToLower().Contains(name.ToLower())) list.Add(player);
            }
            if(list.Count == 0) return null;
            if(list.Count > 1) return null;
            return list[0];
        }

        void StartSleeping(BasePlayer player)
        {
            if(player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if(!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }

        //GRAFOOON_______________________________________________________________________________________________

        public class cui
        {
            public static CuiElementContainer elements = new CuiElementContainer();

            //Элемент-родитель с курсором
            public static CuiElement createparentcurs(string name, string color, string anchmin, string anchmax)
            {
                CuiElement main = new CuiElement
                {
                    Name = name,
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchmin,
                            AnchorMax = anchmax
                        },
                        new CuiNeedsCursorComponent()
                    }
                };
                elements.Add(main);
                return main;
            }

            //Элемент-родитель без курсора
            public static CuiElement createparent(string name, string color, string anchmin, string anchmax)
            {
                CuiElement main = new CuiElement
                {
                    Name = name,
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchmin,
                            AnchorMax = anchmax
                        }
                    }
                };
                elements.Add(main);
                return main;
            }

            ////функция-шаблон для кнопки
            public static CuiElement createbutton(string name, string parent, string command, string close, string color, string anchmin, string anchmax)
            {
                CuiElement element = new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = command,
                            Close = close,
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchmin,
                            AnchorMax = anchmax
                        }
                    }
                };
                elements.Add(element);
                return element;
            }

            //функция-шаблон для прямоугольного фона
            public static CuiElement createbox(string name, string parent, string color, string anchmin, string anchmax)
            {
                CuiElement element = new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchmin,
                            AnchorMax = anchmax
                        }
                    }
                };
                elements.Add(element);
                return element;
            }

            //функция-шаблон для текста
            public static CuiElement createtext(string name, string parent, string text, int size, string anchmin, string anchmax, TextAnchor anch)
            {
                CuiElement element = new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = size,
                            Align = anch
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchmin,
                            AnchorMax = anchmax
                        }
                    }
                };
                elements.Add(element);
                return element;
            }

            //функция-шаблон для изображения
            public static CuiElement createimg(string name, string parent, string img, string anchmin, string anchmax)
            {
                CuiElement element = new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {

                        new CuiRawImageComponent
                        {
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Png = img
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchmin,
                            AnchorMax = anchmax
                        }
                    }
                };
                elements.Add(element);
                return element;
            }
        }
    }
}
