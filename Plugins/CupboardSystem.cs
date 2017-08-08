// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.Specialized;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Logger = RustyCore.Utils.Logger;
using Rust;

namespace Oxide.Plugins
{
    [Info("CupboardSystem", "bazuka5801", "1.0.0")]
    class CupboardSystem : RustPlugin
    {
        #region CLASSES

        class CupboardTrigger : MonoBehaviour
        {
            private static int PlayerMask = LayerMask.GetMask("Player (Server)");
            public BuildingPrivlidge privlidgeEntity;
            public List<BasePlayer> owners = new List<BasePlayer>();

            public void Init(BuildingPrivlidge privlidge)
            {
                this.privlidgeEntity = privlidge;
            }

            void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "CupboardSystemTrigger";

                var sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = instance.batteryUIRadius;
                sphere.isTrigger = true;

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            void OnTriggerEnter(Collider col)
            {
                if (!InterestedInObject(col.gameObject)) return;
                var player = col.gameObject.ToBaseEntity() as BasePlayer;
                if (player == null) return;
                if (!instance.IsOwner(player,privlidgeEntity))return;

                owners.Add(player);
                instance.OnPlayerEnterTrigger(player, privlidgeEntity);
            }
            
            void OnTriggerExit(Collider col)
            {
                if (!InterestedInObject(col.gameObject)) return;
                var player = col.gameObject.ToBaseEntity() as BasePlayer;
                if (player == null) return;
                if (!instance.IsOwner(player, privlidgeEntity)) return;

                instance.OnPlayerExitTrigger(player, privlidgeEntity);
                owners.Remove(player);
            }
            

            bool InterestedInObject(GameObject obj)
            {
                int num = 1 << (obj.layer & 31);
                return (PlayerMask & num) == num;
            }
        }

        class CupboardBox : MonoBehaviour
        {
            LootableCorpse corpse;
            ItemContainer container;

            BasePlayer player;
            public uint cupboardId;
            public void Init(BasePlayer player, uint cupboardId)
            {
                this.player = player;
                this.cupboardId = cupboardId;
            }

            void Awake()
            {
                corpse = GetComponent<LootableCorpse>();
                container = corpse.containers[0];

                container.onItemAddedRemoved += (item, insert) =>
                {
                    if (!insert)return;
                    var batteryCount = instance.batteries[cupboardId];
                    if (batteryCount >= instance.maxBatteryCount) return;
                    int maxAccept = Convert.ToInt32(instance.maxBatteryCount - batteryCount);
                    if (item.amount > maxAccept)
                    {
                        batteryCount = instance.maxBatteryCount;
                        item.amount -= maxAccept;
                        if (!item.MoveToContainer(player.inventory.containerMain))
                            item.Drop(player.GetDropPosition(), player.GetDropVelocity());
                        instance.NextTick(Close);
                    }
                    else
                    {
                        batteryCount += item.amount;
                        item.Remove();
                    }
                    instance.batteries[cupboardId] = batteryCount;
                    instance.UpdateSelected(cupboardId);
                };
                container.canAcceptItem += item => item.info.shortname == "battery.small" ;
            }

            void OnDestroy()
            {
                instance.boxes.Remove(this);
            }

            public void Close()
            {
                corpse.Kill();
            }

            private void PlayerStoppedLooting(BasePlayer player)
            {
                corpse.Kill();
            }
        }


        #endregion

        #region CONFIGURATION

        private string boxCaption;
        private int deployBatteriesCount;
        private int maxBatteryCount;
        private int batteryUIRadius;
        private int batteryLifetime;
        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Надпись над слотами", out boxCaption, "");
            Config.GetVariable("Кол-во вставленных батареек при установке шкафа", out deployBatteriesCount, 1);
            Config.GetVariable("Максимальное кол-во батареек", out maxBatteryCount, 6);
            Config.GetVariable("Радиус от шкафа при котором появляется UI с батарейками", out batteryUIRadius, 10);
            Config.GetVariable("Время жизни одной батарейки(в секундах)", out batteryLifetime, 10);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        static CupboardSystem instance;

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        Dictionary<CupboardBox, BasePlayer> boxes = new Dictionary<CupboardBox, BasePlayer>();
        Dictionary<uint, int> batteries = new Dictionary<uint, int>() { {1,2}};
        Dictionary<BuildingPrivlidge,CupboardTrigger> triggers = new Dictionary<BuildingPrivlidge, CupboardTrigger>();
        Dictionary<BasePlayer, BuildingPrivlidge> selected = new Dictionary<BasePlayer, BuildingPrivlidge>();
        Dictionary<uint,long> batteryTimers = new Dictionary<uint, long>();
        
