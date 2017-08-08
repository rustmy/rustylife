// Reference: Oxide.Core.RustyCore
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;
using RustyCore;

namespace Oxide.Plugins
{
    [Info("Duels", "bazuka5801 & vaalberith", "2.5.4")]
    public class Duels : RustPlugin
    {
        static Duels Instance;
        RCore core = Interface.Oxide.GetLibrary<RCore>();
        [PluginReference]
        Plugin Deposit;




        //дуэль

        public class DuelData
        {
            public Request requesta;
            public Dictionary<ulong, Vector3> coords = new Dictionary<ulong, Vector3>();
            public Dictionary<BasePlayer, bool> players1 = new Dictionary<BasePlayer, bool>();
            public Dictionary<BasePlayer, bool> players2 = new Dictionary<BasePlayer, bool>();
            public bool teleported = false;
            public Vector2i score = new Vector2i(0, 0);
            public string arena;


            public List<BasePlayer> Players()
            {
                var list = new List<BasePlayer>(players1.Keys);
                list.AddRange(players2.Keys);
                return list;
            }

            public bool EndRound => players1.Values.All(dead => dead) || players2.Values.All(dead => dead);

            public void NewRound()
            {
                var newPlayers = new Dictionary<BasePlayer, bool>();
                foreach (var pl in players1)
                    newPlayers[pl.Key] = false;
                players1 = newPlayers;

                newPlayers = new Dictionary<BasePlayer, bool>();
                foreach (var pl in players2)
                    newPlayers[pl.Key] = false;
                players2 = newPlayers;
            }

        }

        static List<DuelData> duels = new List<DuelData>();

        //инвентарь

        class PlayerInfo
        {
            public bool RestoreOnce = false;
            public List<SavedItem> Items;
        }

        class SavedItem
        {
            public string shortname;
            public int itemid;
            public string container;
            public float condition;
            public int amount;
            public int ammoamount;
            public string ammotype;
            public int flamefuel;
            public ulong skinid;
            public bool weapon;
            public List<SavedItem> mods;
        }

        static Dictionary<ulong, PlayerInfo> cachedInventories = new Dictionary<ulong, PlayerInfo>();

        //запросы

        public class Request
        {
            public BasePlayer player;
            public BasePlayer target;
            public int weaponSet = -1;
            public int timestamp;
            public bool hold = false;
            public bool active = false;
            public string arena;
            public bool clan;
            public bool deposit = false;
            public List<Vector3> spawns1;
            public List<Vector3> spawns2;
            public Request(BasePlayer _player, BasePlayer _target, bool _clan)
            {
                player = _player;
                target = _target;
                clan = _clan;
                timestamp = Facepunch.Math.Epoch.Current;
                var spawns = Instance.findSpawns(this);
                if (spawns == null)
                {
                    player.ChatMessage("<color=#fec384>Нет свободных спавнов. Попробуйте позже!</color>");
                    target.ChatMessage("<color=#fec384>Нет свободных спавнов. Попробуйте позже!</color>");
                    return;
                }
                spawns1 = spawns[0];
                spawns2 = spawns[1];
            }
        }

        static readonly List<Request> requests = new List<Request>();

        //арены

        class Arena
        {
            public string id;
            public List<MyVector> spawnsRaw1 = new List<MyVector>();
            public List<MyVector> spawnsRaw2 = new List<MyVector>();
            public bool clan = false;
            public List<Vector3> spawns1() => spawnsRaw1.Select(a => new Vector3(a.x, a.y, a.z)).ToList();
            public List<Vector3> spawns2() => spawnsRaw2.Select(a => new Vector3(a.x, a.y, a.z)).ToList();

            public override string ToString()
            {
                return
                    $"################\nID: {id}\nCLAN: {clan}\nSPAWN 1: {spawnsRaw1.Count}\nSPAWN 2: {spawnsRaw2.Count}";
            }
        }

        class MyVector
        {
            public float x;
            public float y;
            public float z;
            public MyVector(float _x, float _y, float _z)
            {
                x = _x;
                y = _y;
                z = _z;
            }
        }

        static List<Arena> arenas = new List<Arena>();
        DynamicConfigFile arenaFile = Interface.Oxide.DataFileSystem.GetFile("PlayerDuelArenas");

        class weaponSet
        {
            public string name = "...";
            public string pictureURL;
            public Dictionary<string, int> belt;
            public Dictionary<string, int> main;
            public List<string> wear;
            public weaponSet()
            {
                weaponSets.Add(this);
            }
        }

        static List<weaponSet> weaponSets = new List<weaponSet>();
        DynamicConfigFile weaponFile = Interface.Oxide.DataFileSystem.GetFile("PlayerDuelWeapons");
        private readonly Dictionary<ulong, int> cooldowns = new Dictionary<ulong, int>();
        const int COOLDOWN_SECONDS = 420;
        //менеджер запросов

        void WatchDog()
        {
            List<Request> toDelete = new List<Request>();
            timer.Every(1, () =>
            {
                int stamp = Facepunch.Math.Epoch.Current;
                foreach (Request request in requests)
                {
                    if (stamp - request.timestamp > 15 && !request.hold)
                    {
                        toDelete.Add(request);
                    }
                }

                foreach (Request request in toDelete)
                {
                    request.player.ChatMessage("<size=18><color=#ff0048>Время ожидания ответа вышло!</color></size>");
                    request.target.ChatMessage("<size=18><color=#ff0048>Время ожидания ответа вышло!</color></size>");
                    requests.Remove(request);
                }
                toDelete.Clear();
                List<ulong> uids = cooldowns.Keys.ToList();
                for (int i = uids.Count - 1; i >= 0; i--)
                {
                    var userid = uids[i];
                    var time = cooldowns[userid];
                    if (--time < 0)
                        cooldowns.Remove(userid);
                    else cooldowns[userid] = time;
                }
            });
        }


        

