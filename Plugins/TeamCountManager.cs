// Reference: Oxide.Core.RustyCore
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Core.Libraries;
using LogType = Oxide.Core.Logging.LogType;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("TeamCountManager", "bazuka5801", "1.0.0")]
    class TeamCountManager : RustPlugin
    {
        [PluginReference] Plugin BomzhEvent;
        #region CONFIGURATION

        int maxPlayers;

        protected override void LoadDefaultConfig()
        {
            Config["MaxPlayers"] = maxPlayers = GetConfig("MaxPlayers", 3);
            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue)
            => Config[name] == null ? defaultValue : (T) Convert.ChangeType(Config[name], typeof(T));

        #endregion

        #region VARIABLES
        private RustyCore.RCore core = Interface.Oxide.GetLibrary<RustyCore.RCore>();


        readonly MethodInfo entitySnapshot = typeof(BasePlayer).GetMethod("SendEntitySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly FieldInfo codelockWhiteListField = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Instance | BindingFlags.NonPublic));

        HashSet<BasePlayer> players = new HashSet<BasePlayer>();
        HashSet<BasePlayer> moders = new HashSet<BasePlayer>();
        HashSet<BasePlayer> admins = new HashSet<BasePlayer>();
        readonly HashSet<string> currentRepetitionsIDs = new HashSet<string>();
        Dictionary<string, int> suspectsRepetitions = new Dictionary<string, int>();
        Dictionary<BasePlayer, int> spectatingPlayers = new Dictionary<BasePlayer, int>();
        Dictionary<ulong, Vector3> lastPositions = new Dictionary<ulong, Vector3>();
        PermissionService Permission;

        #endregion

        #region COMMANDS

        
        [ConsoleCommand("violation.codelock")]
        void cmdViolationCodelock(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;

            Dictionary<string, List<Vector3>> violations = new Dictionary<string, List<Vector3>>();

            var codelocks = BaseEntity.saveList.Select(p => p as CodeLock).Where(p => p != null).ToList();
            var msg = new StringBuilder();
            foreach (var codelock in codelocks)
            {
                List<ulong> whiteList = codelock.whitelistPlayers;
                whiteList.Sort();
                if (whiteList.Count > maxPlayers)
                {
                    var pos = codelock.parentEntity.Get(true).CenterPoint();

                    var playersKey = string.Join(" ", whiteList.Select(GetPlayerInfo).ToArray());

                    if (!violations.ContainsKey(playersKey))
                        violations[playersKey] = new List<Vector3>();

                    violations[playersKey].Add(pos);
                }
            }

            foreach (var violation in violations.OrderByDescending(p=>p.Value.Count))
            {
                msg.Append($"\n{violation.Key} ({violation.Value.Count} codelocks):\n{string.Join(", ",violation.Value.Select(p => $"(/tp {p.x} {p.y} {p.z})").ToArray())}\n");
            }
            Puts(msg.ToString());
        }

        [ChatCommand("teamspectate")]
        void cmdChatTeamTP(BasePlayer player, string command, string[] args)
        {
            if (!Permission.HasPermission(player.userID, permissionModer))
            {
                SendReply(player, "У вас нет нужных превилегий!");
                return;
            }
            if (!player.IsSpectating())
            {
                if (spectatingPlayers.Count == 0)
                {
                    SendReply(player, "Подозреваемые еще не обнаружены!");
                    return;
                }
                var target = spectatingPlayers.First().Key;
                spectatingPlayers.Remove(target);

                if (target.IsDead())
                {
                    SendReply(player, "Ошибка: игрок не найден или умер!");
                    return;
                }

                if (ReferenceEquals(target, player))
                {
                    SendReply(player, "Следить за собой нельзя!");
                    return;
                }

                if (target.IsSpectating())
                {
                    SendReply(player, "Подозреваемый наблюдает за кем-то");
                    return;
                }

                lastPositions[player.userID] = player.GetNetworkPosition();

                // Prep player for spectate mode
                var heldEntity = player.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                heldEntity?.SetHeld(false);

                player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
                // Put player in spectate mode
                player.gameObject.SetLayerRecursive(10);
                player.CancelInvoke("MetabolismUpdate");
                player.CancelInvoke("InventoryUpdate");
                player.ClearEntityQueue();
                entitySnapshot.Invoke(player, new object[] {target});
                player.gameObject.Identity();
                player.SetParent(target);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
                player.Command("camoffset 0,1.3,0");

                SendReply(player, "Слежка началась!");
            }
            else
            {
                SpectateFinish(player);
            }
        }

        void SpectateFinish(BasePlayer player)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            player.Command("camoffset", "0,1,0");
            player.SetParent(null);
            player.gameObject.SetLayerRecursive(17);
            player.metabolism.Reset();
            player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
            

            var heldEntity = player.GetActiveItem()?.GetHeldEntity() as HeldEntity;
            heldEntity?.SetHeld(true);

            // Teleport to original location after spectating
            if (lastPositions.ContainsKey(player.userID))
            {
                var lastPosition = lastPositions[player.userID];
                core.Teleport(player, new Vector3(lastPosition.x, lastPosition.y, lastPosition.z));
                lastPositions.Remove(player.userID);
            }

            player.StopSpectating();
            SendReply(player, "Слежка закончена!");
        }


        object CanAttack(BasePlayer player) => player.IsSpectating() ? (object) false : null;
        

        #endregion


        #region OXIDE HOOKS

        void Init()
        {
            LoadDefaultConfig();
        }

        void OnServerInitialized()
        {
            try
            {
                suspectsRepetitions = Interface.Oxide.DataFileSystem.GetFile("TeamSuspects").ReadObject<Dictionary<string, int>>() ??
                                      new Dictionary<string, int>();
            }
            catch
            {
                suspectsRepetitions = new Dictionary<string, int>();
            }
            Permission = new PermissionService(this);
            Permission.RegisterPermissions(new List<string>() { permissionModer });
            BasePlayer.activePlayerList.ForEach(p =>
            {
                if (p.IsAdmin) admins.Add(p);
                else if (Permission.HasPermission(p.userID, permissionModer)) moders.Add(p);
                players.Add(p);
            });
            BasePlayer.sleepingPlayerList.ForEach(p => players.Add(p));
            timer.Every(60, OnTimer);
        }

        [ChatCommand("cdfds")]
        void cmdChatCD(BasePlayer player, string command, string[] args)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            player.Command("camoffset", "0,1,0");
            player.SetParent(null);
            player.gameObject.SetLayerRecursive(17);
            player.metabolism.Reset();
            player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.GetFile("TeamSuspects").WriteObject(suspectsRepetitions);
        }

        void OnPlayerInit(BasePlayer player)
        {
            players.Add(player);
            if (player.IsAdmin) admins.Add(player);else if (Permission.HasPermission(player.userID, permissionModer)) moders.Add(player);
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.IsAdmin) admins.Remove(player); else if (Permission.HasPermission(player.userID, permissionModer)) moders.Remove(player);
        }
        #endregion

        #region CORE
        
        void SuspectCheck(BasePlayer player)
        {
            if (player == null) return;
            if (InDuel(player) || InRaid(player) || InEvent(player)) return;
            var playersAround = GetPlayersAround(player);
            if (playersAround == null) return;
            if (playersAround.Count < maxPlayers) return;
            if (playersAround.Count > 5) return;
            var suspects = SplitArray(playersAround).Select(list =>
            {
                list.Sort();
                return ListToString(list);
            }).ToList();
            
            foreach (string sortIds in suspects.Where(sortIds => !currentRepetitionsIDs.Contains(sortIds)).ToList())
            {
                currentRepetitionsIDs.Add(sortIds);

                if (!suspectsRepetitions.ContainsKey(sortIds))
                {
                    suspectsRepetitions.Add(sortIds, 0);
                }
                suspectsRepetitions[sortIds]++;

                var userList = string.Join(", ", ToUserNames(sortIds.Split()));
                LogToFile("suspects", $"{suspectsRepetitions[sortIds]}:{userList}", this);
                var randPlayer = playersAround.GetRandom();
                if (suspectsRepetitions[sortIds] >= 2)
                {
                    var msg = $"{suspectsRepetitions[sortIds]} минуты вместе: " +
                              string.Join(", ", playersAround.Select(p => p.displayName).ToArray());
                    Puts(msg);
                    Msg(msg);
                }
                if (suspectsRepetitions[sortIds] >= 5)
                {
                    var msg= $"Бегают вместе {suspectsRepetitions[sortIds]} минут: {userList}";
                    Msg(msg);
                    LogToFile("greater5min", msg , this);
                    if (!spectatingPlayers.ContainsKey(randPlayer))
                        spectatingPlayers[randPlayer] = 3;
                }
            }
        }
        private void OnTimer()
        {
            DateTime time = DateTime.UtcNow;
            ForEachPlayers(() => OnSuspectsCheckEnd(DateTime.UtcNow - time));
            
            foreach (var key in spectatingPlayers.Keys.ToList())
            {
                if (--spectatingPlayers[key] < 0)
                    spectatingPlayers.Remove(key);
            }
        }

        private void OnSuspectsCheckEnd(TimeSpan delay)
        {
            var message =
                $"Прошла проверка: обнаружено {currentRepetitionsIDs.Count} групп за {delay.TotalSeconds.ToString("F1")} секунд";
            if (suspectsRepetitions.Count > 0)
                for (var j = suspectsRepetitions.Keys.Count - 1; j >= 0; j--)
                    if (!currentRepetitionsIDs.Contains(suspectsRepetitions.Keys.ToList()[j]))
                    {
                        suspectsRepetitions.Remove(suspectsRepetitions.Keys.ToList()[j]);
                    }
            currentRepetitionsIDs.Clear();
            Msg(message);
            LogToFile("suspects", message, this);
        }

        private void ForEachPlayers(Action callback, int index = 0)
        {
            if (BasePlayer.activePlayerList.Count <= 0)
            {
                callback();
                return;
            }
            NextFrame(() =>
            {
                var player = BasePlayer.activePlayerList[index];
                SuspectCheck(player);

                if (index != BasePlayer.activePlayerList.Count - 1)
                    NextFrame(() => ForEachPlayers(callback, index + 1));
                else
                    callback();
            });
        }
        IEnumerator ForEachPlayers()
        {
            var players = BasePlayer.activePlayerList.ToList();
            foreach (var player in players)
            {
                SuspectCheck(player);
                yield return new WaitForSeconds(0.1f);
            }
            if (suspectsRepetitions.Count > 0)
                for (var j = suspectsRepetitions.Keys.Count - 1; j >= 0; j--)
                    if (!currentRepetitionsIDs.Contains(suspectsRepetitions.Keys.ToList()[j]))
                    {
                        suspectsRepetitions.Remove(suspectsRepetitions.Keys.ToList()[j]);
                    }
            LogToFile( "suspects", $"Прошла проверка: обнаружено {currentRepetitionsIDs.Count} групп", this);
            currentRepetitionsIDs.Clear();
        }

        bool InRadius(Vector3 vec1, Vector3 vec2)
        {
            return
                Vector3.Distance(vec1,vec2) <
                30;
        }

        List<BasePlayer> GetPlayersAround(BasePlayer player)
        {
            var start = DateTime.UtcNow;
            int ms;
            try
            {
                var suspects = Physics.OverlapSphere(player.transform.position, 30, LayerMask.GetMask("Player (Server)")).
                    Select(col => col.gameObject.ToBaseEntity() as BasePlayer).ToList();

                suspects.RemoveAll(basePlayer => basePlayer.IsSleeping());
                ms = (int)DateTime.UtcNow.Subtract(start).TotalMilliseconds;
                if (ms > 50)
                    Puts(nameof(GetPlayersAround)+" takes "+ms.ToString("####") + "ms");
                return suspects.Count >= maxPlayers + 1 ? suspects : null;
            }
            catch
            {
                // ignored
            }
            ms = (int)DateTime.UtcNow.Subtract(start).TotalMilliseconds;
            if (ms > 50)
                Puts(nameof(GetPlayersAround) + " takes " + ms.ToString("####") + "ms");
            return null;
        }


        string[] ToUserNames(string[] suspects) => suspects.Select(ToUserName).ToArray();

        string ToUserName(string uid)
        {
            var player = ToBasePlayer(ulong.Parse(uid));
            return player != null ? player.displayName : "[NOT FOUND]";
        }

        BasePlayer ToBasePlayer(ulong uid) => players.FirstOrDefault(p => p.userID == uid);

        #endregion

        #region COMMANDS

        [ChatCommand("tcm")]
        void cmdChatTCM(BasePlayer player, string command, string[] args)
        {
            if (Permission.HasPermission(player.userID, permissionModer) || player.IsAdmin)
            {
                if (args.Length == 1)
                {
                    var suspect = findPlayer(player, args[0]);
                    if (suspect == null) return;
                    var msg = new StringBuilder();
                    foreach (var suspects in suspectsRepetitions.Where(s => s.Key.Contains(suspect.UserIDString)))
                    {
                        msg.Append($"{suspects.Value} раз: {ToUserNames(suspects.Key.Split())}");
                    }
                    string message = msg.ToString();
                    if (message == "") { Msg("Не обнаружен!");}
                    Msg(message);
                }
            }
        }
        BasePlayer findPlayer(BasePlayer asker, string name)
        {
            var list =
                players.Where(
                    player => player.displayName.ToLower().Contains(name.ToLower()) && asker != player).ToList();
            if (list.Count == 0)
            {
                asker.ChatMessage("<color=#ff54008>Игрок не найден!</color>");
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
        #endregion

        #region MESSAGES

        string GetPlayerInfo(ulong userId)
        {
            var name = core.FindDisplayname(userId);
            return name.IsSteamId() ? $"[{name}]" : $"[{name}/{userId}]";
        }

        void Msg(string body)
        {
            var message = $"<size=14><color=orange>{body}</color></size>";
            foreach (var moder in moders)
                moder.ChatMessage(message);
            foreach (var admin in admins)
                admin.ChatMessage(message);
        }
        #endregion

        #region LIST EX

        string ListToString<T>(List<T> list)
        {
            return string.Join(" ", list.Select(uid => uid.ToString()).ToArray());
        }

        IEnumerable<IEnumerable<T>> GetKCombs<T>(IEnumerable<T> list, int length)
            where T : IComparable
        {
            if (length == 1)
                return list.Select(t => (IEnumerable<T>)new[] { t });
            return GetKCombs(list, length - 1)
                .SelectMany(t => list.Where(o => o.CompareTo(t.Last()) > 0),
                    (t1, t2) => t1.Concat(new T[] { t2 }));
        }

        private List<List<ulong>> SplitArray(List<BasePlayer> suspects)
        {
            var suspectslist = new List<IEnumerable<ulong>>();
            if (suspects.Count < maxPlayers + 1) return null;
            suspectslist.Add(suspects.Select(player => player.userID).ToList());
            for (int length = suspects.Count - 1; length >= maxPlayers + 1; length--)
            {
                var sus = GetKCombs(suspects.Select(player => player.userID).ToList(), length);
                suspectslist.AddRange(sus);
            }
            return suspectslist.Select(list => list.ToList()).ToList();
        }

        #endregion

        #region OTHER PLUGINS

        [PluginReference]
        Plugin Duels;

        [PluginReference]
        Plugin NoEscape;

        bool InEvent(BasePlayer player)
        {
            if (BomzhEvent == null) return false;
            try
            {
                var ret = BomzhEvent?.Call("InEvent", player);
                bool result = ret != null && (bool)ret;
                return result;
            }
            catch
            {
                return false;
            }
        }

        bool InDuel(BasePlayer player)
        {
            if (Duels == null) return false;
            try
            {
                var ret = Duels?.Call("inDuel", player);
                bool result = ret != null && (bool)ret;
                return result;
            }
            catch
            {
                return false;
            }
        }

        bool InRaid(BasePlayer player)
        {
            if (NoEscape == null) return false;
            try
            {
                double res = (double)NoEscape.Call("ApiGetTime", player.userID);
                return res > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region PERMISSIONS

        private string permissionModer = "moder";
        public class PermissionService
        {
            private static readonly Permission permission = Interface.GetMod().GetLibrary<Permission>();

            private readonly Plugin owner;

            public PermissionService(Plugin owner)
            {
                this.owner = owner;
            }

            public bool HasPermission(ulong uid, string permissionName)
            {
                if (string.IsNullOrEmpty(permissionName))
                    return false;
                permissionName = $"{owner.Name.ToLower()}.{permissionName.ToLower()}";
                return permission.UserHasPermission(uid.ToString(), permissionName);
            }

            public void RegisterPermissions(List<string> permissions)
            {
                if (owner == null) throw new ArgumentNullException("owner");
                if (permissions == null) throw new ArgumentNullException("commands");
                permissions = permissions.Select(p => $"{owner.Name.ToLower()}.{p.ToLower()}").ToList();

                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }

        #endregion

        #region JSON CONVERTERS
        UnityVector3Converter converter = new UnityVector3Converter();

        public class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]),
                        Convert.ToSingle(values[2]));
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
