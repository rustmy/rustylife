// Reference: Oxide.Core.RustyCore

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using RustyCore;
using RustyCore.Utils;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Promocodes", "bazuka5801", "1.0.0")]
    public class Promocodes : RustPlugin
    {
        #region Fiels

        private const float PromocodeZoneRadius = 250f;
        private int BuildingMask = LayerMask.GetMask("Construction", "Deployed");
        private string[] ResolvedMonuments = new[]
        {
            "military_tunnel_1",
            "airfield_1",
            "trainyard_1",
            "water_treatment_plant_1",
            "harbor",
            "satellite_dish",
            "sphere_tank"
        };

        [PluginReference]
        private Plugin Map;

        [PluginReference]
        private Plugin RustyLoot;

        private static Promocodes m_Instance;

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        


        private PromocodeEventState State = PromocodeEventState.Waiting;

        private int Timeout;
        private int MinimumOnline;
        private int DecodeDuration;
        private int MinMoneyReward;
        private int MaxMoneyReward;
        private int SpamTimeout;
        private int HintTimeout;

        private List<Vector3> PositionsOfMonuments = new List<Vector3>();
        private PromocodeTrigger PromocodeZone;
        private Vector3 PromocodeZonePosition;
        private LootContainer PromocodeLootContainer;
        private Item PromocodeItem;
        private BasePlayer PromocodePlayer;

        private Timer ActivationCooldown = null;
        private Timer HintTimer = null;
        private bool CanActivate = false;

        private Vector3 lastPlayerPosition = Vector3.zero;
        #endregion

        #region Commands

        [ChatCommand("promocode")]
        void cmdChatPromocode(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (State == PromocodeEventState.Waiting)
            {
                this.Reply(player, "STATE.WAITING", core.TimeToString(Timeout));
                return;
            }
            if (player != PromocodePlayer)
            {
                this.Reply(player, "NOTYOU");
                return;
            }
            if (!CanActivate)
            {
                this.Reply(player, "ACTIVETE.ENCODED.PROMOCODE");
                return;
            }
            OnEndPromocodeEvent();
        }
        [ChatCommand( "promocode.tp" )]
        void cmdChatPromocodeTp( BasePlayer player, string command, string[] args )
        {
            if (player == null || !player.IsAdmin) return;
            if (PromocodeLootContainer == null)
            {
                if (PromocodePlayer != null)
                {
                    core.Teleport(player, PromocodePlayer);
                }
                SendReply(player, "Контейнера нету!");
                return;
            }
            core.Teleport(player, PromocodeLootContainer.GetNetworkPosition());
        }

        [ConsoleCommand( "promocode.start" )]
        void cmdPromocodeStart( ConsoleSystem.Arg arg )
        {
            if (arg?.Connection != null) return;
            RunPromocodeEvent();
        }

        [ConsoleCommand( "promocode.notify" )]
        void cmdPromocodeNotify( ConsoleSystem.Arg arg )
        {
            if (arg?.Connection != null) return;
            if (PromocodeLootContainer == null)
            {
                SendReply(arg, "PromocodeLootContainer is null" );
                return;
            }
            OnHint();
        }
        #endregion

        #region Oxide hooks

        void OnServerInitialized()
        {
            m_Instance = this;
            InitPositionsOfMonuments();
            LoadDefaultConfig();
            timer.Every(Timeout, () =>
            {
                if (Player.Players.Count < MinimumOnline)
                {
                    this.Broadcast( "ONLINE.MIN", MinimumOnline );
                    return;
                }
                RunPromocodeEvent();
            });
            timer.Every(1f, OnPromocodePlayerMove);
            timer.Every(SpamTimeout, SpamEventMessage);
        }
        void Unload()
        {
            PromocodeItem?.Remove();
            if (PromocodePlayer != null)
                DestroyMapPlayerIcon(PromocodePlayer);
            DestroyPromocodeZone();
        }
        void OnPlayerDie( BasePlayer player, HitInfo info )
        {
            if (State != PromocodeEventState.Active || player && PromocodePlayer == player)
            {
                DestroyPromocodePlayer();
            }
        }
        void OnEntityDeath( BaseCombatEntity entity, HitInfo info )
        {
            if (State != PromocodeEventState.Active || info == null) return;
            var container = entity as LootContainer;
            var player = info?.InitiatorPlayer;
            
            if (container == null) return;
            if (PromocodeLootContainer != container)
            {
                return;
            }
            if (player == null)
            {
                NextTick(()=>SpawnPromocodeItem());
                return;
            }
            PromocodeItem.MoveToContainer(PromocodeLootContainer.inventory);

            DestroyHintTimer();
            OnHint();
            this.Broadcast( "PROMOCODE.CONTAINER.LOOTED", player.displayName );
            this.Log( "loot", $"{player.userID}/{player.displayName} loot promocode container ({PromocodeLootContainer.GetNetworkPosition()})", true );

            PromocodeLootContainer = null;
        }

        void OnLootEntity( BasePlayer player, BaseEntity entity )
        {
            if (State != PromocodeEventState.Active) return;
            var container = entity as LootContainer;
            if (container == null) return;
            if (PromocodeLootContainer != container)
            {
                return;
            }
            PromocodeItem.MoveToContainer( PromocodeLootContainer.inventory );
            DestroyHintTimer();
            OnHint();
            this.Broadcast( "PROMOCODE.CONTAINER.LOOTED", player.displayName );
            this.Log("loot", $"{player.userID}/{player.displayName} loot promocode container ({PromocodeLootContainer.GetNetworkPosition()})", true );
            PromocodeLootContainer = null;
        }

        void OnRemoveItem(Item item)
        {
            if (State != PromocodeEventState.Active||PromocodeItem == null || PromocodeItem != item) return;
            this.Broadcast( "PROMOCODE.REMOVED" );
            NextTick(()=>SpawnPromocodeItem());
        }

        void OnItemAddedToContainer( ItemContainer container, Item item )
        {
            if (State != PromocodeEventState.Active||item == null || container == null) return;
            if (item == PromocodeItem)
            {
                var player = container.GetOwnerPlayer();
                NextTick(()=>{
                    if (player != null)
                    {
                        if (PromocodePlayer == player) return;

                        if (PromocodePlayer != null && player.IsSleeping())
                        {
                            if (!item.MoveToContainer( PromocodePlayer.inventory.containerMain ))
                                item.MoveToContainer( PromocodePlayer.inventory.containerBelt );
                            return;
                        }

                        if (PromocodePlayer != null)
                        {
                            DestroyMapPlayerIcon(PromocodePlayer);
                        }
                        PromocodePlayer = player;
                        OnPlayerPickupPromocode(player);
                        CreateMapPlayerIcon(player);
                    }
                    else if (PromocodePlayer != null)
                    {
                        if (!item.MoveToContainer(PromocodePlayer.inventory.containerMain))
                            item.MoveToContainer(PromocodePlayer.inventory.containerBelt);
                    }
                } );
            }
        }


        bool? OnItemAction( Item item, string action )
        {
            if (PromocodeItem == null || item == null || string.IsNullOrEmpty(action) || PromocodeItem != item) return null;
            if (action == "drop") return false;
            return null;
        }

        void OnPlayerSleep(BasePlayer player)
        {
            if (State != PromocodeEventState.Active || PromocodePlayer != player)return;
            SpawnPromocodeItem();
            this.Broadcast( "PROMOCODE.PLAYER.DISCONNECTED", PromocodePlayer.displayName );
            DestroyPromocodePlayer();
        }

        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (State != PromocodeEventState.Active||planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;
            if (PromocodePlayer == player || InPromocodeZone(entity))
            {
                RefundHelper.Refund(player, entity);
                entity.Kill();
                this.Reply(player, "BUILDING.BlOCKED" );
                return;
            }
        }

        void OnPromocodePlayerMove()
        {
            if (State != PromocodeEventState.Active) return;
            if (PromocodePlayer == null) return;
            var pos = PromocodePlayer.GetNetworkPosition();
            if (lastPlayerPosition == Vector3.zero)
            {
                lastPlayerPosition = pos;
                return;
            }
            RaycastHit hit;
            if (Physics.Raycast(new Ray(pos + new Vector3(0, 0.1f, 0), Vector3.down), out hit, 5f, BuildingMask))
            {
                PromocodePlayer.Teleport(lastPlayerPosition);
                this.Reply( PromocodePlayer, "BUILDING.STAYBLOCKED" );
                return;
            }
            lastPlayerPosition = pos;
        }

        string CanTeleport(BasePlayer player)
        {
            if (State != PromocodeEventState.Active) return null;
            return PromocodePlayer == player ? "Телепорт заблокирован: вы на ивенте" : null;
        }

        #endregion

        #region Core

        void InitPositionsOfMonuments()
        {
            var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();
            foreach (var monument in monuments)
            {
                if (monument.name.ContainsAny(ResolvedMonuments))
                    PositionsOfMonuments.Add(monument.transform.position);
            }
        }

        void RunPromocodeEvent()
        {
            if (PromocodeZone != null)
            {
                this.Broadcast( "LAST.EVENT.NOT.END" );
                return;
            }
            if (!SpawnPromocodeItem())return;
            HintTimer = timer.Once(HintTimeout, OnHint);
            DrawStart();
            timer.Once(30f, DestroyStart);

            CreatePromocodeZone();
            State = PromocodeEventState.Active;
        }

        void OnEndPromocodeEvent()
        {
            Reward();
            DestroyHintTimer();
            DestroyPromocodePlayer();
            DestroyPromocodeZone();
            State = PromocodeEventState.Waiting;
        }

        void DestroyHintTimer()
        {
            if (HintTimer != null && !HintTimer.Destroyed) timer.Destroy( ref HintTimer );
        }

        void OnHint()
        {
            CreateRocketNotification(PromocodeLootContainer.GetNetworkPosition());
        }

        void CreateRocketNotification(Vector3 pos)
        {
            var notifyStart = pos + Vector3.up;
            CreateRocket( notifyStart, Vector3.up );
            CreateRocket( notifyStart, Vector3.down );

            var rayStart = pos + Vector3.up * 30;
            CreateRocket( rayStart, Vector3.right );
            CreateRocket( rayStart, Vector3.left );
            CreateRocket( rayStart, Vector3.forward );
            CreateRocket( rayStart, Vector3.back );
        }

        void Reward()
        {
            var moneyCount = UnityEngine.Random.Range(MinMoneyReward, MaxMoneyReward);
            Server.Command( "store.money.plus", PromocodePlayer.userID,moneyCount);
            LogToFile("rewards", $"{DateTime.Now:G}: '{PromocodePlayer.displayName}' получил награду {moneyCount}р", this);
            this.Broadcast( "PROMOCODE.WIN", PromocodePlayer.displayName, moneyCount );
        }

        void SpamEventMessage()
        {
            if (State == PromocodeEventState.Waiting) return;
            this.Broadcast( PromocodePlayer == null ? "PROMOCODE.START" : "PROMOCODE.AFTERPICKUP" );
        }

        void OnPlayerPickupPromocode(BasePlayer player)
        {
            lastPlayerPosition = Vector3.zero;
            ResetActivationBlockTimer();

            var decode = core.TimeToString(DecodeDuration);
            this.Broadcast( "PROMOCODE.PICKUP.GLOBAL", player.displayName, decode );
            this.Reply(player, "PROMOCODE.PICKUP.PLAYER", decode );

            ActivationCooldown = timer.Once(DecodeDuration, OnPlayerCanActivate);
        }

        void SetupPromocodePlayer(BasePlayer player)
        {
            PromocodePlayer = player;
            OnPlayerPickupPromocode( player );
            CreateMapPlayerIcon( player );
        }

        void DestroyPromocodePlayer()
        {
            if (PromocodePlayer == null) return;
            CanActivate = false;
            ResetActivationBlockTimer();
            DestroyMapPlayerIcon(PromocodePlayer);
            PromocodePlayer = null;
        }

        void OnPlayerCanActivate()
        {
            CanActivate = true;
            if (PromocodePlayer.IsConnected)
            {
                this.Reply(PromocodePlayer, "CAN.ACTIVATE.PLAYER");
            }
            this.Broadcast( "CAN.ACTIVATE.GLOBAL", PromocodePlayer.displayName );
        }

        void ResetActivationBlockTimer()
        {
            if (ActivationCooldown != null) timer.Destroy( ref ActivationCooldown );
            CanActivate = false;
        }

        void OnPlayerEnterPromocodeZone( BasePlayer player )
        {
            this.Reply(player, "PLAYER.ZONE.ENTER" );
        }

        void OnPlayerExitPromocodeZone( BasePlayer player )
        {
            this.Reply(player, "PLAYER.ZONE.EXIT" );
        }

        bool? CanTrade(BasePlayer player)
        {
            return PromocodePlayer == player ? false : (bool?) null;
        }

        bool SpawnPromocodeItem()
        {
            var item = PromocodeItem;
            PromocodeItem = null;
            item?.Remove();
            LootContainer lootContainer = null;
            int i = 0;
            PromocodeItem = CreatePromocodeItem();
            do
            {
                if (++i > 50) { break; }
                PromocodeZonePosition = PositionsOfMonuments.GetRandom();
                if (PromocodeZone) PromocodeZone.transform.position = PromocodeZonePosition;
                lootContainer = GetPromocodeContainer( PromocodeZonePosition );
            } while (lootContainer == null || lootContainer.inventory.CanAcceptItem( PromocodeItem ) != ItemContainer.CanAcceptResult.CanAccept || !(bool) ( RustyLoot?.Call( "IgnoreContainer", lootContainer ) ?? false ));
            if (lootContainer == null)
            {
                PromocodeItem.Remove();
                PromocodeItem = null;
                this.Broadcast("LOOTCONTAINER.MISSING");
                return false;
            }
            PromocodeLootContainer = lootContainer;
            this.Broadcast( "PROMOCODE.SPAWNED" );
            PrintWarning( lootContainer.GetNetworkPosition().ToString() );
            return true;
        }

        void CreatePromocodeZone()
        {
            var zoneObject = new GameObject();
            
            zoneObject.transform.position = PromocodeZonePosition;
            PromocodeZone = zoneObject.AddComponent<PromocodeTrigger>();
            Map.Call("AddTemporaryMarker", "promocode.zone",  false,2* PromocodeZoneRadius / TerrainMeta.Size.x, 1f,PromocodeZone.transform);
            
        }

        void DestroyPromocodeZone()
        {
            if (PromocodeZone)
            {
                Map.Call( "RemoveTemporaryMarkerByTransform", PromocodeZone.transform );
                UnityEngine.Object.DestroyImmediate(PromocodeZone.gameObject);
            }
        }

        void CreateMapPlayerIcon(BasePlayer player)
        {
            Map.Call( "AddTemporaryMarker", "promocode.player", false, 0.03f, 0.95f, player.transform );
        }
        void DestroyMapPlayerIcon(BasePlayer player)
        {
            Map.Call( "RemoveTemporaryMarkerByTransform", player.transform );
        }

        void CreateRocket( Vector3 startPoint , Vector3 dir)
        {
            BaseEntity entity = null;
            entity = GameManager.server.CreateEntity( TOD_Sky.Instance.IsNight ? "assets/prefabs/npc/patrol helicopter/rocket_heli_airburst.prefab" : "assets/prefabs/ammo/rocket/rocket_smoke.prefab", startPoint + new Vector3( 0, 10, 0 ), new Quaternion(), true );
            var timedExplosive = entity.GetComponent<TimedExplosive>();
            timedExplosive.timerAmountMin = 10;
            timedExplosive.timerAmountMax = 10;
            var serverProjectile = entity.GetComponent<ServerProjectile>();
            serverProjectile.gravityModifier = 0f;
            serverProjectile.speed = 15;
            foreach (DamageTypeEntry dmg in timedExplosive.damageTypes)
            {
                dmg.amount *= 0f;
            }
            entity.SendMessage( "InitializeVelocity",dir* 2f );
            entity.Spawn();
        }

        static System.Random rand = new System.Random();
        static string GetRandomString( int length = 10 )
        {
            char[] chars = new char[ length ];
            for (int i = 0; i < length; i++)
                chars[ i ] = (char) rand.Next( 0x000, 0x200 );
            return new string( chars );
        }

        Item CreatePromocodeItem()
        {
            var note = ItemManager.CreateByItemID( 3387378 );
            note.text = GetRandomString();
            return note;
        }

        LootContainer GetPromocodeContainer(Vector3 position)
        {
            var containers = new List<LootContainer>();
            Vis.Entities(position, PromocodeZoneRadius, containers, -1,QueryTriggerInteraction.Collide);
            if (containers.Count == 0) return null;
            return containers.GetRandom();
        }

        bool InPromocodeZone(BaseEntity entity)
        {
            return !(Vector3.Distance(PromocodeZonePosition, entity.GetNetworkPosition()) > PromocodeZoneRadius);
        }

        #endregion

        #region UI
        
        void DrawStart()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                core.DrawUI( player, "Promocodes", "start" );
            }
        }

        void DestroyStart()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                core.DestroyUI( player, "Promocodes", "start" );
            }
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Время между ивентами (в секундах)", out Timeout, 7200);
            Config.GetVariable( "Минимальный онлайн для запуска ивента", out MinimumOnline, 50 );
            Config.GetVariable( "Длительность расшифровки промокода после его поднятия", out DecodeDuration, 300 );
            Config.GetVariable( "Минимальная награда (деньги на магазин)", out MinMoneyReward, 20 );
            Config.GetVariable( "Максимальная награда (деньги на магазин)", out MaxMoneyReward, 70 );
            Config.GetVariable( "Переодичность рассылки сообщения об ивенте", out SpamTimeout, 60 );
            Config.GetVariable( "Через сколько будет подсказка", out HintTimeout, 1800 );
            SaveConfig();
        }

        #endregion

        #region Localization

        private Dictionary<string,string> Messages = new Dictionary<string, string>()
        {
            { "ONLINE.MIN", "Ивент 'Промокод' пропущен!\nПричина: не набран минимальный онлайн({0} игроков)" },
            { "LOOTCONTAINER.MISSING", "Ивент 'Промокод' пропущен!\nПричина: не найден контейнер для записки!" },
            { "LAST.EVENT.NOT.END", "Ивент 'Промокод' пропущен!\nПричина: предыдущий Event еще не закончился!" },
            { "PLAYER.ZONE.EXIT", "Вы вышли из зоны ивента 'Promocode'" },
            { "PLAYER.ZONE.ENTER", "Вы вошли в зону ивента 'Promocode'" },
            { "NOTYOU", "У вас нет промокода!" },
            { "STATE.WAITING", "Ивента 'Promocode' не запущен, он запускается автоматически каждые {0}..." },
            { "CAN.ACTIVATE.PLAYER", "Поздравляем, вы успешно расшифровали промокод, теперь можете его активировать: /promocode" },
            { "CAN.ACTIVATE.GLOBAL", "Игрок '{0}' расшифровал промокод и может его активировать - поспешите забрать промокод!" },
            { "PROMOCODE.PLAYER.DISCONNECTED", "Расшифровка промокода у игрока '{0}' приостановлена\nПричина: игрок заснул\nПромокод перереспаунился!" },
            { "PROMOCODE.PICKUP.GLOBAL", "Игрок '{0}' нашёл промокод, у него есть {1} чтобы расшифровать его - поспешите его убить!" },
            { "PROMOCODE.PICKUP.PLAYER", "Поздравляем, вы нашли промокод, расшифровка займёт {0}\nВы видны на карте - постарайтесь выжить!" },
            { "PROMOCODE.WIN", "Ивент 'Promocode' окончен.\n'{0}' расшифровал и использовал промокод\nПолученный приз: {1} руб" },
            { "ACTIVETE.ENCODED.PROMOCODE", "Промокод ещё не расшифрован!" },
            { "BUILDING.BlOCKED", "Вы носитель промокода - строительство запрещено!" },
            { "BUILDING.STAYBLOCKED", "Вы носитель промокода - Заходить в постройки запрещено!" },
            { "PROMOCODE.START", "Начался ивент, ищите промокод на карте" },
            { "PROMOCODE.AFTERPICKUP", "Игрок с промокодом отображён на карте, ликвидируйте объект и заберите его промокод" },
            { "PROMOCODE.SPAWNED", "Промокод заспавнен, ищите его в зоне" },
            { "PROMOCODE.REMOVED", "Промокод куда-то делся, зареспаунили его заного, возможно в новой зоне" },
            { "PROMOCODE.CONTAINER.LOOTED", "'{0}' обнаружил контейнер с промокодом!" },
        };
        
        #endregion

        #region Nested type: PromocodeTriggers

        public class PromocodeTrigger : MonoBehaviour
        {
            private void Awake()
            {
                gameObject.layer = (int) Rust.Layer.Reserved1; //hack to get all trigger layers...otherwise child zones
                gameObject.name = "Promocode Zone";

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                var sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = PromocodeZoneRadius;
                sphere.isTrigger = true;
            }

            
            private void OnTriggerEnter( Collider col )
            {
                var player = col.gameObject.ToBaseEntity() as BasePlayer;
                if (player == null || !player.IsConnected || player.HasPlayerFlag( BasePlayer.PlayerFlags.ReceivingSnapshot )) return;
                m_Instance.OnPlayerEnterPromocodeZone( player );
            }
            private void OnTriggerExit( Collider col )
            {
                var player = col.gameObject.ToBaseEntity() as BasePlayer;
                if (player == null || !player.IsConnected || player.HasPlayerFlag( BasePlayer.PlayerFlags.ReceivingSnapshot )) return;
                m_Instance.OnPlayerExitPromocodeZone( player );
            }
        }

        #endregion

        #region Nested type: PromocodeEventState
        
        public enum PromocodeEventState
        {
            Active,
            Waiting
        }

        #endregion
    }
}