        [ConsoleCommand("duel")]
        void cmdDuel(ConsoleSystem.Arg arg)
        {
            if (arg.Args[0] == "clear" && arg.Connection == null)
            {
                DuelClear();
                return;
            }
            var player = arg.Player();
            var args = arg.Args.ToList();
            args.RemoveAt(arg.Args.Length - 1);
            if (player)
                duelChat(player, "", args.ToArray());
        }

        void DuelClear()
        {
            int count = duels.Count;
            for (int i = duels.Count - 1; i >= 0; i--)
            {
                var duel = duels[i];
                closeDuel(duel);
            }
            requests.Clear();
            Puts($"Complete {count} duels!");
        }

        [ChatCommand("duel")]
        void duelChat(BasePlayer player, string cmd, string[] args)
        {
            /*if (!player.IsAdmin)
            {
                player.ChatMessage("Дуэли на тех. работах!");
                return;
            }*/
            var help = "<color=#ccff00>U</color>";
            if (args == null || args.Length == 0)
            {
                player.ChatMessage(help);
                return;
            }
            else if (args[0] == "spawns")
            {
                if (!player.IsAdmin) return;
                foreach (var arena in arenas)
                {
                    foreach (var spawn in arena.spawns1())
                        player.SendConsoleCommand("ddraw.sphere", 10, Color.green, spawn, 2);
                    foreach (var spawn in arena.spawns2())
                        player.SendConsoleCommand("ddraw.sphere", 10, Color.blue, spawn, 2);
                }
            }
            else if (args.Length >= 2)
            {
                if (args[0] == "ask")
                {
                    //запрос на дуэль
                    BasePlayer target = findPlayer(player, args[1]);
                    if (target == null) return;
                    string msg;
                    if ((msg = askDuel(player, target)) == null)
                    {
                        player.ChatMessage("<color=#fee3b4>Вы вызвали </color><size=16><color=#ff5400>" + target.displayName + "</color> </size><color=#fee3b4>на дуэль.\nПодождите, пока он ответит...</color>");
                        target.ChatMessage("<color=#fee3b4>Вас вызвал на дуэль </color><size=16><color=#ff5400>" + player.displayName + "</color></size>\n<color=#fee3b4>Принять вызов клавиша <color=yellow>I(Ш)</color>\n На решение 15 сек....</color>");
                    }
                    else player.ChatMessage(msg);
                }
                else if (args[0] == "clan")
                {
                    if (Clans == null) return;
                    //запрос на дуэль клановую
                    BasePlayer target = findPlayer(player, args[1]);
                    if (target == null)
                        return;
                    askDuelClan(player, target);

                }
                else if (args[0] == "createarena")
                {
                    createOrUpdateZone(args[1], player, 0);
                }
                else if (args[0] == "spawn")
                {
                    int team = Convert.ToInt32(args[2]);
                    if (!player.IsAdmin) return;
                    createOrUpdateZone(args[1], player, team);
                }
                else if (args.Length == 2 && args[0] == "arenaclan")
                {
                    Arena arena = arenas.Find(x => x.id == args[1]);
                    if (arena != null)
                    {
                        arena.clan = !arena.clan;
                        player.ChatMessage(arena.clan ? "Теперь эта арена для кланов" : "Теперь эта арена для 2 игроков");
                        arenaFile.WriteObject(arenas);
                    }
                }

                else if (args[0] == "delete")
                {
                    if (!player.IsAdmin) return;
                    Arena arena = arenas.Find(x => x.id == args[1]);
                    if (arena != null)
                    {
                        arenas.Remove(arena);
                        player.ChatMessage("Арена " + args[1] + " удалена!");
                        arenaFile.WriteObject(arenas);
                    }
                    else player.ChatMessage("Арена " + args[1] + " не найдена!");
                }
            }

            else if (args.Length == 1)
            {
                if (args[0] == "y")
                {
                    Request request = requests.Find(x => x.player == player);
                    if (request != null)
                    {
                        if (request.weaponSet == -1)
                            player.ChatMessage("<color=#fee3b4>Ждите ответа оппонента!</color>");
                        else if (!request.active)
                        {
                            aboutToStart(request);
                            return;
                        }
                        else return;
                    }

                    player.ChatMessage(acceptRequest(player)
                        ? "<color=#ff0048>Вы ответили на запрос!</color>"
                        : "<color=#ff0048>Нет входящих запросов!</color>");
                }
                if (args[0] == "n")
                {
                    if (canBeRequested(player)) player.ChatMessage("<color=#ff0048>Нет активных запросов!</color>");
                    else
                    {
                        Request request = findRequest(player);
                        if (request.active)
                        {
                            player.ChatMessage("<color=#ff0048>Нельзя отменить во время дуэли! Если застрял - kill в консоли.</color>");
                            return;
                        }
                        //
                        request.player.ChatMessage("<color=#ff0048>Дуэль отменена по воле одного из игроков!</color>");
                        request.target.ChatMessage("<color=#ff0048>Дуэль отменена по воле одного из игроков!</color>");
                        requests.Remove(request);
                    }
                }
            }

            else player.ChatMessage(help);
        }

