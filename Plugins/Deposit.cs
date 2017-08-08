// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apex;
using Facepunch;
using Network;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using RustyCore;
using RustyCore.Utils;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
    [Info( "Deposit", "bazuka5801", "1.0.2" )]
    public class Deposit : RustPlugin
    {
        public static Deposit m_Instance;
        RCore core = Interface.Oxide.GetLibrary<RCore>();

        

        #region VARIABLES

        private List<Competition> competitions = new List<Competition>();
        private Dictionary<string, DepositController> m_Boxes = new Dictionary<string, DepositController>();
        private Dictionary<DepositController, string> m_Hashes = new Dictionary<DepositController, string>();
        #endregion


        #region Oxide Hook's


        void Loaded()
        {
            m_Instance = this;

        }
        void Unloaded()
        {
            foreach (var trade in m_Boxes)
            {
                TradeBox.Destroy( trade.Key );
                UnityEngine.Object.Destroy( trade.Value, 0.1f );
            }
        }
        

        private bool init = false;
        void OnServerInitialized()
        {
            init = true;
        }
        

        #endregion

        #region Hooks
        

        #endregion

        #region Function's
        

        string StartDeposit( List<BasePlayer> players )
        {
            var hash = CuiHelper.GetGuid();

            var trade = OpenBox(players[0], players[1]);
            m_Hashes.Add(trade, hash);
            var playrs = players.ToList();
            NextTick(() =>
            {
                var ret1 = canDeposit( playrs[ 0]);
                var ret2 = canDeposit( playrs[ 1]);
                if (ret1 != null)
                {
                    players[0].ChatMessage(ret1);
                    OnDepositCanceled(trade.guid);
                }
                else if (ret2 != null)
                {
                    players[1].ChatMessage(ret2);
                    OnDepositCanceled(trade.guid);
                }
            });

            return hash;
            
        }
        private void OnCompleteTrade( ShopFront shop )
        {
            var trade = m_Boxes.Select(p=>p.Value).FirstOrDefault( p => p.shop.net.ID == shop.net.ID );
            if (trade != null)
            {
                global::RustyCore.Utils.Logger.Info( $"OnCompleteTrade {trade.shop.net.ID}" );
                trade.Complete();
                var competition = new Competition();

                m_Instance.Puts( "Ставка " + shop.customerPlayer );
                var customerItems = new List<Item>();
                foreach (var item in shop.customerInventory.itemList.Where( p => p != null ).ToList())
                {
                m_Instance.Puts( item.info.shortname+" "+item.amount );
                    item.RemoveFromContainer();
                    customerItems.Add( item );
                }

                m_Instance.Puts( "Ставка " + shop.vendorPlayer );
                var vendorItems = new List<Item>();
                foreach (var item in shop.vendorInventory.itemList.Where( p => p != null ).ToList())
                {
                    m_Instance.Puts( item.info.shortname + " " + item.amount );
                    item.RemoveFromContainer();
                    vendorItems.Add( item );
                }

                competitions.Add(competition);
                competition.Players.Add(shop.customerPlayer);
                competition.Players.Add(shop.vendorPlayer);
                competition.Bets = new Dictionary<BasePlayer, List<Item>>()
                {
                    { shop.customerPlayer, customerItems },
                    { shop.vendorPlayer  , vendorItems },
                };
                var hash = m_Hashes[trade];
                m_Hashes.Remove(trade);
                Duels?.Call("OnDepositEnd", true, hash );
                DropDeposit(trade);
            }
        }
        private DepositController OpenBox( BasePlayer player1, BasePlayer player2)
        {
            var guid = TradeBox.Create( player1, player2 );
            var trade = TradeBox.AddComponent<DepositController>( guid );
            trade.Init( guid, player1, player2 );
            global::RustyCore.Utils.Logger.Info( $"OpenBox {trade.shop.net.ID}/{player1.displayName}/{player2.displayName}" );
            m_Boxes.Add(guid, trade);
            return trade;
        }

        private void OnDepositCanceled( string guid )
        {
            DepositController trade;
            if (m_Boxes.TryGetValue( guid, out trade ))
            {
                global::RustyCore.Utils.Logger.Info( $"DepositCanceled {trade.shop.net.ID}" );
                trade.shop.ReturnPlayerItems( trade.player1 );
                trade.shop.ReturnPlayerItems( trade.player2 );
                var hash = m_Hashes[ trade ];
                m_Hashes.Remove( trade );
                DropDeposit( trade );
                Duels?.Call( "OnDepositEnd", false, hash );
            }
        }

        private void DropDeposit( DepositController trade )
        {
            global::RustyCore.Utils.Logger.Info( $"DropDeposit {trade.shop.net.ID}" );
            m_Boxes.Remove( trade.guid );
            TradeBox.Destroy( trade.guid );
            UnityEngine.Object.DestroyImmediate( trade );
        }

        private Competition FindCompetition(BasePlayer player)
        {
            return competitions.FirstOrDefault(p => p.Players.Contains(player));
        }

        #endregion

        void OnPlayerLootEnd( PlayerLoot inventory )
        {
            var player = inventory.gameObject.ToBaseEntity();
            if (player == null) return;
            var box = m_Boxes.Select( p => p.Value ).FirstOrDefault( p => p.player1 == player || p.player2 == player );
            if (box != null)
            {
                OnDepositCanceled( box.guid );
            }
        }


        #region Duels


        void DuelEnd( bool draw, BasePlayer winner )
        {
            var competition = FindCompetition( winner );
            if (competition == null) return;
            if (draw)
                foreach (var player in competition.Players.ToList())
                {
                    var mainPlayer = player;
                    Wait(() => !mainPlayer.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) && !InDuel( mainPlayer ),
                        () =>
                        {
                            competition.ReturnBet( mainPlayer );
                            if (competition.Players.Count == 0)
                            {
                                competitions.Remove(competition);
                            }
                        }, 1);
                }
            else
            {
                Wait( () => !winner.HasPlayerFlag( BasePlayer.PlayerFlags.ReceivingSnapshot ) && !InDuel( winner ),
                    () =>
                    {
                        competition.Reward(winner);
                        competition.Players.Clear();
                        competitions.Remove(competition);
                    }, 1 );
            }
        }

        #endregion

        #region External Call's

        [PluginReference]
        Plugin EventManager;

        [PluginReference]
        Plugin Duels;

        [PluginReference]
        Plugin NoEscape;

        bool InEvent( BasePlayer player )
        {
            if (EventManager == null) return false;
            try
            {
                var ret = EventManager?.Call( "isPlaying", player );
                if (ret == null) return false;
                bool result = (bool) ret;
                return result;
            }
            catch
            {
                return false;
            }
        }

        bool InDuel( BasePlayer player )
        {
            if (Duels == null) return false;
            try
            {
                var ret = Duels?.Call( "inDuel", player );
                bool result = ret != null && (bool) ret;
                return result;
            }
            catch
            {
                return false;
            }
        }

        bool InRaid( BasePlayer player )
        {
            if (NoEscape == null) return false;
            try
            {
                double res = (double) NoEscape.Call( "ApiGetTime", player.userID );
                return Math.Abs(res) > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion
        
        string canDeposit( BasePlayer player )
        {
            if (InDuel( player ))
                return "Нельзя!!! Вы находитесь на дуэли!";
            if (InEvent( player ))
                return "Нельзя!!! Вы находитесь на ивенте!";
            if (InRaid( player ))
                return "Нельзя!!! Вы находитесь на рейде!";

            if (!player.CanBuild())
                return "Нельзя!!! Вас бьёт шкаф) !";
            if (player.IsSwimming())
                return "Нельзя!!! Вы на море) !";
            if (player.IsFlying)
                return "Нельзя!!! Вы левитируете!";
            if (!player.IsOnGround())
                return "Нельзя!!! Вы где-то застряли!";
            if (player.IsWounded())
                return "Нельзя!!! Вас кто-то шлёпнул) !";
            return Interface.Call("CanDeposit") as string;
        }
        

        void Wait( Func<bool> expr, Action callback, float timeout = 0.1f )
        {
            if (expr.Invoke())
            {
                timer.Once(0.1f, callback.Invoke);
                return;
            }
            timer.Once( timeout, () => Wait( expr, callback, timeout ) );
        }

        void GetAvatar( ulong uid, Action<string> callback )
        {
            string url = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=443947CEA9A0CC4F4868BFD1AA33E972&" +
                         "steamids=" + uid;
            webrequest.EnqueueGet( url, ( i, json ) =>
            {
                callback?.Invoke( (string) JObject.Parse( json )[ "response" ][ "players" ][ 0 ][ "avatarmedium" ] );
            }, this );
        }

        
        #region Nested type: TradeController

        class DepositController : MonoBehaviour
        {
            public string guid;
            public ShopFront shop;
            private bool complete = false;
            public BasePlayer player1, player2;

            public void Init( string guid, BasePlayer player1, BasePlayer player2 )
            {
                this.guid = guid;
                this.player1 = player1;
                this.player2 = player2;
            }


            private void Awake()
            {
                shop = GetComponent<ShopFront>();
            }

            public void Complete()
            {
                complete = true;
            }
        }

        #endregion

        public class Competition
        {
            public HashSet<BasePlayer> Players = new HashSet<BasePlayer>();
            public Dictionary<BasePlayer, List<Item>> Bets;



            public void Reward(BasePlayer player)
            {
                var list = Bets.Values.ToList();
                var items = new List<Item>().Concat( list.SelectMany( x => x ) ).ToList();
                foreach (var item in items)
                {
                    item.MoveToContainer( player.inventory.containerMain );
                }
                Bets.Clear();
                Players.Clear();
            }

            public void ReturnBet(BasePlayer player)
            {
                m_Instance.Puts("Возвращение ставки игроку "+player.displayName);
                var bet = Bets[player];
                foreach (var item in bet)
                {
                     m_Instance.Puts(item.info.shortname+" "+item.amount);
                    item.MoveToContainer(player.inventory.containerMain);
                }
                Bets.Remove(player);
                Players.Remove( player );
            }
        }
    }
}