        bool init = false;
        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            instance = this;
            LoadData();
            ClearOldData();
            UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>().ToList().ForEach(InitTrigger);
            init = true;
            timer.Every(10f, OnDecayTimer);
        }

        void Unload()
        {
            SaveData();
            triggers.Values.ToList().ForEach(trigger => UnityEngine.Object.Destroy(trigger.gameObject));
            foreach (var box in boxes.Keys)
            {
                box.Close();
            }

        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (go == null || plan == null) return;
            if (go.name != "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab") return;

            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            OnCupboardDeployed(player, go.GetComponent<BuildingPrivlidge>());
        }

        private void OnEntitySpawned(BaseNetworkable ent)
        {
            if (!init) return;
            if (ent == null) return;
            BuildingPrivlidge privlidge = ent as BuildingPrivlidge; 
            if (privlidge == null) return;
            
            NextTick(()=>InitTrigger(privlidge));
        }
        void OnEntityKill(BaseNetworkable ent)
        {
            if (!init) return;
            if (ent == null) return;
            BuildingPrivlidge privlidge = ent as BuildingPrivlidge;
            if (privlidge == null) return;

            
            CupboardTrigger trigger;
            if (triggers.TryGetValue(privlidge, out trigger))
            {
                foreach (var owner in trigger.owners)
                {
                    OnPlayerExitTrigger(owner, privlidge);
                }
                UnityEngine.Object.Destroy(trigger.gameObject);
                triggers.Remove(privlidge);
            }

            var cupboardId = privlidge.net.ID;
            batteries.Remove(cupboardId);
            batteryTimers.Remove(cupboardId);
            var box = boxes.FirstOrDefault(b => b.Key.cupboardId == cupboardId);
            if (box.Key != null)
            {
                box.Key.Close();
                boxes.Remove(box.Key);
            }
        }
        
        #endregion

        #region CORE

        void InitTrigger(BuildingPrivlidge privlidge)
        {
            var obj = new GameObject();
            obj.transform.SetParent(privlidge.transform);
            obj.transform.position = privlidge.GetNetworkPosition();
            var trigger = obj.AddComponent<CupboardTrigger>();
            trigger.Init(privlidge);
            triggers[privlidge] = trigger;
        }

        void OnPlayerEnterTrigger(BasePlayer player, BuildingPrivlidge privlidge)
        {
            selected[player] = privlidge;
            int batteryCount;
            if (!batteries.TryGetValue(privlidge.net.ID, out batteryCount)) return;

            DrawBatteries(player,batteryCount);
        }

        void OnPlayerExitTrigger(BasePlayer player, BuildingPrivlidge privlidge)
        {
            selected.Remove(player);
            DestroyBatteries(player);
        }

        void OpenBox(BasePlayer player, uint cupboardId)
        {
            timer.Once(0.1f, () =>
            {
                StorageBox.Create(this, player, boxCaption, 18);
                var sb = StorageBox.AddComponent<CupboardBox>(this, player);
                boxes.Add(sb, player);
                sb.Init(player, cupboardId);
                StorageBox.StartLooting(this, player);
            });
        }

        void OnCupboardDeployed(BasePlayer player, BuildingPrivlidge cupboard)
        {
            var cupboardId = cupboard.net.ID;
            batteries[cupboardId] = deployBatteriesCount;
            batteryTimers[cupboardId] = GetTimeStamp() + batteryLifetime;
            Puts($"created {(GetTimeStamp() + batteryLifetime)}");
            OpenBox(player, cupboardId);
        }

        bool IsOwner(BasePlayer player, BuildingPrivlidge privlidge)
        {
            return GetTeamMembers(privlidge.OwnerID).Contains(player.userID);
        }
        
