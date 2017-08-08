using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facepunch.Network;
using Network;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Facepunch;
using Network.Visibility;

namespace RustyCore.Utils
{

    public static class TradeBox
    {
        private static Dictionary<string, ShopFront> boxes = new Dictionary<string, ShopFront>();
        
        private static Dictionary<string, List<BasePlayer>> players = new Dictionary<string, List<BasePlayer>>();

        


        static TradeBox()
        {
            Plugins.BaseCore.GetCmd().AddConsoleCommand("tradebox.button", null, arg =>{ cmdTradeButton(arg); return false; });
        }

        public static string Create( BasePlayer player1, BasePlayer player2 )
        {
            var ent = GameManager.server.CreateEntity(
                "assets/prefabs/building/wall.frame.shopfront/wall.frame.shopfront.metal.prefab" );
            ent.transform.position = Vector3.zero;


            ent.Spawn();
            var shop = (ShopFront) ent;

            shop.vendorInventory.capacity /= 2;
            shop.customerInventory.capacity /= 2;

            var guid = CuiHelper.GetGuid();

            boxes.Add( guid, shop );
            players[ guid ] = new List<BasePlayer>() { player1, player2 };

            if (!player1.net.subscriber.IsSubscribed( shop.net.@group ))
            {
                player1.net.subscriber.Subscribe( shop.net.@group );
            }
            if (!player2.net.subscriber.IsSubscribed( shop.net.@group ))
            {
                player2.net.subscriber.Subscribe( shop.net.@group );
            }

            SendEntity( player1, shop );
            SendEntity( player2, shop );
            SendEntity( player1, player2 );
            SendEntity( player2, player1 );

            RustyCore.Plugins.BaseCore.GetTimer().Once( 0.1f, () =>
            {
                StartLooting( guid, player1 );
            } );
            RustyCore.Plugins.BaseCore.GetTimer().Once( 0.5f, () =>
            {
                StartLooting( guid, player2 );
            } );
            Logger.Warning($"CreateBox {guid}/{shop.net.ID}");
            return guid;
        }

        private static void cmdTradeButton(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var shop = boxes.Select(p=>p.Value).FirstOrDefault(p => p.customerPlayer == player || p.vendorPlayer == player);
            if (shop == null) return;

            if (shop.HasFlag( BaseEntity.Flags.Reserved3 )) return;
            if (!shop.IsTradingPlayer( player ))
            {
                return;
            }
            if (shop.vendorPlayer == null || shop.customerPlayer == null)
            {
                return;
            }
            if (shop.IsPlayerVendor( player ))
            {
                if (shop.HasFlag(BaseEntity.Flags.Reserved1))
                {
                    shop.ResetTrade();
                }
                else
                {
                    shop.SetFlag(BaseEntity.Flags.Reserved1, true);
                    shop.vendorInventory.SetLocked(true);
                }
            }
            else if (shop.IsPlayerCustomer(player))
            {
                if (shop.HasFlag(BaseEntity.Flags.Reserved2))
                {
                    shop.ResetTrade();
                }
                else
                {
                    shop.SetFlag(BaseEntity.Flags.Reserved2, true);
                    shop.customerInventory.SetLocked(true);
                }
            }
            if (shop.HasFlag( BaseEntity.Flags.Reserved1 ) && shop.HasFlag( BaseEntity.Flags.Reserved2 ))
            {
                Logger.Warning( $"TradeAccepted {shop.net.ID}" );
                shop.SetFlag( BaseEntity.Flags.Reserved3, true );
                shop.Invoke( shop.CompleteTrade, 2f );
            }
        }

        public static void Destroy( string guid )
        {
            ShopFront shop;
            if (boxes.TryGetValue( guid, out shop ))
            {
                Logger.Warning( $"TradeDestroy {shop.net.ID}" );
                if (players.ContainsKey( guid ))
                {
                    players[ guid ].ForEach( p =>
                    {
                        Plugins.BaseCore.GetCore().DestroyUI( p, "TradeBox", "button" );
                    } );
                    players.Remove( guid );
                }
                boxes.Remove( guid );
                shop.Kill();
            }
        }

        public static void StartLooting( string guid, BasePlayer player )
        {
            ShopFront shop;
            if (boxes.TryGetValue( guid, out shop ))
            {
                player.inventory.loot.StartLootingEntity( shop, false );
                player.inventory.loot.AddContainer( shop.vendorInventory );
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer( null, player, "RPC_OpenLootPanel", "shopfront", null, null, null, null );

                shop.DecayTouch();
                shop.SendNetworkUpdate();

                player.inventory.loot.AddContainer( shop.customerInventory );
                player.inventory.loot.SendImmediate();

                if (shop.customerPlayer == null)
                    shop.customerPlayer = player;
                else shop.vendorPlayer = player;

                Plugins.BaseCore.GetCore().DrawUI(player, "TradeBox", "button");
                try
                {
                shop.ResetTrade();

                }
                catch (Exception ex)
                {
                   Logger.Error(ex.Message+Environment.NewLine+ex.StackTrace);
                }
                shop.UpdatePlayers();
            }
        }

        public static T AddComponent<T>( string guid ) where T : Component
        {
            ShopFront shop;
            if (!boxes.TryGetValue( guid, out shop ))
            {
                throw new InvalidOperationException( $"AddBehaviour: TradeBox for {guid} not Found" );
            }
            return shop.gameObject.AddComponent<T>();
        }

        static void SendEntity( BasePlayer player, BaseEntity ent )
        {
            if (Net.sv.write.Start())
            {
                player.net.connection.validate.entityUpdates++;
                BaseNetworkable.SaveInfo saveInfo = Pool.Get<BaseNetworkable.SaveInfo>();
                saveInfo.forConnection = player.net.connection;
                saveInfo.forDisk = false;
                Net.sv.write.PacketID( Message.Type.Entities );
                Net.sv.write.UInt32( player.net.connection.validate.entityUpdates );
                ent.ToStreamForNetwork( Net.sv.write, saveInfo );
                Net.sv.write.Send( new SendInfo( player.net.connection ) );
                Pool.Free<BaseNetworkable.SaveInfo>( ref saveInfo );
            }
        }
    }
}
