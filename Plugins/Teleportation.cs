// Reference: Oxide.Core.RustyCore

using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Teleportation", "bazuka5801", "1.0.0")]
    class Teleportation : RustPlugin
    {
        #region CLASSES

        class TP
        {
            public BasePlayer Player;
            public BasePlayer Player2;
            public Vector3 pos;
            public int seconds;

            public TP(BasePlayer player, Vector3 pos, int seconds, BasePlayer player2 =null)
            {
                this.Player = player;
                this.pos = pos;
                this.seconds = seconds;
                this.Player2 = player2;
            }
        }

        #endregion

        #region CONFIGURATION

        int homelimitDefault;
        Dictionary<string, int> homelimitPerms;
        int tpkdDefault;
        Dictionary<string, int> tpkdPerms;
        int tpkdhomeDefault;
        Dictionary<string, int> tpkdhomePerms;
        int teleportSecsDefault;
        int resetPendingTime;
        bool restrictCupboard;
        bool homecupboard;
        Dictionary<string, int> teleportSecsPerms;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Разрешить телепортироваться домой в зоне действия чужого шкафа", out homecupboard, true);
            Config.GetVariable("Запретить принимать запрос в зоне действия чужого шкафа", out restrictCupboard, true);
            Config.GetVariable("Время на ответ на запроса телепортации (в секундах)", out resetPendingTime, 15);
            Config.GetVariable("Кол-во местоположений", out homelimitDefault, 3);
            Config["Кол-во местоположений (Привилегии)"] =
                homelimitPerms =
                    GetConfig("Кол-во местоположений (Привилегии)",
                            new Dictionary<string, object>() {{"teleportation.vip", 5}})
                        .ToDictionary(p => p.Key, p => Convert.ToInt32(p.Value));
            PermissionService.RegisterPermissions(this, homelimitPerms.Keys.ToList());

            Config.GetVariable("Время телепорта", out teleportSecsDefault, 15);
            Config["Время телепорта (Привилегии)"] =
                teleportSecsPerms =
                    GetConfig("Время телепорта (Привилегии)",
                            new Dictionary<string, object>() {{"teleportation.vip", 10}})
                        .ToDictionary(p => p.Key, p => Convert.ToInt32(p.Value));
            PermissionService.RegisterPermissions(this, teleportSecsPerms.Keys.ToList());

            Config.GetVariable("KD телепорта", out tpkdDefault, 300);
            Config["KD телепорта (Привилегии)"] =
                tpkdPerms =
                    GetConfig("KD телепорта (Привилегии)",
                            new Dictionary<string, object>() { { "teleportation.vip", 150 } })
                        .ToDictionary(p => p.Key, p => Convert.ToInt32(p.Value));
            PermissionService.RegisterPermissions(this, tpkdPerms.Keys.ToList());

            Config.GetVariable("KD телепорта HOME", out tpkdhomeDefault, 300);
            Config["KD телепорта HOME (Привилегии)"] =
                tpkdhomePerms =
                    GetConfig("KD телепорта HOME (Привилегии)",
                            new Dictionary<string, object>() { { "teleportation.vip", 150 } })
                        .ToDictionary(p => p.Key, p => Convert.ToInt32(p.Value));
            PermissionService.RegisterPermissions(this, tpkdhomePerms.Keys.ToList());

            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue)
            => Config[name] == null ? defaultValue : (T) Convert.ChangeType(Config[name], typeof(T));

        #endregion

        #region FIELDS

        private FieldInfo SleepingBagUnlockTimeField = typeof(SleepingBag).GetField("unlockTime",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly int groundLayer = LayerMask.GetMask("Terrain", "World");
        private readonly int buildingMask = Rust.Layers.Server.Buildings;

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        Dictionary<ulong, Dictionary<string, Vector3>> homes;
        Dictionary<ulong,int> cooldownsTP = new Dictionary<ulong, int>();
        Dictionary<ulong, int> cooldownsHOME = new Dictionary<ulong, int>();
        List<TP> tpQueue = new List<TP>();
        List<TP> pendings = new List<TP>();
        List<ulong> sethomeBlock  = new List<ulong>();
        #endregion

        #region COMMANDS

        [ChatCommand("sethome")]
        void cmdChatSetHome(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                SendReply(player, Messages["sethomeArgsError"]);
                return;
            }
            if (player.IsWounded() || player.metabolism.bleeding.value > 0)
            {
                SendReply(player, Messages["woundedAction"]);
                return;
            }

            if (sethomeBlock.Contains(player.userID))
            {
                SendReply(player, Messages["sethomeBlock"]);
                return;
            }
            var name = args[0];
            SetHome(player, name);
        }

        [ChatCommand("removehome")]
        void cmdChatRemoveHome(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                SendReply(player, Messages["removehomeArgsError"]);
                return;
            }
            if (!homes.ContainsKey(player.userID))
            {
                SendReply(player, Messages["homesmissing"]);
                return;
            }
            var name = args[0];
            var playerHomes = homes[player.userID];
            if (!playerHomes.ContainsKey(name))
            {
                SendReply(player, Messages["homenotexist"]);
                return;
            }
            foreach (var sleepingBag in SleepingBag.FindForPlayer(player.userID, true))
            {
                if (Vector3.Distance(sleepingBag.transform.position, playerHomes[name]) < 1)
                {
                    sleepingBag.Kill();
                    break;
                }
            }
            playerHomes.Remove(name);
            SendReply(player, Messages["removehomesuccess"]);
        }

        [ConsoleCommand("home")]
        void cmdHome(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || arg.Args.Length > 0) return;
            cmdChatHome(player, "", new[] {arg.Args[0]});
        }
        

        [ChatCommand("home")]
        void cmdChatHome(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                SendReply(player, Messages["homeArgsError"]);
                return;
            }
            if (player.IsWounded() || player.metabolism.bleeding.value > 0)
            {
                SendReply(player, Messages["woundedAction"]);
                return;
            }
            int seconds;
            if (cooldownsHOME.TryGetValue(player.userID, out seconds) && seconds > 0)
            {
                SendReply(player, string.Format(Messages["tpkd"], core.TimeToString(seconds)));
                return;
            }
            if (!homes.ContainsKey(player.userID))
            {
                SendReply(player, Messages["homesmissing"]);
                return;
            }
            var name = args[0];
            var playerHomes = homes[player.userID];
            if (!playerHomes.ContainsKey(name))
            {
                SendReply(player, Messages["homenotexist"]);
                return;
            }
            var time = GetTeleportTime(player.userID);
            var pos = playerHomes[name];
            SleepingBag bag = GetSleepingBag(name, pos);
            if (bag == null)
            {
                SendReply(player,Messages["sleepingbagmissing"]);
                playerHomes.Remove(name);
                return;
            }

            if (player.metabolism.temperature.value < 0)
            {
                SendReply(player, Messages["coldplayer"]);
                return;
            }

            var ret = Interface.Call("CanTeleport", player) as string;
            if (ret != null)
            {
                SendReply(player, ret);
                return;
            }
            var lastTp = tpQueue.Find(p => p.Player == player);
            if (lastTp != null)
            {
                tpQueue.Remove(lastTp);
            }
            tpQueue.Add(new TP(player, pos,time));
            SendReply(player,String.Format(Messages["homequeue"],name,core.TimeToString(time)));
        }

        [ChatCommand("tpr")]
        void cmdChatTpr(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                SendReply(player, Messages["tprArgsError"]);
                return;
            }
            if (player.IsWounded() || player.metabolism.bleeding.value > 0)
            {
                SendReply(player, Messages["woundedAction"]);
                return;
            }
            var name = args[0];
            var target = core.FindBasePlayer(name);
            if (target == null)
            {
                SendReply(player, Messages["playermissing"]);
                return;
            }
            int seconds = 0;
            if (restrictCupboard && tpQueue.Any(p => p.Player == player && p.Player2 != null) &&
                player.GetBuildingPrivilege() != null &&
                !player.GetBuildingPrivilege().authorizedPlayers.Select(p => p.userid).Contains(player.userID))
            {
                SendReply(player,Messages["tpcupboard"]);
                return;
            }
            if (cooldownsTP.TryGetValue(player.userID, out seconds) && seconds > 0)
            {
                SendReply(player,string.Format(Messages["tpkd"], core.TimeToString(seconds)));
                return;
            }

            if (player.metabolism.temperature.value < 0)
            {
                SendReply(player, Messages["coldplayer"]);
                return;
            }

            if (tpQueue.Any(p => p.Player == player) ||
                pendings.Any(p => p.Player2 == player))
            {
                SendReply(player, Messages["tpError"]);
                return;
            }

            var ret = Interface.Call("CanTeleport", player) as string;
            if (ret != null)
            {
                SendReply(player, ret);
                return;
            }

            SendReply(player,string.Format(Messages["tprrequestsuccess"], target.displayName));
            SendReply(target,string.Format(Messages["tprpending"], player.displayName));
            pendings.Add(new TP(target,Vector3.zero, 15, player));
        }

        [ChatCommand("tpa")]
        void cmdChatTpa(BasePlayer player, string command, string[] args)
        {
            var tp = pendings.Find(p => p.Player == player);
            BasePlayer pendingPlayer = tp?.Player2;
            if (pendingPlayer == null)
            {
                SendReply(player, Messages["tpanotexist"]);
                return;
            }

            if (player.metabolism.temperature.value < 0)
            {
                SendReply(player, Messages["coldplayer"]);
                return;
            }
            if (player.IsWounded() || player.metabolism.bleeding.value > 0)
            {
                SendReply(player, Messages["woundedAction"]);
                return;
            }
            if (restrictCupboard && player.GetBuildingPrivilege() != null &&
                !player.GetBuildingPrivilege().authorizedPlayers.Select(p => p.userid).Contains(player.userID))
            {
                SendReply(player, Messages["tpacupboard"]);return;
            }


            var ret = Interface.Call("CanTeleport", player) as string;
            if (ret != null)
            {
                SendReply(player,ret);
                return;
            }

            var time = GetTeleportTime(pendingPlayer.userID);
            pendings.Remove(tp);
            
            var lastTp = tpQueue.Find(p => p.Player == pendingPlayer);
            if (lastTp != null)
            {
                tpQueue.Remove(lastTp);
            }

            tpQueue.Add(new TP(pendingPlayer,player.GetNetworkPosition(),time,player));
            SendReply(pendingPlayer, string.Format(Messages["tpqueue"], player.displayName, core.TimeToString(time)));
            SendReply(player,String.Format(Messages["tpasuccess"],pendingPlayer.displayName,core.TimeToString(time)));
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            pendings.RemoveAll(p => p.Player == player || p.Player2 == player);
            tpQueue.RemoveAll(p => p.Player == player || p.Player2 == player);
        }

        [ChatCommand("tpc")]
        void cmdChatTpc(BasePlayer player, string command, string[] args)
        {
            var tp = pendings.Find(p => p.Player == player);
            BasePlayer target = tp?.Player2;
            if (target != null)
            {
                pendings.Remove(tp);
                SendReply(player, Messages["tpc"]);
                SendReply(target, string.Format(Messages["tpctarget"], player.displayName));
                return;
            }
            if (player.IsWounded() || player.metabolism.bleeding.value > 0)
            {
                SendReply(player, Messages["woundedAction"]);
                return;
            }
            if (player.metabolism.temperature.value < 0)
            {
                SendReply(player, Messages["coldplayer"]);
                return;
            }
            foreach (var pend in pendings)
            {
                if (pend.Player2 == player)
                {
                    SendReply(player, Messages["tpc"]);
                    SendReply(pend.Player, string.Format(Messages["tpctarget"], player.displayName));
                    pendings.Remove(pend);
                    return;
                }
            }
            foreach (var tpQ in tpQueue)
            {
                if (tpQ.Player2 != null && tpQ.Player2 == player)
                {
                    SendReply(player, Messages["tpc"]);
                    SendReply(tpQ.Player, string.Format(Messages["tpctarget"], player.displayName));
                    tpQueue.Remove(tpQ);
                    return;
                }
                if (tpQ.Player == player)
                {
                    SendReply(player, Messages["tpc"]);
                    if (tpQ.Player2 != null)
                        SendReply(tpQ.Player2, string.Format(Messages["tpctarget"], player.displayName));
                    tpQueue.Remove(tpQ);
                    return;
                }
            }
        }

        #region ADMIN COMMANDS

        [ChatCommand("tp")]
        void cmdTP(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            switch (args.Length)
            {
                case 1:
                    string name = args[0];
                    BasePlayer target = core.FindBasePlayer(name);
                    if (target == null)
                    {
                        SendReply(player,Messages["playermissing"]);
                        return;
                    }
                    core.Teleport(player,target);
                    break;
                case 2:
                    string name1 = args[0];
                    string name2 = args[1];
                    BasePlayer target1 = core.FindBasePlayer(name1);
                    BasePlayer target2 = core.FindBasePlayer(name2);
                    if (target1 == null || target2 == null)
                    {
                        SendReply(player, Messages["playermissing"]);
                        return;
                    }
                    core.Teleport(target1, target2);
                    break;
                case 3:
                    float x = float.Parse(args[0]);
                    float y = float.Parse(args[1]);
                    float z = float.Parse(args[2]);
                    core.Teleport(player,x,y,z);
                    break;
            }
        }
        #endregion

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadData();
            timer.Every(1f, TeleportationTimerHandle);
        }

        void Unload()
        {
            SaveData();
        }

        void OnEntityBuilt( Planner planner, GameObject gameobject )
        {
            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;
            if (gameobject.name.Contains("foundation"))
            {
                var pos = gameobject.transform.position;
                foreach (var pending in tpQueue)
                {
                    if (Vector3.Distance(pending.pos, pos ) < 3)
                    {
                        entity.Kill();
                        SendReply( planner.GetOwnerPlayer(), "Нельзя, тут телепортируется игрок!");
                        return;
                    }
                }
            }
        }

        #endregion

        #region CORE

        void TeleportationTimerHandle()
        {
            List<ulong> tpkdToRemove = new List<ulong>();
            foreach (var uid in cooldownsTP.Keys.ToList())
            {
                if (--cooldownsTP[uid] <= 0)
                {
                    tpkdToRemove.Add(uid);
                }
            }
            tpkdToRemove.ForEach(p=>cooldownsTP.Remove(p));

            List<ulong> tpkdHomeToRemove = new List<ulong>();
            foreach (var uid in cooldownsHOME.Keys.ToList())
            {
                if (--cooldownsHOME[uid] <= 0)
                {
                    tpkdHomeToRemove.Add(uid);
                }
            }
            tpkdHomeToRemove.ForEach(p => cooldownsHOME.Remove(p));

            for (int i = pendings.Count - 1; i >= 0; i--)
            {
                var pend = pendings[i];
                if (pend.Player2!= null && pend.Player2.IsConnected && pend.Player2.IsWounded())
                {
                    SendReply(pend.Player2, Messages["tpwounded"]);
                    pendings.RemoveAt(i);
                    continue;
                }
                if (--pend.seconds <= 0)
                {
                    pendings.RemoveAt(i);
                    if (pend.Player2 != null && pend.Player2.IsConnected) SendReply( pend.Player2, Messages["tppendingcanceled"]);
                    if (pend.Player != null && pend.Player.IsConnected) SendReply(pend.Player, Messages["tpacanceled"]);
                }
            }
            for (int i = tpQueue.Count - 1; i >= 0; i--)
            {
                var tp = tpQueue[i];
                if (tp.Player != null && tp.Player.IsConnected && (tp.Player.metabolism.bleeding.value > 0 || tp.Player.IsWounded()))
                {
                    SendReply(tp.Player, Messages["tpwounded"]);
                    if (tp.Player2 != null && tp.Player.IsConnected)
                        SendReply(tp.Player2, Messages["tpWoundedTarget"]);
                    tpQueue.RemoveAt(i);
                    continue;
                }
                if (--tp.seconds <= 0)
                {
                    tpQueue.RemoveAt(i);
                    var ret = Interface.CallHook("CanTeleport", tp.Player) as string;
                    if (ret != null)
                    {
                        SendReply(tp.Player,ret);
                        continue;
                    }
                    core.Teleport(tp.Player, tp.pos);
                    if (tp.Player2 != null && tp.Player != null && tp.Player.IsConnected)
                    {
                        var seconds = GetKD(tp.Player.userID);
                        CooldownTP(tp.Player,seconds);
                        cooldownsTP[tp.Player.userID] = seconds;
                        SendReply(tp.Player,string.Format(Messages["tpplayersuccess"], tp.Player2.displayName));
                    }
                    else
                    {
                        if (tp.Player != null && tp.Player2 != null && tp.Player.IsConnected && tp.Player2.IsConnected)
                        {
                            var seconds = GetKDHome(tp.Player.userID);
                            CooldownHome(tp.Player, seconds);
                            cooldownsHOME[tp.Player.userID] = seconds;
                            SendReply(tp.Player, Messages["tphomesuccess"]);
                        }
                    }
                    NextTick(()=>Interface.CallHook("OnPlayerTeleported", tp.Player));
                }
            }
        }

        void SetHome(BasePlayer player, string name)
        {
            var uid = player.userID;
            var pos = player.GetNetworkPosition();
            var foundationmissing = GetFoundation(pos);
            
            if (foundationmissing != null)
            {
                SendReply(player, foundationmissing.ToString());
                return;
            }

            if (player.GetBuildingPrivilege() != null &&
                !player.GetBuildingPrivilege().authorizedPlayers.Select(p => p.userid).Contains(player.userID))
            {
                SendReply(player, Messages["sethomecupboard"]);
                return;
            }
            Dictionary<string,Vector3> playerHomes;
            if (!homes.TryGetValue(uid, out playerHomes))
                playerHomes = (homes[uid] = new Dictionary<string, Vector3>());
            if (GetHomeLimit(uid) == playerHomes.Count)
            {
                SendReply(player, Messages["maxhomes"]);
                return;
            }
            
            if (playerHomes.ContainsKey(name))
            {
                SendReply(player, Messages["homeexist"]);
                return;
            }

            playerHomes.Add(name, pos);
            CreateSleepingBag(player, pos, name);
            SendReply(player, Messages["homesucces"]);
            sethomeBlock.Add(player.userID);
            timer.Once(10f, () => sethomeBlock.Remove(player.userID));
        }

        

        int GetKDHome(ulong uid)
        {
            int min = tpkdhomeDefault;
            foreach (var privilege in tpkdhomePerms)
                if (PermissionService.HasPermission(uid, privilege.Key))
                    min = Mathf.Min(min, privilege.Value);
            return min;
        }
        int GetKD(ulong uid)
        {
            int min = tpkdDefault;
            foreach (var privilege in tpkdPerms)
                if (PermissionService.HasPermission(uid, privilege.Key))
                    min = Mathf.Min(min, privilege.Value);
            return min;
        }
        int GetHomeLimit(ulong uid)
        {
            int max = homelimitDefault;
            foreach (var privilege in homelimitPerms)
                if (PermissionService.HasPermission(uid, privilege.Key))
                    max = Mathf.Max(max, privilege.Value);
            return max;
        }
        int GetTeleportTime(ulong uid)
        {
            int min = teleportSecsDefault;
            foreach (var privilege in teleportSecsPerms)
                if (PermissionService.HasPermission(uid, privilege.Key))
                    min = Mathf.Min(min, privilege.Value);
            return min;
        }
        object GetFoundation(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Ray(pos, Vector3.down), out hit, 0.1f))
            {
                var entity = hit.GetEntity();
                if (entity != null && entity.ShortPrefabName.Contains("foundation"))
                    return null;
            }
            return Messages["foundationmissing"];
        }
        
        SleepingBag GetSleepingBag(string name, Vector3 pos)
        {
            List<SleepingBag> sleepingBags = new List<SleepingBag>();
            Vis.Components(pos, .1f, sleepingBags);
            return sleepingBags.Count > 0 ? sleepingBags[0] : null;
        }

        void CreateSleepingBag(BasePlayer player, Vector3 pos, string name)
        {
            SleepingBag sleepingBag =
                GameManager.server.CreateEntity("assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab", pos,
                    Quaternion.identity) as SleepingBag;
            if (sleepingBag == null) return;
            sleepingBag.skinID = 893086724;
            sleepingBag.deployerUserID = player.userID;
            sleepingBag.niceName = name;
            sleepingBag.OwnerID = player.userID;
            SleepingBagUnlockTimeField.SetValue(sleepingBag, Time.realtimeSinceStartup + 300f);
            sleepingBag.Spawn();
        }

        #endregion

        #region API

        Dictionary<string, Vector3> GetHomes(ulong uid)
        {
            Dictionary<string, Vector3> positions;
            if (!homes.TryGetValue(uid, out positions))
                return null;
            return positions.ToDictionary(p=>p.Key, p=>p.Value);
        }

        #endregion

        #region COOLDOWN SYSTEM

        [PluginReference] private Plugin CooldownSystem;

        void CooldownTP(BasePlayer player, int seconds)
        {
            CooldownSystem?.Call("SetCooldown", player, "tp", seconds);
        }
        void CooldownHome(BasePlayer player, int seconds)
        {
            CooldownSystem?.Call("SetCooldown", player, "home", seconds);
        }

        #endregion

        #region DATA

        DynamicConfigFile homesFile = Interface.Oxide.DataFileSystem.GetFile("Teleportation/Homes");

        void OnServerSave()
        {
            SaveData();
        }

        void LoadData()
        {
            homesFile.Settings.Converters.Add(converter);
            homes = homesFile.ReadObject<Dictionary<ulong, Dictionary<string, Vector3>>>();
        }

        void SaveData()
        {
            homesFile.WriteObject(homes);
        }

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"foundationmissing", "Фундамент не найден!" },
            {"maxhomes", "У вас максимальное кол-во местоположений!" },
            {"homeexist", "Такое местоположение уже существует!" },
            { "homesucces", "Местоположение успешно установлено!" },
            { "sethomeArgsError", "Для установки местоположения используйте /sethome ИМЯ" },
            {"homeArgsError", "Для телепортации на местоположение используйте /home ИМЯ" },
            {"tpError", "Запрещено! вы в очереди на телепортацию" },
            {"homenotexist", "Местоположение с таким названием не найдено!" },
            {"homequeue", "Телепортация на {0} будет через {1}" },
            {"tpwounded", "Вы получили ранение! Телепортация отменена!" },
            {"tphomesuccess", "Вы телепортированы домой!" },
            {"homesmissing", "У вас нет доступных местоположений!" },
            {"removehomeArgsError", "Для удаления местоположения используйте /removehome ИМЯ" },
            {"removehomesuccess", "Местоположение успешно удалено" },
            {"sleepingbagmissing", "Спальный мешок не найден, местоположение удалено!" },
            {"tprArgsError", "Для отправки запроса на телепортация используйте /tpr НИК" },
            {"playermissing", "Игрок не найден" },
            { "tprrequestsuccess", "Запрос {0} успешно отправлен" },
            { "tprpending", "{0} отправил вам запрос на телепортацию\nЧтобы принять используйте /tpa\nЧтобы отказаться используйте /tpc" },
            { "tpanotexist", "У вас нет активных запросов на телепортацию!" },
            { "tpqueue", "{0} принял ваш запрос на телепортацию\nВы будете телепортированы через {1}" },
            { "tpc", "Телепортация успешно отменена!" },
            { "tpctarget", "{0} отменил телепортацию!" },
            { "tpplayersuccess", "Вы успешно телепортировались к {0}" },
            { "tpasuccess", "Вы приняли запрос телепортации от {0}\nОн будет телепортирован через {1}" },
            { "tppendingcanceled", "Запрос телепортации отменён" },
            {"tpcupboard", "Телепортация в зоне действия чужого шкафа запрещена!" },
            {"tpacupboard", "Принятие телепортации в зоне действия чужого шкафа запрещена!" },
            {"sethomecupboard", "Установка местоположения в зоне действия чужого шкафа запрещена!" },
            {"tpacanceled", "Вы не ответили на запрос." },
            { "tpkd", "Телепортация на перезарядке!\nОсталось {0}" },
            { "tpWoundedTarget", "Игрок ранен. Телепортация отменена!" },
            { "woundedAction", "Вы ранены!" },
            { "coldplayer", "Вам холодно!" },
            { "sethomeBlock", "Нельзя использовать /sethome слишком часто, попробуйте позже!" }
        };

        #endregion

        #region VECTOR CONVERTER

        static UnityVector3Converter converter = new UnityVector3Converter();
        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }

        #endregion
        
    }
}