        void ClearOldData()
        {
            Puts("Очистка не валидных шкафов");
            List<uint> toRemove = new List<uint>();
            foreach (var battery in batteries)
            {
                if (BaseNetworkable.serverEntities.Find(battery.Key) == null)
                    toRemove.Add(battery.Key);
            }
            toRemove.ForEach(uid => { batteries.Remove(uid);
                batteryTimers.Remove(uid);
            });
            SaveData();
        }

        void UpdateSelected(uint cupboardId)
        {
            int batteryCount = batteries[cupboardId];
            foreach (var kvp in selected.Where(kvp=>kvp.Value.net.ID == cupboardId && kvp.Key.IsConnected))
            {
                if (batteryCount > 0)
                {
                    DrawBatteries(kvp.Key, batteryCount);
                }
                else
                {
                    DestroyBatteries(kvp.Key);
                }
            }
        }

        #endregion


        #region UI

        List<ulong> uiBatteriesCache = new List<ulong>();

        void DrawBatteries(BasePlayer player, int batteryCount)
        {
            DestroyBatteries(player);
            NextTick(()=> {
                Puts("1");
                core.DrawUI(player, "CupboardSystem", "batteries", batteryCount, maxBatteryCount);
            });
            if (!uiBatteriesCache.Contains(player.userID)) uiBatteriesCache.Add(player.userID);
        }

        void DestroyBatteries(BasePlayer player)
        {
            if (!uiBatteriesCache.Contains(player.userID)) return;
            Puts("2");
            core.DestroyUI(player, "CupboardSystem", "batteries");
            uiBatteriesCache.Remove(player.userID);
        }

        #endregion

        #region DECAY

        void OnDecayTimer()
        {
            var time = GetTimeStamp();
            Puts($"current {GetTimeStamp() + batteryLifetime}");
            Dictionary<uint, long> modified = new Dictionary<uint, long>();
            foreach (var decayTime in batteryTimers)
            {
                if (time > decayTime.Value)
                {
                    uint id = decayTime.Key;
                    if (--batteries[id] > 0)
                    {
                        modified[id] = time+batteryLifetime;
                        UpdateSelected(id);
                    }
                    else
                    {
                        NextTick(()=>DestroyCupboard(id));
                    }
                }
            }
            foreach (var decayTime in modified)
            {
                batteryTimers[decayTime.Key] = decayTime.Value;
            }
        }

        void DestroyCupboard(uint id)
        {
            var privlidge = BaseNetworkable.serverEntities.Find(id) as BuildingPrivlidge;
            if (privlidge == null) return;
            privlidge.Kill();
        }

        long GetTimeStamp()
        {
            return (long)DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }
        #endregion

        #region COMMANDS

        [ConsoleCommand("cupboardsystem.openbox")]
        void cmdOpenBox(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            BuildingPrivlidge privlidge;
            if (!selected.TryGetValue(player, out privlidge)) return;
            if (batteries[privlidge.net.ID] == maxBatteryCount)
            {
                SendReply(player, Messages["BatteryMax"]);
                return;
            }
            if (boxes.ContainsValue(player)) return;
            OpenBox(player, privlidge.net.ID);
        }

        #endregion

        #region CLANS

        [PluginReference]
        Plugin Clans;

        List<ulong> GetTeamMembers(ulong userId)
        {
            var members = Clans?.Call("ApiGetMembers", userId) as List<ulong> ?? new List<ulong>();
            if (members.Count == 0)
              members.Add(userId);
            return members;
        }
        #endregion

        #region DATA

        DynamicConfigFile batteriesFile = Interface.Oxide.DataFileSystem.GetFile("CupboardSystem");
        DynamicConfigFile batteryTimersFile = Interface.Oxide.DataFileSystem.GetFile("CupboardSystem.Timers");

        void LoadData()
        {
            batteries = batteriesFile.ReadObject<Dictionary<uint, int>>();
            batteryTimers = batteryTimersFile.ReadObject<Dictionary<uint, long>>();
        }

        void OnSeverSave() => SaveData();

        void SaveData()
        {
            batteriesFile.WriteObject(batteries);
            batteryTimersFile.WriteObject(batteryTimers);
        }

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "BatteryMax", "В шкафу уже максимум батареек!" }
        };

        #endregion
    }
}