        [ConsoleCommand("duelweapon")]
        void duelconsole(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null) return;
            var target = arg.Connection?.player as BasePlayer;
            if (target == null) return;
            Request request = requests.Find(x => x.target == target);
            if (request?.weaponSet != -1) return;

            request.weaponSet = Convert.ToInt32(arg.Args[0]);
            Interface.CallHook("PlayerDuelChooseWeapon", target, request.weaponSet);
            request.player.ChatMessage("<color=#fee3b4>Выбрано оружие:</color> <size=16><color=#ff5400>" + weaponSets[request.weaponSet].name + "</color></size> \n <color=#fee3b4>Чтобы продолжить нажмите клавишу <color=#ff5400>I(Ш)</color></color>");
            request.target.ChatMessage("<color=#fee3b4>Противник выбрал:</color> <size=16><color=#ff5400>" + weaponSets[request.weaponSet].name + "</color></size>");
            request.timestamp = Facepunch.Math.Epoch.Current;
            request.hold = false;
        }

        //API
        [PluginReference]
        Plugin EventManager;

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

        bool inDuel(BasePlayer player)
        {
            DuelData duel = duels.Find(x => x.Players().Contains(player) && x.teleported);
            return duel != null;
        }

        //хуки


        void OnServerInitialized()
        {
            Instance = this;
            rust.RunServerCommand("reload Deposit");
            arenas = arenaFile.ReadObject<List<Arena>>();
            weaponSets = weaponFile.ReadObject<List<weaponSet>>();
            weaponFile.WriteObject(weaponSets);
            WatchDog();
        }

        void Unload()
        {
            foreach (var duel in duels)
            {
                closeDuel(duel, true);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, "DuelParent");
        }


        void OnPreServerRestart()
        {
            DuelClear();
        }

        //смерть

