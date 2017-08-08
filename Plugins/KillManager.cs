// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using Oxide.Core.Database;
using RustyCore.ConsoleTable;
using UnityEngine;
using Newtonsoft.Json.Serialization;

namespace Oxide.Plugins
{
    [Info("KillManager", "bazuka5801", "1.0.0")]
    class KillManager : RustPlugin
    {
        #region CLASSES

        class VictimData
        {
            public string UserId;
            public string Displayname;

            public VictimData()
            {
            }
            
            public VictimData(BasePlayer player)
            {
                UserId = player.UserIDString;
                Displayname = player.displayName;
            }

            public override string ToString()
            {
                return $"{UserId}/{Displayname}";
            }
        }

        class KillData
        {
            private VictimData victim;
            private float distance;
            private string weapon;
            private string time;
            private string bone;

            public string Victim { get { return victim.ToString(); } }
            public string Distance { get { return distance.ToString("F1"); } }
            public string Weapon { get { return weapon; } }
            public string Time { get { return time; } }
            public string Bone { get { return bone; } }

            public KillData()
            {
            }

            private KillData(VictimData victim, float distance, string weapon, string time, string bone)
            {
                this.victim = victim;
                this.distance = distance;
                this.weapon = weapon;
                this.bone = bone;
                this.time = time;
            }

            public static KillData Create(BasePlayer killer, BasePlayer victim, HitInfo info)
            {
                var weapon = info.WeaponPrefab?.ShortPrefabName;
                if (weapon == null) return null;
                if (info.IsNaNOrInfinity()) return null;
                var distance = Vector3.Distance(info.PointStart, info.PointEnd);
                var bone = info.boneName;
                if (bonesTranslated.ContainsKey(bone))
                    bone = bonesTranslated[bone];
                return new KillData(new VictimData(victim), distance, weapon,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), bone);
            }

            #region Bones Localization

            static Dictionary<string, string> bonesTranslated = new Dictionary<string, string>()
            {
                {"body", "в тело"},
                {"chest", "в грудь"},
                {"groin", "в пах"},
                {"head", "в голову"},
                {"hip", "в таз"},
                {"jaw", "в челюсть"},
                {"left arm", "в руку"},
                {"left eye", "в глаз"},
                {"left foot", "в ногу"},
                {"left forearm", "в предплечье"},
                {"left hand", "в руку"},
                {"left knee", "в колено"},
                {"left ring finger", "в палец"},
                {"left shoulder", "в плечо"},
                {"left thumb", "в палец"},
                {"left toe", "в палец"},
                {"left wrist", "в запястье"},
                {"lower spine", "в хребет"},
                {"neck", "в шею"},
                {"pelvis", "в таз"},
                {"right arm", "в руку"},
                {"right eye", "в глаз"},
                {"right foot", "в ступню"},
                {"right forearm", "в предплечье"},
                {"right hand", "в руку"},
                {"right knee", "в колено"},
                {"right ring finger", "в палец"},
                {"right shoulder", "в плечо"},
                {"right thumb", "в палец"},
                {"right toe", "в палец"},
                {"right wrist", "в запястье"},
                {"stomach", "в живот"}
            };

            #endregion
        }

        #endregion

        #region FIELDS

        readonly RCore core = Interface.Oxide.GetLibrary<RCore>();

        Dictionary<ulong, List<KillData>> kills;

        #endregion
        

        #region COMMANDS


        [ConsoleCommand("killplayer")]
        void cmdKill(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;

            var name = arg.Args[0];
            var player = core.FindBasePlayer(name);
            if (!player)
            {
                SendReply(arg, "Игрок не найден");
                return;
            }

            player.Kill();
            SendReply(arg, player.displayName + " убит!");
        }
        

        [ConsoleCommand("killmanager.player")]
        void cmdStatPlayer(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !arg.IsAdmin) return;

            var userId = arg.GetULong(0);
            if (!userId.IsSteamId())
            {
                SendReply(arg, "Invalid UserId");
            }

            var mod = arg.GetInt(1);
            List<KillData> killdata;
            if (!kills.TryGetValue(userId, out killdata))
            {
                SendReply(arg, "User doesn't have kills");
                return;
            }

            switch (mod)
            {
                case 0:
                    killdata = killdata.OrderByDescending(p => p.Time).ToList();
                    break;
                case 1:
                    killdata = killdata.OrderByDescending(p => p.Distance).ToList();
                    break;
                case 2:
                    killdata = killdata.OrderBy(p => p.Weapon).ToList();
                    break;
            }
            var viewData = killdata.Where(p => float.Parse(p.Distance) >= 10).ToList();
            var msg = new StringBuilder("\n");
            msg.Append($"Максимальная дистанция: {viewData.Max(p => float.Parse(p.Distance))}\n");
            var bones = new Dictionary<string, int>();
            foreach (var k in viewData.Select(p=>p.Bone).ToList())
            {
                if (!bones.ContainsKey(k))
                {
                    bones.Add(k, viewData.Count(p => p.Bone == k));
                }
            }
            msg.Append($"Любимая часть тела: {bones.OrderByDescending(p=>p.Value).ToList()[0].Key}\n");
            msg.Append(ConsoleTable.From(viewData).ToMarkDownString());
            Puts(msg.ToString());
        }

        #endregion

        #region OXIDE HOOKS

        void Loaded() => LoadData();
        
        void Unload() => SaveData();

        void CanBeWounded(BasePlayer victim, HitInfo info)
        {
            OnWoundedOrDeath(victim,info);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var victim = entity as BasePlayer;
            OnWoundedOrDeath(victim,info);
        }
        
        #endregion

        #region CORE

        void OnWoundedOrDeath(BasePlayer victim, HitInfo info)
        {
            if (info == null || victim == null) return;

            if (info.Initiator is BaseNPC) return;                                            // if animals are off and the initiator is an animal, return

            var killer = info.Initiator as BasePlayer;
            if (killer == null || killer == victim) return;

            var kill = KillData.Create(killer, victim, info);
            if (kill == null) return;

            List<KillData> killdata;
            if (!kills.TryGetValue(killer.userID, out killdata))
                killdata = kills[killer.userID] = new List<KillData>();
            if (killdata.Count > 0)
            {
                var last = killdata.Last();
                if (last.Bone == kill.Bone && last.Distance == kill.Distance && last.Victim == kill.Victim)
                    return;

            }
            killdata.Add(kill);

        }

        #endregion

        #region DATA

        DynamicConfigFile killsFile = Interface.Oxide.DataFileSystem.GetFile("KillManager");

        void LoadData()
        {
            killsFile.Settings.ContractResolver = new PrivateContractResolver();

            kills = killsFile.ReadObject<Dictionary<ulong, List<KillData>>>() ?? new Dictionary<ulong, List<KillData>>();
        }

        void OnServerSave()=> SaveData();

        void SaveData() => killsFile.WriteObject(kills);

        class PrivateContractResolver : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                MemberInfo[] fields = objectType.GetFields(flags);
                List<MemberInfo> list = fields.ToList();
                return list.Except(objectType.GetProperties(flags).Where(propInfo => propInfo.CanWrite).Cast<MemberInfo>()).ToList();
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var properties = base.CreateProperties(type, MemberSerialization.Fields);
                return properties;
            }
        }

        #endregion
    }
}
