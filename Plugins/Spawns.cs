// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConVar;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using RustyCore;
using RustyCore.Utils;

namespace Oxide.Plugins
{
    [Info("Spawns", "bazuka5801","1.0.0")]
    public class Spawns : RustPlugin
    {

        #region VARIABLES
        static Spawns m_Instance;

        private RCore core = Interface.Oxide.GetLibrary<RCore>();
        List<Vector3> spawns = new List<Vector3>();
        List<GameObject> blocks = new List<GameObject>();
        float worldsize;
        #endregion

        #region CONFIGURATION

        float spawnRadius;
        int spawnsCount;
        int zoneRadius;
        int zoneOffset;
        List<string> whitelistWeapons = new List<string>(){};
        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Кол-во спаунов",out spawnsCount, 50);
            Config.GetVariable("Минимальная дистанция между спаунами", out spawnRadius, 30f);
            Config.GetVariable("Радиус зоны спауна", out zoneRadius, 700);
            Config.GetVariable("Смещение зоны от левого/правого края", out zoneOffset, 500);
            Config["Разрешённое оружие на спауне"] = whitelistWeapons = ((List<object>)Config["Разрешённое оружие на спауне"])?.Select(p=>p.ToString())?.ToList() ?? new List<string>();
            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));
        #endregion

        #region DATA

        private DynamicConfigFile spawnsFile = Interface.Oxide.DataFileSystem.GetFile("Spawns");

        #endregion

        #region OXIDE HOOKS

        void Init()
        {
            m_Instance = this;
            LoadDefaultConfig();
        }

        void OnServerInitialized()
        {
            lang.RegisterMessages( Messages, this );
            Messages = lang.GetMessages( "en", this );
            worldsize = TerrainMeta.Size.x;
            GenerateBlocks();
            RandomSpawns();
            CreateSpawnTriggers();
        }

        void Unloaded()
        {
            foreach (var block in blocks.Where(b => b != null))
                UnityEngine.Object.Destroy(block);
            DestroySpawnTriggers();
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(0.1f, () => OnPlayerInit(player));
                return;
            }
            if (InSpawnZone(player))
            {
                DrawUI(player);
            }
        }

        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;
            if (SpawnBlockEntity.IsBlock(entity))
            {
                player.ChatMessage("Строительство запрещено!!!\nВы находитесь в зоне возрождения, двигайтесь к центру или к полюсам карты!");
                entity.Kill();
            }
        }

        object OnPlayerRespawn(BasePlayer player)
        {
            return new BasePlayer.SpawnPoint() {pos = spawns.GetRandom(), rot = Quaternion.identity};
        }

        private List<string> Ricochets = new List<string>()
        {
            "assets/bundled/prefabs/fx/ricochet/ricochet1.prefab",
            "assets/bundled/prefabs/fx/ricochet/ricochet2.prefab",
            "assets/bundled/prefabs/fx/ricochet/ricochet3.prefab",
            "assets/bundled/prefabs/fx/ricochet/ricochet4.prefab"
        };
        private void OnEntityTakeDamage( BaseCombatEntity entity, HitInfo hitinfo )
        {
            if (entity == null || hitinfo == null || entity.IsDestroyed) return;
            var victim = entity as BasePlayer;
            if (victim == null) return;
            var info = hitinfo?.Weapon?.GetItem()?.info;
            var attacker = hitinfo.InitiatorPlayer;
            if (info == null || attacker == null) return;

            if ((InSpawnZone(entity)||InSpawnZone(attacker)) &&
                info.category == ItemCategory.Weapon && !whitelistWeapons.Contains(info.shortname)
                )
            {
                if (attacker?.IsConnected == true)
                {
                    Effect.server.Run(Ricochets.GetRandom(), hitinfo.HitPositionWorld);
                    SendReply(attacker, Messages[ "WEAPONBLOCKED" ]);
                }
                CancelDamage(hitinfo);
            }
        }
        #endregion

        #region FUNCTIONS
        
        private static void CancelDamage( HitInfo hitinfo )
        {
            hitinfo.damageTypes = new DamageTypeList();
            hitinfo.DoHitEffects = false;
            hitinfo.HitMaterial = 0;
        }

        bool IsValidSpawn(Vector3 point)
        {
            return spawns.All(spawn => !(Vector3.Distance(point, spawn) < spawnRadius)) &&
                   SpawnBlockEntity.CanPlaceSpawn(point);
        }

        void OnPlayerTeleported(BasePlayer player)
        {
            if (InSpawnZone(player))
                DrawUI(player);
            else DestroyUI(player);
        }
        void OnPlayerRespawned( BasePlayer player )
        {
            if (InSpawnZone( player ))
                DrawUI( player );
            else DestroyUI( player );
        }


        void CreateBlock(Vector3 position)
        {
            blocks.Add(SpawnBlockEntity.Create(position));
        }

        void DrawUI(BasePlayer player)
        {
            core.DrawUI(player, "Spawns", "inzone");
        }

        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI( player, "Spawns", "inzone" );
        }

        #endregion

        #region API

        Dictionary<Vector3, int> GetSpawnZones()=> blocks.ToDictionary(p => p.transform.position, p => zoneRadius);

        bool InSpawnZone(BaseEntity entity) => SpawnBlockEntity.IsBlock(entity);

        #endregion

        #region COMMANDS

        void RandomSpawns()
        {
            spawns.Clear();
            int failed = 0;
            while (failed < 1000 && spawns.Count < spawnsCount)
            {
                BasePlayer.SpawnPoint spawnPoint = SpawnHandler.GetSpawnPoint();

                if (spawnPoint == null)
                {
                    failed++;
                    continue;
                }
                if (!IsValidSpawn(spawnPoint.pos))
                {
                    failed++;
                    continue;
                }
                spawns.Add(spawnPoint.pos);
            }
            if (failed >= 1000)
                Puts("FAILED > 1000");
            Puts("CREATED "+spawns.Count.ToString());
            spawnsFile.WriteObject(JsonConvert.SerializeObject(spawns, converter));
        }

        void GenerateBlocks()
        {
            CreateBlock(new Vector3(-worldsize / 2+zoneOffset, 0, 0));
            CreateBlock(new Vector3(worldsize / 2-zoneOffset, 0, 0));
        }

        #endregion

        #region CLASSES
        
        private class SpawnBlockEntity : MonoBehaviour
        {
            private static List<SpawnBlockEntity> entities = new List<SpawnBlockEntity>();
            public static bool IsBlock(BaseEntity player) => entities.Any(e=>e.IsBlockPlayer(player));

            public static bool CanPlaceSpawn(Vector3 pos)=> entities.Any(e => Vector3.Distance(e.transform.position, pos) < m_Instance.zoneRadius);


            public bool IsBlockPlayer(BaseEntity player)
                => Vector3.Distance(player.transform.position, transform.position) <= m_Instance.zoneRadius;

            void Awake()
            {
                gameObject.name = "SPAWN";

                gameObject.layer = 3;
                entities.Add(this);
            }

            void OnDestroy()
            {
                entities.Remove(this);
            }

            public static GameObject Create(Vector3 pos)
            {
                var obj = new GameObject();

                SpawnBlockEntity block = obj.AddComponent<SpawnBlockEntity>();

                block.transform.position = pos;
                return block.gameObject;
            }
            
        }
        static void DrawBox(BasePlayer player, Vector3 center, Quaternion rotation, Vector3 size)
        {
            size /= 2;
            var point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
            var point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
            var point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
            var point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
            var point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
            var point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
            var point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
            var point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point3);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point5);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point3);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point8);

            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point6);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point7);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point6, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point6);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point7);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point7, point3);
        }

        static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }

        #endregion


        #region JSON CONVERTERS
        UnityVector3Converter converter = new UnityVector3Converter();

            public class UnityVector3Converter : JsonConverter
            {
                public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                {
                    var vector = (Vector3) value;
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

        #region Trigger

        private SpawnTrigger leftSpawnTrigger, rightSpawnTrigger;

        void CreateSpawnTriggers()
        {
            var leftGo = new GameObject();
            leftGo.transform.position = new Vector3(-worldsize / 2 + zoneOffset, 0, 0);
            leftSpawnTrigger = leftGo.AddComponent<SpawnTrigger>();
            var rightGo = new GameObject();
            rightGo.transform.position = new Vector3(worldsize / 2 - zoneOffset, 0, 0);
            rightSpawnTrigger = rightGo.AddComponent<SpawnTrigger>();
        }

        void DestroySpawnTriggers()
        {
            UnityEngine.Object.DestroyImmediate(leftSpawnTrigger.gameObject);
            UnityEngine.Object.DestroyImmediate( rightSpawnTrigger.gameObject);
        }

        public class SpawnTrigger : MonoBehaviour
        {
            private void Awake()
            {
                gameObject.layer = (int) Rust.Layer.Reserved1; //hack to get all trigger layers...otherwise child zones
                gameObject.name = "Spawn Zone";

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                var sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = m_Instance.zoneRadius;
                sphere.isTrigger = true;
            }

            private void OnTriggerEnter( Collider col )
            {
                var player = col.gameObject.ToBaseEntity() as BasePlayer;
                if (player == null || !player.IsConnected || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)) return;
                m_Instance.DrawUI(player);
            }
            private void OnTriggerExit( Collider col )
            {
                var player = col.gameObject.ToBaseEntity() as BasePlayer;
                if (player == null || !player.IsConnected || player.HasPlayerFlag( BasePlayer.PlayerFlags.ReceivingSnapshot )) return;
                m_Instance.DestroyUI( player );
            }
        }

        #endregion


        #region Localization

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "WEAPONBLOCKED", "<color=red>Урон не проходит</color> - вы в <color=orange>SPAWN</color> зоне" },
        };

        #endregion
    }
}