        void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim == null) return;
            BasePlayer vict = victim.ToPlayer();
            if (vict == null) return;
            //поиск дуэли
            DuelData duel = duels.Find(x => x.Players().Contains(vict));
            if (duel == null) return;

            Interface.CallHook("PlayerDuelDeath", victim);

            var attacker = info.InitiatorPlayer;
            if (attacker != null)
            {
                Interface.CallHook("PlayerDuelKill", attacker);
            }

            handleDeath(duel, vict);
        }

        //функции
        bool ArenaIsFree(Arena arena)
        {
            foreach (var req in requests)
                if (req.arena == arena.id)
                    return false;
            foreach (var duel in duels)
                if (duel.requesta.arena == arena.id)
                    return false;
            return true;
        }

        List<List<Vector3>> findSpawns(Request request)
        {
            var spawns = new List<List<Vector3>>();
            foreach (Arena arena in arenas)
            {
                if (request.clan)
                {
                    if (arena.clan && ArenaIsFree(arena))
                    {
                        request.arena = arena.id;
                        spawns.Add(arena.spawns1());
                        spawns.Add(arena.spawns2());
                        return spawns;
                    }
                    continue;
                }
                if (ArenaIsFree(arena) && !arena.clan)
                {
                    request.arena = arena.id;
                    spawns.Add(arena.spawns1());
                    spawns.Add(arena.spawns2());
                    return spawns;
                }
            }
            return null;
        }

        void createOrUpdateZone(string _id, BasePlayer player, int team)
        {
            Arena arena = arenas.Find(x => x.id == _id);
            if (arena == null)
            {
                player.ChatMessage("Создание новой арены");
                Arena i = new Arena
                {
                    id = _id
                };
                arenas.Add(i);
            }
            else
            {
                var spawn = player.GetNetworkPosition();
                switch (team)
                {
                    case 1:
                        player.ChatMessage($"Создан спавн для {team} команды");
                        arena.spawnsRaw1.Add(new MyVector(spawn.x, spawn.y, spawn.z));
                        break;
                    case 2:
                        player.ChatMessage($"Создан спавн для {team} команды");
                        arena.spawnsRaw2.Add(new MyVector(spawn.x, spawn.y, spawn.z));
                        break;
                    default:
                        player.ChatMessage("Неверно указан номер команды!");
                        break;
                }
            }
            arena = arenas.Find(x => x.id == _id);
            if (arena == null)
            {
                player.ChatMessage("Какая-то ошибка при создании арены!");
                return;
            }
            foreach (var spawn in arena.spawns1())
                player.SendConsoleCommand("ddraw.sphere", 10, Color.green, spawn, 2);
            foreach (var spawn in arena.spawns2())
                player.SendConsoleCommand("ddraw.sphere", 10, Color.blue, spawn, 2);
            arenaFile.WriteObject(arenas);
        }

        Dictionary<string, Request> hashRequests = new Dictionary<string, Request>();
        void aboutToStart(Request request)
        {
            request.hold = true;
            request.active = true;

            var hash = Deposit.Call<string>("StartDeposit", new List<BasePlayer>() { request.player, request.target });
            hashRequests[hash] = request;
            request.deposit = true;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var duel = duels.Find(d => d.players1.ContainsKey(player) || d.players2.ContainsKey(player));
            if (duel != default(DuelData))
            {
                int winner = duel.players1.ContainsKey(player) ? 1 : 0;
                duel.score.x = winner == 0 ? 3 : 0;
                duel.score.y = winner == 1 ? 3 : 0;
                closeDuel(duel);
            }
            var req = requests.Find(r => r.player == player || r.target == player);
            if (req != default(Request))
            {
                if (player != req.player) req.player.ChatMessage("<size=16><color=#ff5400>Не удалось поставить ставку!</color></size>");
                if (player != req.target) req.target.ChatMessage("<size=16><color=#ff5400>Не удалось поставить ставку!</color></size>");
                requests.Remove(req);
            }
        }


        [HookMethod("OnDepositEnd")]
        void OnDepositEnd(bool success, string hash)
        {
            if (!hashRequests.ContainsKey(hash)) return;
            var req = hashRequests[hash];
            if (success)
            {
                req.player.ChatMessage("<size=18><color=#fee3b4>Дуэль скоро начнется...</color></size>");
                req.target.ChatMessage("<size=18><color=#fee3b4>Дуэль скоро начнется...</color></size>");
                timer.In(5, () => createDuel(req));
            }
            else
            {
                req.player.ChatMessage("<size=16><color=#ff5400>Не удалось поставить ставку!</color></size>");
                req.target.ChatMessage("<size=16><color=#ff5400>Не удалось поставить ставку!</color></size>");
                requests.Remove(req);
            }
            hashRequests.Remove(hash);
        }


        void drawChooseWeapon(BasePlayer player)
        {
            //рисуем гуи с пушками, назначаем команды консольные
            CuiHelper.DestroyUi(player, "DuelParent");

            string mainFrameColor = "0.5 0.5 0.5 0.5";
            float gap = 0.05f;
            float width = 0.08f;
            float height = 0.20f;
            float startxBox = 0.03f;
            float startyBox = 1f - height - 0.05f;

            float xmin = startxBox;
            float ymin = startyBox;

            cui.createparentcurs("DuelParent", mainFrameColor, "0.1 0.15", "0.9 0.95");
            int i = 0;
            foreach (weaponSet set in weaponSets)
            {
                if (ymin < 0.2) continue;
                /*string text="";
                foreach (KeyValuePair<string,int> weap in set.weapons)
                {
                    ItemDefinition itemdef = ItemManager.FindItemDefinition(weap.Key);
                    if (itemdef == null) return;
                    text += itemdef.displayName.translated +" x"+weap.Value.ToString()+ "\n";
                }

                foreach (KeyValuePair<string,int> ammo in set.ammo)
                {
                    ItemDefinition itemdef = ItemManager.FindItemDefinition(ammo.Key);
                    if (itemdef == null) return;
                    text += itemdef.displayName.translated +" x"+ammo.Value.ToString()+ "\n";
                }*/

                cui.createtext(i + "_text", "DuelParent", set.name, 16, xmin + " " + ymin, (xmin + width) + " " + (ymin + height), TextAnchor.UpperCenter);
                if (set.pictureURL != null) cui.createimgurl(i + "_pic", "DuelParent", set.pictureURL, xmin + " " + ymin, (xmin + width) + " " + (ymin + height));

                cui.createbutton(i + "_but", "DuelParent", "duelweapon " + i, "DuelParent", "0 0 0 0", xmin + " " + ymin, (xmin + width) + " " + (ymin + height));

                xmin += width + gap;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + gap;
                }
                i++;
            }

            cui.createtext("DuelExitText", "DuelParent", "Отменить", 20, "0.85 0.05", "0.95 0.15", TextAnchor.MiddleCenter);
            cui.createbutton("DuelExitButton", "DuelParent", "duels.cancel", "DuelParent", "0.9 0 0 0.5", "0.85 0.05", "0.95 0.15");
            CuiHelper.AddUi(player, cui.elements);
            cui.elements.Clear();
        }

        [ConsoleCommand("duels.cancel")]
        void cmdDuelCancel(ConsoleSystem.Arg arg)
        {
            var req = findRequest(arg.Player());
            req.player.ChatMessage("<size=16><color=#ff5400>Дуэль отменена!</color></size>");
            req.target.ChatMessage("<size=16><color=#ff5400>Дуэль отменена!</color></size>");
            requests.Remove(req);
        }
        Request findRequest(BasePlayer player)
        {
            Request request = requests.Find(x => x.player == player);
            if (request == null)
            {
                request = requests.Find(x => x.target == player);
                if (request == null)
                    return null;
            }
            return request;
        }

        bool acceptRequest(BasePlayer target)
        {
            Request request = requests.Find(x => x.target == target);
            if (request == null) return false;
            request.player.ChatMessage(target.displayName + " <size=16><color=#ff5400>принял вызов!\n Сейчас он выбирает оружие...</color></size>");
            request.hold = true;
            drawChooseWeapon(target);
            timer.Once(20f, () =>
            {
                if (request.deposit) return;
                CuiHelper.DestroyUi(target, "DuelParent");
                request.player.ChatMessage("<size=18><color=#ff5400>Время ожидания ответа вышло!</color></size>");
                request.target.ChatMessage("<size=18><color=#ff5400>Время ожидания ответа вышло!</color></size>");
                requests.Remove(request);
            });

            return true;
        }

        bool canBeRequested(BasePlayer player)
        {
            bool ok = true;
            foreach (Request request in requests)
            {
                if (request.player == player) ok = false;
                if (request.target == player) ok = false;
            }
            if (InEvent(player) || inDuel(player)) ok = false;
            foreach (DuelData duel in duels)
                if (duel.Players().Contains(player)) ok = false;
            return ok;
        }

        string askDuel(BasePlayer asker, BasePlayer target)
        {
            if (!canBeRequested(asker) || !canBeRequested(target)) return "<color=#ff5400>Не удалось отправить запрос, возможно вы или игрок на ивенте!</color>";
            if (cooldowns.ContainsKey(asker.userID)) return $"<size=16><color=#ff5400>Для использования дуэли подождите <color=#ff5400>{cooldowns[asker.userID]}</color> секунд...</color></size>";
            if (cooldowns.ContainsKey(target.userID)) return "<size=16><color=#ff5400>Ваш соперник на перезарядке!</color></size>";
            var req = new Request(asker, target, false);
            if (req.spawns1 == null)
            {
                return "<color=#ff5400>Нет свободных арен, попробуйте позже.</color>";
            }
            requests.Add(req);
            return null;
        }

        [PluginReference]
        Plugin Clans;



        void askDuelClan(BasePlayer asker, BasePlayer target)
        {
            if (cooldowns.ContainsKey(asker.userID))
            {
                asker.ChatMessage($"<size=16><color=#fee3b4>Для использования дуэли подождите <color=#ff5400>{cooldowns[asker.userID]}</color> секунд...</color></size>");
                return;
            }
            if (cooldowns.ContainsKey(target.userID))
            {
                asker.ChatMessage("<size=16><color=#fee3b4>Ваш соперник на перезарядке!</color></size>");
                return;
            }
            var clan1tag = (string)Clans?.Call("GetClanOf", new object[] { asker });
            var clan2tag = (string)Clans?.Call("GetClanOf", new object[] { target });
            if (clan1tag == null || clan2tag == null || clan1tag == clan2tag)
            {
                asker.ChatMessage("<color=#ff5400>У Вас или у цели нет Команды!</color>");
                return;
            }
            asker.ChatMessage("<color=#fee3b4>Вы вызвали <size=16><color=#ff5400>" + target.displayName + "</color></size> из команды <size=16><color=#ff5400>" + clan2tag + "</color></size> на командную дуэль.\n Подождите, пока он ответит...</color>");
            target.ChatMessage("<color=#fee3b4>Вас вызвал на командную дуэль <size=16><color=#ff5400>" + asker.displayName + "</color></size> из команды <size=16><color=#ff5400>" + clan1tag + "</color></size> \n Выберите оружие клавиша <color=#ff5400>I(Ш)</color>\n На решение 15 сек....</color>");

            if (!canBeRequested(asker) || !canBeRequested(target))
            {
                asker.ChatMessage("<color=#fee3b4>Вы не можете играть в дуэли!</color>");
                return;
            }

            var req = new Request(asker, target, true);
            if (req.spawns1 == null)
            {
                asker.ChatMessage("<color=#ff5400>Нет свободных арен!</color>");
                return;
            }
            requests.Add(req);
        }

        BasePlayer findPlayer(BasePlayer asker, string name)
        {
            var list =
                BasePlayer.activePlayerList.Where(
                    player => player.displayName.ToLower().Contains(name.ToLower()) && asker != player).ToList();
            if (list.Count == 0)
            {
                asker.ChatMessage("<color=#ff5400>Игрок не найден!</color>");
                return null;
            }
            if (list.Count <= 1) return list[0];

            asker.ChatMessage("<color=#ff5400>Несколько похожих имен:</color>");
            foreach (BasePlayer player in list)
            {
                asker.ChatMessage(player.displayName + "\n");
            }
            asker.ChatMessage("<color=#ff5400>Уточните запрос.</color>");
            return null;

        }

        BasePlayer findPlayerID(ulong id)
        {
            return BasePlayer.activePlayerList.FirstOrDefault(player => player.userID == id);
        }

        void createDuel(Request request)
        {
            DuelData duel = new DuelData
            {
                requesta = request,
                arena = request.arena,
            };
            if (request.clan)
            {
                var members1 = ((JObject)Clans?.Call("GetClan", Clans.Call("GetClanOf", request.player.userID)))["members"].ToObject<List<ulong>>();
                if (members1 != null)
                    foreach (ulong id in members1)
                    {
                        BasePlayer man = findPlayerID(id);
                        if (man == null) continue;
                        duel.players1.Add(man, false);
                    }

                var members2 = ((JObject)Clans?.Call("GetClan", Clans.Call("GetClanOf", request.target.userID)))["members"].ToObject<List<ulong>>();
                if (members2 != null)
                    foreach (ulong id in members2)
                    {
                        BasePlayer man = findPlayerID(id);
                        if (man == null) continue;
                        duel.players2.Add(man, false);
                    }
            }
            else
            {
                duel.players1.Add(request.player, false);
                duel.players2.Add(request.target, false);
            }
            LogDuels("Участники " + string.Join(", ", duel.Players().Select(p => p.displayName).ToArray()));
            // Проверка на спящих
            foreach (var player in duel.Players())
            {
                player.Hurt(0.1f);
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) || player.IsSleeping() || player.IsDead() || InEvent(player))
                {
                    request.player.ChatMessage("<color=#ff5400>Кто-то спит или умер, дуэль отменена!</color>");
                    request.target.ChatMessage("<color=#ff5400>Кто-то спит или умер, дуэль отменена!</color>");
                    requests.Remove(request);
                    return;
                }
            }

            timer.Once(3f, () =>
            {
                duels.Add(duel);
                foreach (BasePlayer player in duel.Players())
                    player.ChatMessage("<color=#ff5400><size=18>Дуэль началась. Удачных побед!</size></color>");
                DuelTimer(duel);
            });

            for (int i = 0; i < duel.players1.Count; i++)
                setupPlayer(duel, duel.players1.Keys.ToList()[i], duel.requesta.spawns1[i]);
            for (int i = 0; i < duel.players2.Count; i++)
                setupPlayer(duel, duel.players2.Keys.ToList()[i], duel.requesta.spawns2[i]);


        }

        private Dictionary<string, int> itemIds = new Dictionary<string, int>()
        {
            {"ammo.rifle", 815896488},
            {"ammo.shotgun.slug", 1819281075},
            {"ammo.shotgun",-1035059994  },
            {"ammo.pistol", -533875561}
        };
        void setupPlayer(DuelData duel, BasePlayer player, Vector3 spawn)
        {
            if (duel.coords.ContainsKey(player.userID)) return;
            duel.coords[player.userID] = player.transform.position;
            player.health = 100;
            timer.Once(3f, () =>
            {
                if (player.inventory?.loot != null)
                    player.EndLooting();
                duel.teleported = true;
                SaveInventory(player);
                player.inventory.Strip();
                foreach (var belt in weaponSets[duel.requesta.weaponSet].belt)
                {
                    var itemdef = ItemManager.FindItemDefinition(belt.Key);
                    if (itemdef == null) continue;
                    player.inventory.GiveItem(ItemManager.CreateByItemID(itemdef.itemid, belt.Value),
                        player.inventory.containerBelt);
                }
                foreach (var main in weaponSets[duel.requesta.weaponSet].main)
                {
                    Item item;
                    if (main.Key.Contains("ammo"))
                    {
                        item = ItemManager.CreateByItemID(itemIds[main.Key], main.Value);
                    }
                    else
                    {
                        var itemdef = ItemManager.FindItemDefinition(main.Key);
                        if (itemdef == null) continue;
                        item = ItemManager.CreateByItemID(itemdef.itemid, main.Value);
                    }
                    player.inventory.GiveItem(item, player.inventory.containerMain);
                }
                foreach (string wear in weaponSets[duel.requesta.weaponSet].wear)
                {
                    var itemdef = ItemManager.FindItemDefinition(wear);
                    if (itemdef == null) continue;
                    player.inventory.GiveItem(ItemManager.CreateByItemID(itemdef.itemid, 1),
                        player.inventory.containerWear);
                }
                core.Teleport(player, spawn);
                CommunityEntity.ServerInstance.StartCoroutine(EndSleepingAndHeal(player));
                player.Heal(player.MaxHealth());
            });
        }


        void timeOut(DuelData duel)
        {
            if (duel == null) return;
            if (!duels.Contains(duel)) return;
            foreach (BasePlayer player in duel.Players())
                player.ChatMessage("<color=#ff5400>Время вышло! Дуэль окончена.</color>");
            closeDuel(duel);
        }

        void closeDuel(DuelData duel, bool unload = false)
        {
            foreach (var player in duel.Players())
                if (player.userID != 76561198134194280)
                    cooldowns[player.userID] = COOLDOWN_SECONDS;
            if (duel.score.x == duel.score.y || unload)
            {
                duel.Players().ForEach(p =>
                {
                    p.ChatMessage(
                        "<color=#ff5400><size=18>Победила дружба</size></color>");
                });
                if (unload)
                {
                    DestroyTimerUI(duel);
                    foreach (BasePlayer player in duel.Players())
                        restoreAll(player, duel);
                }
                else
                {
                    Deposit.Call("DuelEnd", true, duel.requesta.player);
                    Interface.CallHook(duel.requesta.clan ? "ClanDuelEnded" : "CommonDuelEnded",
                        duel.requesta.player, duel.requesta.target, true);
                }
            }
            else if (duel.score.y > duel.score.x)
            {
                PrintToChat("<color=#fee3b4><size=18>Команда <color=#ff5400>" + duel.requesta.target.displayName +
                            "</color> победила!</size></color>");
                Deposit.Call("DuelEnd", false, duel.requesta.target);
                Interface.CallHook(duel.requesta.clan ? "ClanDuelEnded" : "CommonDuelEnded", duel.requesta.target,
                    duel.requesta.player, false);
            }
            else
            {
                PrintToChat("<color=#fee3b4><size=18>Команда <color=#ff5400>" + duel.requesta.player.displayName +
                            "</color> победила!</size></color>");
                Deposit.Call("DuelEnd", false, duel.requesta.player);
                Interface.CallHook(duel.requesta.clan ? "ClanDuelEnded" : "CommonDuelEnded", duel.requesta.player,
                    duel.requesta.target, false);
            }
            requests.Remove(duel.requesta);
            DestroyTimerUI(duel);
            if (!unload)
                timer.Once(1f, () =>
                {
                    foreach (BasePlayer player in duel.Players())
                        if (player.IsConnected)
                            restoreAll(player, duel);
                        else
                        {
                            duel.players1.Remove(player);
                            duel.players2.Remove(player);
                            player.inventory.Strip();
                            player.Kill();
                        }
                });
        }
        List<BasePlayer> respawn = new List<BasePlayer>();
        void restoreAll(BasePlayer player, DuelData duel)
        {
            Vector3 pos;
            if (!duel.coords.TryGetValue(player.userID, out pos))
                pos = SpawnHandler.GetSpawnPoint().pos;
            if (player.IsDead())
            {
                player.RespawnAt(pos, Quaternion.identity);
                respawn.Add(player);
            }
            if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) && player.IsSleeping())
                player.EndSleeping();
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) || player.IsSleeping() || player.IsDead())
            {
                timer.Once(0.1f, () => restoreAll(player, duel));
                return;
            }
            if (!respawn.Contains(player))
                core.Teleport(player, pos);
            RestoreInventory(player);
            duel.coords.Remove(player.userID);
            player.health = 100;
            player.metabolism.bleeding.value = 0;
            RemoveInventory(player);
            DestroyTimerUI(duel);
            duel.players1.Remove(player);
            duel.players2.Remove(player);
            if (duel.coords.Count <= 0) duels.Remove(duel);
            respawn.Remove(player);
        }

        private float lastUpdate = 0;
        [HookMethod("OnTick")]
        void OnTick()
        {
            if (Time.time > lastUpdate + 0.03f)
            {
                lastUpdate = Time.time;
                HeadHighlight();
            }
        }

        void HeadHighlight()
        {
            foreach (var duel in duels)
                if (duel.requesta.clan)
                {
                    foreach (var pl in duel.players1.Keys)
                        foreach (var pl2 in duel.players1.Keys)
                            if (pl2 != pl)
                                pl.SendConsoleCommand("ddraw.text", 0.1, 0,
                                    pl2.GetNetworkPosition() + new Vector3(0, 1f, 0), "<color=green><size=28>︽</size></color>");

                    foreach (var pl in duel.players2.Keys)
                        foreach (var pl2 in duel.players2.Keys)
                            if (pl2 != pl)
                                pl.SendConsoleCommand("ddraw.text", 0.1, 0,
                                    pl2.GetNetworkPosition() + new Vector3(0, 1f, 0), "<color=green><size=28>︽</size></color>");
                }
        }


        void WaitReceivingSnapShot(DuelData duel, List<BasePlayer> players, List<Vector3> spawns, Action callback, Action failCallback, int it = 0)
        {
            int i = 0;
            foreach (var player in players)
            {
                if (player.IsDead()) player.RespawnAt(spawns[i], Quaternion.identity);
                if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) && player.IsSleeping())
                    player.EndSleeping();
                i++;
            }
            timer.Once(1f, () =>
            {
                if (it >= 40)
                {
                    players.ForEach(p =>
                    {
                        p.ChatMessage(
                            "<color=#fee3b4><size=18>Битва окончилась!!!\nКто-то не нажал возродиться!!!</size></color>");
                    });
                    closeDuel(duel);
                    return;
                }
                if (
                    players.Any(
                        player =>
                            player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) || player.IsSleeping() ||
                            player.IsDead()))
                {
                    it += 1;
                    WaitReceivingSnapShot(duel, players, spawns, callback, failCallback, it);
                    return;
                }
                callback?.Invoke();
            });
        }

        void handleDeath(DuelData duel, BasePlayer victim)
        {
            if (duel.players1.ContainsKey(victim))
            {
                duel.players1[victim] = true;
            }
            else if (duel.players2.ContainsKey(victim))
            {
                duel.players2[victim] = true;
            }
            if (!duel.EndRound) return;
            duel.NewRound();
            if (duel.players1.ContainsKey(victim))
            {
                duel.score += new Vector2i(0, 1);
            }
            else
            {
                duel.score += new Vector2i(1, 0);
            }
            if (duel.score.x + duel.score.y >= 3 || duel.score.x >= 2 || duel.score.y >= 2)
            {
                closeDuel(duel);
                return;
            }
            duel.Players().ForEach(p => p.ChatMessage($"<color=#fee3b4>Скоро начнётся следующий раунд</color>"));

            int indexX = 0;
            int indexY = 0;
            List<Vector3> spawns = duel.Players().Select(p =>
            {
                Vector3 playerspawn = duel.players1.ContainsKey(p)
                    ? duel.requesta.spawns1[indexX]
                    : duel.requesta.spawns2[indexY];
                if (duel.players1.ContainsKey(p)) indexX++;
                else indexY++;
                return playerspawn;
            }).ToList();
            int i = 0;
            Dictionary<BasePlayer, Vector3> tpPlayers = duel.Players().ToDictionary(p => p, p =>
            {
                var vec = spawns[i];
                i++;
                return vec;
            }).Where(p => !p.Key.IsDead()).ToDictionary(p => p.Key, p => p.Value);
            WaitReceivingSnapShot(duel, duel.Players(), spawns, () =>
            {
                foreach (var p in tpPlayers)
                    core.Teleport(p.Key, p.Value);
                duel.Players().ForEach(player =>
                {
                    player.inventory.Strip();
                    foreach (var belt in weaponSets[duel.requesta.weaponSet].belt)
                    {
                        var itemdef = ItemManager.FindItemDefinition(belt.Key);
                        if (itemdef == null) continue;
                        player.inventory.GiveItem(ItemManager.CreateByItemID(itemdef.itemid, belt.Value),
                            player.inventory.containerBelt);
                    }
                    foreach (var main in weaponSets[duel.requesta.weaponSet].main)
                    {
                        var itemdef = ItemManager.FindItemDefinition(main.Key);
                        if (itemdef == null) continue;
                        player.inventory.GiveItem(ItemManager.CreateByItemID(itemdef.itemid, main.Value),
                            player.inventory.containerMain);
                    }
                    foreach (var wear in weaponSets[duel.requesta.weaponSet].wear)
                    {
                        var itemdef = ItemManager.FindItemDefinition(wear);
                        if (itemdef == null) continue;
                        player.inventory.GiveItem(ItemManager.CreateByItemID(itemdef.itemid, 1),
                            player.inventory.containerWear);
                    }
                    CommunityEntity.ServerInstance.StartCoroutine(EndSleepingAndHeal(player));
                });
            }, () => duel.Players().ForEach(p => restoreAll(p, duel)));
        }


        IEnumerator EndSleepingAndHeal(BasePlayer player)
        {
            while (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                yield return new WaitForSeconds(0.1f);
            player.EndSleeping();
            player.health = 100;
            player.metabolism.bleeding.value = 0;
        }


        void DuelTimer(DuelData duel, int time = 720)
        {
            if (time == 720)
            {
                duel.Players().ForEach(p => core.DrawUI(p, "Duels", "timer_image"));
            }
            if (!duels.Contains(duel))
            {
                DestroyTimerUI(duel);
                return;
            }
            if (time <= 0)
            {
                timeOut(duel);
                return;
            }
            DrawTimerUI(duel, time);
            timer.Once(1, () => { DuelTimer(duel, --time); });
        }

        void DrawTimerUI(DuelData duel, int time)
        {
            duel.Players().ForEach(p => core.DrawUI(p, "Duels", "timer_text", time.ToString()));
        }

        void DestroyTimerUI(DuelData duel)
        {
            duel.Players().ForEach(p =>
            {
                core.DestroyUI(p, "Duels", "timer_text");
                core.DestroyUI(p, "Duels", "timer_image");
            });
        }

        //инвентарь

        bool SaveInventory(BasePlayer player)
        {
            List<SavedItem> items = GetPlayerItems(player);
            if (!cachedInventories.ContainsKey(player.userID))
                cachedInventories.Add(player.userID, new PlayerInfo { });
            cachedInventories[player.userID].Items = items;


            StringBuilder sb = new StringBuilder(500);

            sb.Append(player.displayName + " SAVE" + '\n');
            foreach (var i in items)
                sb.Append(i.shortname + '\n');
            LogItems(sb.ToString());

            sb.Clear();
            return true;
        }

        List<SavedItem> GetPlayerItems(BasePlayer player)
        {
            List<SavedItem> kititems = (from item in player.inventory.containerBelt.itemList where item != null select ProcessItem(item, "belt")).ToList();
            kititems.AddRange(from item in player.inventory.containerWear.itemList where item != null select ProcessItem(item, "wear"));
            kititems.AddRange(from item in player.inventory.containerMain.itemList where item != null select ProcessItem(item, "main"));
            return kititems;
        }

        SavedItem ProcessItem(Item item, string container)
        {
            SavedItem iItem = new SavedItem
            {
                shortname = item.info?.shortname,
                amount = item.amount,
                mods = new List<SavedItem>(),
                container = container,
                skinid = item.skin
            };
            if (item.info == null) return iItem;
            iItem.itemid = item.info.itemid;
            iItem.weapon = false;
            if (item.hasCondition)
                iItem.condition = item.condition;
            FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();
            if (flameThrower != null)
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
                        iItem.mods.Add(ProcessItem(mod, "noun"));
            return iItem;
        }

        bool RemoveInventory(BasePlayer player)
        {
            if (cachedInventories.ContainsKey(player.userID))
            {
                cachedInventories.Remove(player.userID);
                return true;
            }
            return false;
        }

        bool RestoreInventory(BasePlayer player)
        {
            if (!cachedInventories.ContainsKey(player.userID))
                return false;
            player.inventory.Strip();
            StringBuilder sb = new StringBuilder(500);

            sb.Append($"RESTORE {player.displayName}\n");
            foreach (SavedItem kitem in cachedInventories[player.userID].Items)
            {
                sb.Append($"{kitem.shortname}\n");
                GiveItem(player, kitem.weapon ? BuildWeapon(kitem) : BuildItem(kitem), kitem.container);
            }
            LogItems(sb.ToString());
            sb.Clear();
            return true;
        }

        private object CanTrade(BasePlayer player)
        {
            return inDuel(player) ? "Трейд во время дуэли строго запрещен!!!" : null;
        }

        private object CanTeleport(BasePlayer player)
        {
            if (inDuel(player)) { return "Телепорт заблокирован, вы не можете \"убежать\" с дуэли!"; }
            return null;
        }

        void GiveItem(BasePlayer player, Item item, string container)
        {
            if (item == null) return;
            ItemContainer cont;
            switch (container)
            {
                case "wear":
                    cont = player.inventory.containerWear;
                    break;
                case "belt":
                    cont = player.inventory.containerBelt;
                    break;
                default:
                    cont = player.inventory.containerMain;
                    break;
            }
            item.MoveToContainer(cont);
        }

        Item BuildItem(SavedItem sItem)
        {
            if (sItem.amount < 1) sItem.amount = 1;
            Item item = ItemManager.CreateByItemID(sItem.itemid, sItem.amount, sItem.skinid);
            if (item.hasCondition) item.condition = sItem.condition;
            FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();
            if (flameThrower)
                flameThrower.ammo = sItem.flamefuel;
            return item;
        }

        Item BuildWeapon(SavedItem sItem)
        {
            Item item = ItemManager.CreateByItemID(sItem.itemid, 1, sItem.skinid);
            if (item.hasCondition)
                item.condition = sItem.condition;
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

        //API
        string[] namesOfWeapons;

        string[] GetNamesOfWeapons() => namesOfWeapons ?? (namesOfWeapons = weaponSets.Select(s => s.name).ToArray());

        static void LogDuels(string message)
        {
           // ConVar.Server.Log($"oxide/logs/duels{DateTime.Now:dd-MM-yyyy}.txt", message);
        }
        static void LogItems(string message)
        {
            //ConVar.Server.Log($"oxide/logs/duels_items{DateTime.Now:dd-MM-yyyy}.txt", message);
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

            //функция-шаблон для изображенияURL
            public static CuiElement createimgurl(string name, string parent, string img, string anchmin, string anchmax)
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
                            Url = img
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
