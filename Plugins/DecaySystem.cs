// Reference: Oxide.Core.RustyCore

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using RustyCore.Utils;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DecaySystem", "bazuka5801", "1.1.0")]
    class DecaySystem : RustPlugin
    {
        #region CLASSES

        class Vector3
        {
            public float x, y, z;

            public Vector3(string s)
            {
                var ss = s.Split(' ');
                this.x = float.Parse(ss[0]);
                this.y = float.Parse(ss[1]);
                this.z = float.Parse(ss[2]);
            }

            public string Save() => $"{x} {y} {z}";

            public Vector3(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public Vector3(UnityEngine.Vector3 vec)
            {
                this.x = vec.x;
                this.y = vec.y;
                this.z = vec.z;
            }

            public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(x, y, z);

            public static bool Equal(Vector3 a, Vector3 b)
                => UnityEngine.Vector3.Distance(a.ToVector3(), b.ToVector3()) < 0.001;

            public static bool Equal(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
                => UnityEngine.Vector3.Distance(a, b) < 0.001;

        }

        #endregion

        #region PLUGINS API

        [PluginReference]
        Plugin ZoneManager;

        bool inZone(UnityEngine.Vector3 vec)
        {
            if (ZoneManager == null) return false;
            return (bool)ZoneManager.Call("inZone", vec);
        }

        #endregion

        #region CONST VARIABLES

        private List<string> baseDecayPrefabs = new List<string>()
        {
            "foundation",
            "foundation.triangle",
            "gates.external.high.wood",
            "wall.external.high.wood",
            "gates.external.high.stone",
            "wall.external.high.stone",
        };

        private List<string> defaultDecayPrefabs = new List<string>()
        {
            "foundation",
            "foundation.triangle",
            "wall.external.high.stone",
            "cupboard.tool.deployed",
            "box.wooden.large",
            "sleepingbag_leather_deployed",
            "stocking_large_deployed",
            "stocking_small_deployed",
            "furnace",
            "woodbox_deployed",
            "barricade.sandbags",
            "barricade.stone",
            "jackolantern.happy",
            "barricade.concrete",
            "floor.grill",
            "barricade.metal",
            "autoturret_deployed",
            "campfire",
            "repairbench_deployed",
            "beartrap",
            "wall.external.high.wood",
            "bed_deployed",
            "gates.external.high.stone",
            "furnace.large",
            "refinery_small_deployed",
            "reactivetarget_deployed",
            "barricade.woodwire",
            "landmine",
            "lantern.deployed",
            "ceilinglight.deployed",
            "gates.external.high.wood",
            "spikes.floor",
            "barricade.wood",
            "jackolantern.angry",
            "water_catcher_large"
        };

        #endregion

        List<BaseCombatEntity> decayEntities;
        Dictionary<uint, uint> blockCupboards = new Dictionary<uint, uint>();
        int WorldBuildingsLayer = LayerMask.GetMask("Construction", "World", "Terrain");
        int BuildingsLayer = LayerMask.GetMask("Construction");
        int DeployedLayer = LayerMask.GetMask("Deployed");

        HashSet<uint> cupboards = new HashSet<uint>();

        #region CONGIGURATION

        private int doorTimeout;
        private float cupboardRadius;
        private float timeout = 3600;
        private Dictionary<string, int> decaySettings = new Dictionary<string, int>();
        protected override void LoadDefaultConfig()
        {
            Config["Задержка после открытия/закрытия двери"] =
                doorTimeout = GetConfig("Задержка после открытия/закрытия двери", 5);
            Config["Радиус обнаружения дверей"] =
                cupboardRadius = GetConfig("Радиус обнаружения дверей", 10f);
            Config["Гниение объектов"] =
                decaySettings =
                    GetConfig("Гниение объектов", defaultDecayPrefabs.ToDictionary(p => p, p => (object)2))
                        .ToDictionary(p => p.Key, p => int.Parse(p.Value.ToString()));
            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue)
            => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion

        #region DATA

        private DynamicConfigFile blockCupboardsFile = Interface.Oxide.DataFileSystem.GetFile("DecaySystem_Cupboards");
        void LoadBlockPrivlidges()
        {
            blockCupboards = blockCupboardsFile.ReadObject<Dictionary<uint, uint>>();
        }

        void Unload()
        {
            OnServerSave();
            if (decayCoroutine != null)
                CommunityEntity.ServerInstance.StopCoroutine(decayCoroutine);
        }


        void OnServerSave()
        {
            blockCupboardsFile.WriteObject(blockCupboards);
        }

        #endregion

        #region COMMANDS

        [ChatCommand("decay")]
        void cmdChatDecay(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            RunDecay();
        }

        [ConsoleCommand("decay")]
        void cmdDecay(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            RunDecay();
        }

        
        /*[ConsoleCommand("setdoors")]
        void cmdSetDoors(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            foreach (var door in UnityEngine.Object.FindObjectsOfType<Door>())
            {
                if (door?.net?.ID != null)
                    doorTimers[door.net.ID] = doorTimeout;
            }
            Puts("success");
        }*/

        #endregion

        #region OXIDE HOOKS

        private bool init = false;
        private bool isdecay = false;
        private UnityEngine.Vector3 offset = new UnityEngine.Vector3(0, 0.5f, 0);
        private Coroutine decayCoroutine;

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            LoadBlockPrivlidges();
            timer.Every(timeout, RunDecay);
            InitDecayEntities();
            foreach (var cupboard in UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>())
            {
                cupboards.Add(cupboard.net.ID);
            }
        }

        void InitDecayEntities()
        {
            var ents = UnityEngine.Object.FindObjectsOfType<BaseCombatEntity>();
            decayEntities = new List<BaseCombatEntity>(ents.Length);

            Parallel.For(0, ents.Length, (i) =>
            {

                var ent = ents[i];
                if (ent == null) return;
                DecayEntity decEnt = ent as DecayEntity;
                NextTick(() =>
                {
                    if (decEnt != null)
                    {
                        decEnt.CancelInvoke("RunDecay");
                    }
                    if (IsDecayEntity(ent))
                        decayEntities.Add(ent);
                });
            });
            Puts($"Загрузка объектов прошла успешно! Count: {ents.Length}");
            init = true;
        }
        

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!init) return;
            if (entity?.net?.ID == null) return;
            if (IsDecayEntity(entity) && !decayEntities.Contains((BaseCombatEntity)entity))
                decayEntities.Add((BaseCombatEntity)entity);
            BuildingPrivlidge privlidge = entity as BuildingPrivlidge;
            if (privlidge != null)
                cupboards.Add(privlidge.net.ID);
            NextTick(() =>
            {
                DecayEntity decEnt = entity as DecayEntity;
                if (decEnt != null)
                {
                    decEnt.CancelInvoke("RunDecay");
                }
            });
        }

        bool IsDecayEntity(BaseNetworkable entity)
        {
            if (decaySettings.ContainsKey(entity.ShortPrefabName))
            {
                if (!baseDecayPrefabs.Contains(entity.ShortPrefabName))
                {
                    RaycastHit hit;
                    var ray = new Ray(entity.transform.position + offset,
                        entity.transform.TransformDirection(UnityEngine.Vector3.down));
                    if (!Physics.Raycast(ray, out hit, 5, WorldBuildingsLayer, QueryTriggerInteraction.Ignore))
                        return true;
                    if (hit.transform.gameObject.layer == BuildingsLayer)
                        return false;
                }
                return true;
            }
            return false;
        }

        void OnEntityKill(BaseNetworkable ent)
        {
            if (!init) return;
            if (ent?.net?.ID == null) return;
            BuildingPrivlidge privlidge = ent as BuildingPrivlidge;
            if (privlidge != null)
                cupboards.Remove(privlidge.net.ID);

            var combatEnt = ent as BaseCombatEntity;
            if (combatEnt != null && decaySettings.ContainsKey(ent.ShortPrefabName))
                decayEntities.Remove(combatEnt);
        }

        
        #endregion

        #region CORE

        
        void RunDecay()
        {
            if (decayCoroutine != null)
                CommunityEntity.ServerInstance.StopCoroutine(decayCoroutine);
            decayCoroutine = CommunityEntity.ServerInstance.StartCoroutine(Decay());
        }

        bool HasCupboard(BaseCombatEntity ent)
        {
            uint entId = ent.net.ID;
            uint cupboardId;
            if (blockCupboards.TryGetValue(entId, out cupboardId))
            {
                if (cupboards.Contains(cupboardId))
                    return true;
                blockCupboards.Remove(entId);
            }
            var cupboard = GetCupboard(ent);
            if (cupboard == null) return false;
            blockCupboards[entId] = cupboard.net.ID;
            return true;
        }

        BuildingPrivlidge GetCupboard(BaseCombatEntity ent)
        {
            var position = ent.GetNetworkPosition();
            List<SphereCollider> colliders = new List<SphereCollider>();
            Vis.Colliders(position, cupboardRadius, colliders, -1, QueryTriggerInteraction.Collide);
            var privlidges = colliders.Where(p => p.transform.parent?.name == "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab").ToList();
            if (privlidges.Count > 0) return privlidges[0].transform.parent.GetComponent<BuildingPrivlidge>();
            return null;
        }

        IEnumerator Decay()
        {
            isdecay = true;
            PrintToChat("<size=18><color=#fee3b4>Запущена оптимизация карты</color></size>\nПожалуйста, ожидайте...");
            
            int i = 0;
            int count = decayEntities.Count;
            int die = 0;
            int lastpercent = -1;
            var start = DateTime.UtcNow;
            decayEntities.RemoveAll(item => item == null || item.IsDestroyed);
            StopwatchUtils.StopwatchStart("DecaySystem");
            foreach (var block in decayEntities.ToArray())
            {

                i++;

                var percent = (int) (i / (float) count * 100);
                if (StopwatchUtils.StopwatchElapsedMilliseconds("DecaySystem") > 10 || percent != lastpercent)
                {
                    StopwatchUtils.StopwatchStart("DecaySystem");
                    if (percent != lastpercent)
                    {
                        if (percent % 20 == 0)
                            Puts($"Идёт оптимизация карты: {percent}%");
                        lastpercent = percent;
                        yield return new WaitForSeconds(0.2f);
                    }
                }

                if (Performance.report.frameRate < 150 || Performance.current.frameRate < 150 || i % 10 == 0)
                    yield return new WaitForEndOfFrame();

                if (block == null) continue;
                if (block.IsDestroyed) continue;
                if (block.transform == null) continue;
                if (inZone(block.transform.position)) continue;
                if (HasCupboard(block)) continue;

                block.Hurt(block.MaxHealth() / decaySettings[block.ShortPrefabName], DamageType.Decay);
                if (block.IsDead())
                {
                    die++;
                    yield return new WaitForEndOfFrame();
                }
            }
            var time = DateTime.UtcNow.Subtract(start).TotalSeconds.ToString("F2");
            Puts($"count:{count} die:{die}");
            PrintToChat($"<size=18><color=#fee3b4>Оптимизация карты завершена</color></size>\nОбработанно объектов: <color=#fee3b4>{count}</color>\nРазрушенно объектов: <color=#fee3b4>{die}</color>\nЗатрачено времени: <color=#fee3b4>{time}c</color>");
            List<BaseCombatEntity> list = new List<BaseCombatEntity>(decayEntities.Count);
            i = 0;
            foreach (var p in decayEntities.ToArray())
            {
                if (p != null) list.Add(p);
                if (i++ % 100 == 0)
                    yield return new WaitForFixedUpdate();
            }
            decayEntities = list;
            isdecay = false;
        }

        #endregion
    }
}
