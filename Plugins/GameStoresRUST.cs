using System.Collections.Generic;
using System;
using System.Reflection;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System.Linq;
using System.Text;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Network;
using Oxide.Core;
using System.Collections;
using Oxide.Plugins;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info( "GameStores", "Sstine", "1.6.1", ResourceId = 715 )]
    class GameStoresRUST : RustPlugin
    {
        public string Request => $"http://gamestores.ru/api/?shop_id={Config[ "SHOP.ID" ]}&secret={Config[ "SECRET.KEY" ]}&server={Config[ "SERVER.ID" ]}";
        private List<Dictionary<string, object>> Stats = new List<Dictionary<string, object>>();
        private List<string> Requests = new List<string>();

        #region [Override] Load default configurations
        protected override void LoadDefaultConfig()
        {
            Config[ "SHOP.ID" ] = "0";
            Config[ "SERVER.ID" ] = "0";
            Config[ "SECRET.KEY" ] = "KEY";
            Config[ "BUCKET.IMG" ] = "http://gamestores.ru/img/plugin_new.png";
            Config[ "ITEMS.SPLIT" ] = false;
            Config[ "COMMAND.TOP" ] = false;
            Config[ "UI.ENABLED" ] = false;
            Config[ "TOP.USERS" ] = false;
            Config[ "BUCKET.BUTTON" ] = false;
        }
        #endregion

        #region [HookMethod] [Unload]
        private void Unload()
        {
            if (System.Convert.ToBoolean( Config[ "UI.ENABLED" ] ) && BasePlayer.activePlayerList.Count > 0)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi( player, "ui.store.buttonimage" );
                    CuiHelper.DestroyUi( player, "ui.store.button" );
                    DestroyUI( player );
                }
        }
        #endregion

        #region[Variables]
        private Dictionary<BasePlayer, int> Items = new Dictionary<BasePlayer, int>();
        private Dictionary<BasePlayer, int> Index = new Dictionary<BasePlayer, int>();
        string shopLink = string.Empty;
        #endregion

        #region [HookMethod] On server intitialized
        private void OnServerInitialized()
        {
            if (Config[ "SECRET.KEY" ].ToString().Contains( "KEY" ))
            {
                Debug.LogError( "Plugin isn't configured" );
            }
            else
            {
                if (System.Convert.ToBoolean( Config[ "UI.ENABLED" ] ))
                {
                    webrequest.EnqueueGet( $"{this.Request}&info=true", ( code, response ) =>
                    {
                        switch (code)
                        {
                            case 0:
                                Debug.LogError( "Api does not responded to a request" );
                                break;
                            case 200:
                                Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>( response, new KeyValuesConverter() );
                                Dictionary<string, object> data = ( Response[ "data" ] as Dictionary<string, object> );
                                shopLink = $"{data[ "link" ]}";
                                break;
                            case 404:
                                Debug.LogError( "Response code: 404, please check your configurations" );
                                break;
                        }

                    }, this );
                }
            }

        }
        #endregion

        #region[HookMethod] OnPlayerSleepEnded
        void OnPlayerSleepEnded( BasePlayer player )
        {
            if (System.Convert.ToBoolean( Config[ "UI.ENABLED" ] ))
            {
                if (System.Convert.ToBoolean( Config[ "BUCKET.BUTTON" ] ))
                {
                    string Image = Config[ "BUCKET.IMG" ].ToString();

                    CuiElementContainer UI = new CuiElementContainer();
                    UI.Add( new CuiElement()
                    {
                        Parent = "Hud",
                        Name = "ui.store.buttonimage",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Sprite = "assets/icons/loot.png",
                                Color = "1 1 1 1"
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0.01 0.955",
                                AnchorMax = "0.035 0.99"
                            }
                        }
                    } );
                    UI.Add( new CuiButton()
                    {
                        Button =
                        {
                            Command = $"ui.store {player.userID}",
                            Color = "0 0 0 0"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.01 0.95",
                            AnchorMax = "0.04 0.99"
                        },
                        Text =
                        {
                            Text = ""
                        }
                    }, "Hud", "ui.store.button" );
                    CuiHelper.DestroyUi( player, "ui.store.buttonimage" );
                    CuiHelper.DestroyUi( player, "ui.store.button" );
                    CuiHelper.AddUi( player, UI );
                }
            }
        }
        #endregion

        #region[Method] Executing - WebRequest callback handler
        private void Executing( BasePlayer Player, string response, int code )
        {
            switch (code)
            {
                case 0:
                    Debug.LogError( "Api does not responded to a request" );
                    Player.ChatMessage( "Корзина недоступна. Попробуйте позже" );
                    break;
                case 200:
                    Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>( response, new KeyValuesConverter() );
                    if (Response != null && response != null && response != "null")
                    {
                        CuiElementContainer UI = new CuiElementContainer();
                        switch (System.Convert.ToInt32( Response[ "code" ] ))
                        {
                            case 100:
                                List<object> data = Response[ "data" ] as List<object>;
                                DestroyUI( Player );

                                if (System.Convert.ToBoolean( Config[ "UI.ENABLED" ] ))
                                {
                                    #region[Panel] Parent
                                    UI.Add( new CuiPanel()
                                    {
                                        Image =
                                        {
                                            Color = "0 0 0 0.95"
                                        },
                                        RectTransform =
                                        {
                                            AnchorMin = "0.20 0.18",
                                            AnchorMax = "0.80 0.95"
                                        },
                                        CursorEnabled = true
                                    }, "Overlay", "ui.store" );
                                    #endregion

                                    int index = 0;
                                    Items.Add( Player, data.Count );
                                    Index.Add( Player, data.Count > 14 ? 14 : data.Count );
                                    for (int r = 0; r < 4; r++)
                                    {
                                        for (int i = 0; i < ( r > 1 ? 2 : 5 ); i++)
                                        {
                                            #region[Panel] Backgroud
                                            UI.Add( new CuiPanel()
                                            {
                                                Image =
                                                {
                                                    Color = "0.1 0.1 0.1 1"
                                                },
                                                RectTransform =
                                                {
                                                    AnchorMin = $"{0.04f + (0.19 * i)} {0.75f - (r * 0.23f)}",
                                                    AnchorMax = $"{0.19f + ((0.19f * i) - (i == 4 ? 0f : 0f))} {0.95f - (r * 0.23f)}"
                                                }

                                            }, "ui.store", $"ui.background{index}" );
                                            #endregion
                                            if (index < data.Count)
                                            {
                                                Dictionary<string, object> itemdata = data[ index ] as Dictionary<string, object>;

                                                int ItemID = System.Convert.ToInt32( itemdata[ "item_id" ] );
                                                int Amount = System.Convert.ToInt32( itemdata[ "amount" ] );
                                                string Image = $"{itemdata[ "img" ]}";


                                                #region[Element] ImgBlock                                                                                      
                                                UI.Add( new CuiElement()
                                                {
                                                    Name = $"ui.block{index}",
                                                    Parent = $"ui.background{index}",
                                                    Components =
                                                    {
                                                        new CuiRawImageComponent
                                                        {
                                                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                                            Url = Image
                                                        },
                                                        new CuiRectTransformComponent
                                                        {
                                                            AnchorMin = $"0.10 0.10",
                                                            AnchorMax = $"0.90 0.90"
                                                        },
                                                        new CuiOutlineComponent
                                                        {
                                                            Distance = "1.0 1.0",
                                                            Color = "0.0 0.0 0.0 1.0"
                                                        }
                                                    }
                                                } );
                                                #endregion

                                                #region[Label] Amount
                                                UI.Add( new CuiLabel()
                                                {
                                                    RectTransform =
                                                    {
                                                        AnchorMin = $"0.0 0.0",
                                                        AnchorMax = $"1.0 0.90"
                                                    },
                                                    Text =
                                                    {
                                                        Text = $"{Amount} шт. ",
                                                        FontSize = 14,
                                                        Align = TextAnchor.LowerRight,
                                                        Color = "1 1 1 1"
                                                    }
                                                }, $"ui.background{index}", $"ui.amount{index}" );
                                                #endregion

                                                #region[ItemName] Product
                                                UI.Add( new CuiLabel()
                                                {
                                                    RectTransform =
                                                    {
                                                        AnchorMin = $"0.05 0.01",
                                                        AnchorMax = $"0.99 0.99"
                                                    },
                                                    Text =
                                                    {
                                                        Text = $"{itemdata["name"]}",
                                                        FontSize = 14,
                                                        Align = TextAnchor.UpperLeft,
                                                        Color = "1 1 1 1"
                                                    }
                                                }, $"ui.background{index}", $"ui.product{index}" );
                                                #endregion

                                                #region[Button] Take
                                                UI.Add( new CuiButton
                                                {
                                                    Button =
                                                    {
                                                        Command = $"ui.gives {Player.userID} {index} {itemdata["id"]}",
                                                        Color = "0 0 0 0"
                                                    },
                                                    RectTransform =
                                                    {
                                                        AnchorMin = $"0.0 0.0",
                                                        AnchorMax = $"1.0 1.0"
                                                    },
                                                    Text =
                                                    {
                                                        Text = ""
                                                    }
                                                }, $"ui.background{index}", $"ui.command.take{index}" );
                                                #endregion

                                            }
                                            index++;
                                        }
                                    }

                                    #region[Button] Close
                                    UI.Add( new CuiButton
                                    {
                                        Button =
                                        {
                                            Command = $"ui.destroy {Player.userID}",
                                            Color = "0.1 0.1 0.1 0"
                                        },
                                        RectTransform =
                                        {
                                            AnchorMin = "0.960 0.94",
                                            AnchorMax = "0.999 0.998"
                                        },
                                        Text =
                                        {
                                            Color = "0.9 0.9 0.9 1",
                                            Text = "X",
                                            FontSize = 15,
                                            Align = TextAnchor.MiddleCenter
                                        }
                                    }, "ui.store", "ui.close" );
                                    #endregion

                                    #region[Button] Back
                                    UI.Add( new CuiButton
                                    {
                                        Button =
                                        {
                                            Command = $"ui.back {Player.UserIDString}",
                                            Color = "0.1 0.1 0.1 1"
                                        },
                                        RectTransform =
                                        {
                                            AnchorMin = $"0.42 0.39",
                                            AnchorMax = $"0.65 0.49"
                                        },
                                        Text =
                                        {
                                            Text = "Назад",
                                            Color = "1 1 1 1",
                                            Align = TextAnchor.MiddleCenter,
                                            FontSize = 20
                                        }
                                    }, "ui.store", $"ui.back" );
                                    #endregion

                                    #region[Button] Next
                                    UI.Add( new CuiButton
                                    {
                                        Button =
                                        {
                                            Command = $"ui.next {Player.UserIDString}",
                                            Color = "0.1 0.1 0.1 1"
                                        },
                                        RectTransform =
                                        {
                                            AnchorMin = $"0.70 0.39",
                                            AnchorMax = $"0.95 0.49"
                                        },
                                        Text =
                                        {
                                            Text = "Вперёд",
                                            Color = "1 1 1 1",
                                            Align = TextAnchor.MiddleCenter,
                                            FontSize = 20
                                        }
                                    }, "ui.store", $"ui.next" );
                                    #endregion

                                    #region[Button] TakeAll
                                    UI.Add( new CuiButton
                                    {
                                        Button =
                                        {
                                            Command = $"ui.takeall {Player.UserIDString}",
                                            Color = "0.1 0.1 0.1 1"
                                        },
                                        RectTransform =
                                        {
                                            AnchorMin = $"0.42 0.25",
                                            AnchorMax = $"0.95 0.35"
                                        },
                                        Text =
                                        {
                                            Text = "Забрать всё",
                                            Color = "1 1 1 1",
                                            Align = TextAnchor.MiddleCenter,
                                            FontSize = 20
                                        }
                                    }, "ui.store", $"ui.command.takeall" );
                                    #endregion

                                    #region[Label] shopLink
                                    UI.Add( new CuiLabel()
                                    {
                                        RectTransform =
                                        {
                                            AnchorMin = $"0.42 0.15",
                                            AnchorMax = $"0.95 0.25"
                                        },
                                        Text =
                                        {
                                            Text = "Магазин",
                                            Align = TextAnchor.MiddleCenter,
                                            Color = "1 1 1 1",
                                            FontSize = 40
                                        }
                                    }, "ui.store", "ui.link" );
                                    #endregion

                                    #region[Label] shopLink
                                    UI.Add( new CuiLabel()
                                    {
                                        RectTransform =
                                        {
                                            AnchorMin = $"0.42 0.05",
                                            AnchorMax = $"0.95 0.15"
                                        },
                                        Text =
                                        {
                                            Text = shopLink,
                                            Align = TextAnchor.MiddleCenter,
                                            Color = "1 1 1 1",
                                            FontSize = 35
                                        }
                                    }, "ui.store", "ui.link" );
                                    #endregion

                                    CuiHelper.AddUi( Player, UI );
                                    return;
                                }

                                #region [UI OFF] Give Items If UI Off
                                foreach (object pair in data)
                                {
                                    Dictionary<string, object> iteminfo = pair as Dictionary<string, object>;

                                    if (iteminfo.ContainsKey( "command" ))
                                    {
                                        string command = iteminfo[ "command" ].ToString().ToLower().Replace( '\n', '|' ).Replace( "%steamid%", Player.UserIDString ).Replace( "%username%", Player.displayName );
                                        String[] CommandArray = command.Split( '|' );
                                        foreach (var substring in CommandArray)
                                        {
                                            //ConsoleSystem.Run.Server.Normal(substring);
                                            ConsoleSystem.Run( ConsoleSystem.Option.Server, substring );
                                        }
                                        Player.ChatMessage( $"Получен товар из магазина: <color=lime>\"{iteminfo[ "name" ]}\"</color>" );
                                        SendResult( new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{iteminfo[ "id" ]}" } } );
                                        break;
                                    }

                                    int ItemID = System.Convert.ToInt32( iteminfo[ "item_id" ] );
                                    int Amount = System.Convert.ToInt32( iteminfo[ "amount" ] );
                                    Item Item = ItemManager.CreateByItemID( ItemID, Amount );

                                    if (CanTake( Player, Item ) >= Amount)
                                    {
                                        if (System.Convert.ToBoolean( Config[ "ITEMS.SPLIT" ] ))
                                        {
                                            List<Item> Items = SplitItem( Item );

                                            foreach (Item item in Items)
                                            {
                                                Player.inventory.GiveItem( ItemManager.CreateByItemID( Item.info.itemid, item.amount ), Player.inventory.containerMain );
                                            }
                                        }
                                        else
                                        {
                                            Player.inventory.GiveItem( Item, Player.inventory.containerMain );
                                        }

                                        Player.ChatMessage( $"Получен товар из магазина: <color=lime>\"{iteminfo[ "name" ]}\"</color> в количестве <color=lime>{Amount}</color> шт." );
                                        SendGived( new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{iteminfo[ "id" ]}" } }, Player );
                                    }
                                    else
                                        Player.ChatMessage( $"В инвентаре недостаточно места для получения <color=lime>\"{iteminfo[ "name" ]}\"</color>" );

                                }
                                #endregion
                                break;
                            case 104:
                                if (System.Convert.ToBoolean( Config[ "UI.ENABLED" ] ))
                                {
                                    #region[Panel] Parent

                                    UI.Add( new CuiPanel()
                                    {
                                        Image =
                                        {
                                            Color = "0 0 0 0.95"
                                        },
                                        RectTransform =
                                        {
                                            AnchorMin = "0.20 0.20",
                                            AnchorMax = "0.80 0.85"
                                        },
                                        CursorEnabled = true
                                    }, "Overlay", "ui.store" );
                                    #endregion

                                    UI.Add( new CuiLabel()
                                    {
                                        RectTransform =
                                        {
                                            AnchorMin = "0.01 0.01",
                                            AnchorMax = "0.99 0.50"
                                        },
                                        Text =
                                        {
                                            Text = "Ваша корзина пуста!",
                                            Align = TextAnchor.UpperCenter,
                                            Color = "1 1 1 1",
                                            FontSize = 25
                                        }
                                    }, "ui.store", "ui.noitems" );

                                    UI.Add( new CuiLabel()
                                    {
                                        RectTransform =
                                        {
                                            AnchorMin = "0.01 0.01",
                                            AnchorMax = "0.98 0.07"
                                        },
                                        Text =
                                        {
                                            Text = shopLink,
                                            Align = TextAnchor.UpperRight,
                                            Color = "1 1 1 1",
                                            FontSize = 25
                                        }
                                    }, "ui.store", "ui.link" );

                                    #region[Button] Close
                                    UI.Add( new CuiButton
                                    {
                                        Button =
                                        {
                                            Command = $"ui.destroy {Player.userID}",
                                            Color = "0.1 0.1 0.1 0"
                                        },
                                        RectTransform =
                                        {
                                            AnchorMin = "0.960 0.94",
                                            AnchorMax = "0.999 0.998"
                                        },
                                        Text =
                                        {
                                            Color = "0.9 0.9 0.9 1",
                                            Text = "X",
                                            FontSize = 15,
                                            Align = TextAnchor.MiddleCenter
                                        }
                                    }, "ui.store", "ui.close" );
                                    #endregion

                                    CuiHelper.AddUi( Player, UI );
                                    return;
                                }
                                Player.ChatMessage( $"Ваша корзина пуста!" );
                                break;
                        }
                    }
                    else
                        Debug.LogWarning( response );
                    break;
                case 404:
                    Debug.LogError( "Response code: 404, please check your configurations" );
                    break;
            }
        }
        #endregion

        #region [Method] SendResult - Send WebRequest result
        private void SendResult( Dictionary<string, string> Args ) => SendRequest( Args );
        #endregion

        #region[Method] SendRequest - Send request to GameStore API
        private void SendRequest( Dictionary<string, string> Args, BasePlayer Player = null, bool exec = true )
        {
            string Request = $"{this.Request}&{string.Join( "&", Args.Select( x => x.Key + "=" + x.Value ).ToArray() )}";
            webrequest.EnqueueGet( Request, ( code, res ) => { if (Player != null && exec) Executing( Player, res, code ); }, this );
        }
        #endregion        

        #region[Method] SendGived - Send request about givint item to GameStore API
        private void SendGived( Dictionary<string, string> Args, BasePlayer Player = null )
        {
            string Request = $"{this.Request}&{string.Join( "&", Args.Select( x => x.Key + "=" + x.Value ).ToArray() )}";
            webrequest.EnqueueGet( Request, ( code, res ) => { if (Player != null) TestRequestSent( Player, res, code, Args ); }, this );
        }
        #endregion     

        #region[Method] TestRequestSent - Check send request
        private void TestRequestSent( BasePlayer Player, string response, int code, Dictionary<string, string> Args )
        {
            if (code == 200)
            {
                Dictionary<string, object> Resp = JsonConvert.DeserializeObject<Dictionary<string, object>>( response, new KeyValuesConverter() );
                if (Resp[ "result" ].ToString() != "success")
                {
                    Debug.LogError( "Api do not responded to request. Trying again (Player received items but it was not recorded)" );
                    SendGived( Args, Player );
                }
            }
            else
            {
                Debug.LogError( "Api do not responded to request. Trying again (Player received items but it was not recorded)" );
                SendGived( Args, Player );
            }
        }
        #endregion

        #region[ChatCommand] /store
        [ChatCommand( "store" )]
        private void cmdStore( BasePlayer Player, string command, string[] args )
        {
            SendRequest( new Dictionary<string, string>() { { "items", "true" }, { "steam_id", $"{Player.UserIDString}" } }, Player );
        }
        #endregion

        #region[ChatCommand] /store
        [ConsoleCommand( "ui.store" )]
        private void cmdUiStore( ConsoleSystem.Arg Args )
        {
            BasePlayer Player = BasePlayer.FindByID( System.Convert.ToUInt64( Args.Args[ 0 ] ) );
            DestroyUI( Player );
            SendRequest( new Dictionary<string, string>() { { "items", "true" }, { "steam_id", $"{Player.UserIDString}" } }, Player );
        }
        #endregion

        #region[ConsoleCommand] ui.takeall
        [ConsoleCommand( "ui.takeall" )]
        private void cmdTakeAll( ConsoleSystem.Arg Args )
        {
            BasePlayer Player = BasePlayer.FindByID( System.Convert.ToUInt64( Args.Args[ 0 ] ) );

            if (Player != null)
            {
                webrequest.EnqueueGet( $"{Request}&items=true&steam_id={Player.UserIDString}", ( code, response ) =>
                {
                    switch (code)
                    {
                        case 0:
                            Debug.LogError( "Api does not responded to a request" );
                            break;
                        case 200:
                            Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>( response, new KeyValuesConverter() );
                            if (!Response.ContainsKey( "data" ))
                                return;
                            List<object> data = Response[ "data" ] as List<object>;
                            if (data == null || Response == null)
                                return;

                            if (data.Count() > 14)
                            {
                                Player.ChatMessage( $"Вы не можете забрать больше 14 предметов за раз." );
                                return;
                            }

                            if (data.Count() < 1)
                            {
                                return;
                            }
                            int i = data.Count() - 1;
                            foreach (object pair in data)
                            {
                                Dictionary<string, object> iteminfo = pair as Dictionary<string, object>;
                                List<string> Arguments = new List<string>() { { $"{Args.Args[ 0 ]}" }, { $"{i}" }, { $"{iteminfo[ "id" ]}" } };
                                cmdTakeItem( Arguments );
                                i--;
                            }
                            if (Items[ Player ] < 1)
                            {
                                Items.Remove( Player );
                                Index.Remove( Player );
                            }
                            break;
                        case 404:
                            Debug.LogError( "Response code: 404, please check your configurations" );
                            break;
                    }
                }, this );
            }
        }
        #endregion

        #region[Method] cmdTakeItem
        private void cmdTakeItem( List<string> Args )
        {
            BasePlayer player = BasePlayer.FindByID( System.Convert.ToUInt64( Args[ 0 ] ) );
            if (Requests.Contains( player.UserIDString + Args[ 1 ].ToString() ))
            {
                player.ChatMessage( $"Дождитесь завершения предведущего запроса" );
                return;
            }
            Requests.Add( player.UserIDString + Args[ 1 ].ToString() );
            webrequest.EnqueueGet( $"{Request}&item=true&steam_id={player.UserIDString}&id={Args[ 2 ]}", ( code, response ) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError( "Api does not responded to a request" );
                        break;
                    case 200:
                        Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>( response, new KeyValuesConverter() );
                        if (!Response.ContainsKey( "data" ))
                        {
                            if (Requests.Contains( player.UserIDString + Args[ 1 ].ToString() ))
                                Requests.Remove( player.UserIDString + Args[ 1 ].ToString() );
                            return;
                        }
                        Dictionary<string, object> data = Response[ "data" ] as Dictionary<string, object>; ;
                        if (data[ "type" ].ToString() == "item")
                        {
                            Item Item = ItemManager.CreateByItemID( System.Convert.ToInt32( data[ "item_id" ] ), System.Convert.ToInt32( data[ "amount" ] ) );

                            if (( System.Convert.ToBoolean( Config[ "ITEMS.SPLIT" ] ) && CanTake( player, Item ) >= Item.amount ) || ( !System.Convert.ToBoolean( Config[ "ITEMS.SPLIT" ] ) && ( !player.inventory.containerMain.IsFull() || !player.inventory.containerBelt.IsFull() ) ))
                            {
                                SendGived( new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args[ 2 ]}" } }, player );
                                CuiHelper.DestroyUi( player, $"ui.block{Args[ 1 ]}" );
                                CuiHelper.DestroyUi( player, $"ui.amount{Args[ 1 ]}" );
                                CuiHelper.DestroyUi( player, $"ui.product{Args[ 1 ]}" );
                                CuiHelper.DestroyUi( player, $"ui.command.take{Args[ 1 ]}" );

                                if (System.Convert.ToBoolean( Config[ "ITEMS.SPLIT" ] ))
                                {
                                    List<Item> Items = SplitItem( Item );

                                    foreach (Item item in Items)
                                    {
                                        player.inventory.GiveItem( ItemManager.CreateByItemID( Item.info.itemid, item.amount ), player.inventory.containerMain );
                                        player.ChatMessage( $"Получен предмет из магазина: <color=lime>\"{data[ "name" ]}\"</color> в количестве <color=lime>{Item.amount}</color> шт." );
                                    }
                                }
                                else
                                {
                                    player.inventory.GiveItem( Item, player.inventory.containerMain );
                                    player.ChatMessage( $"Получен предмет из магазина: <color=lime>\"{data[ "name" ]}\"</color> в количестве <color=lime>{data[ "amount" ]}</color> шт." );
                                }

                                if (Index[ player ] < 14)
                                    Index[ player ] -= 1;
                                Items[ player ] -= 1;

                                if (Items[ player ] < 1)
                                {
                                    Items.Remove( player );
                                    Index.Remove( player );
                                }
                            }
                            else
                            {
                                player.ChatMessage( $"В инвентаре недостаточно места для получения <color=lime>\"{Item.info.displayName.english}\"</color>" );
                            }
                        }
                        else if (data[ "type" ].ToString() == "command")
                        {
                            string command = data[ "command" ].ToString().Replace( '\n', '|' ).ToLower().Trim( '\"' ).Replace( "%steamid%", player.UserIDString ).Replace( "%username%", player.displayName );
                            String[] CommandArray = command.Split( '|' );
                            foreach (var substring in CommandArray)
                            {
                                //ConsoleSystem.Run.Server.Normal(substring);
                                ConsoleSystem.Run( ConsoleSystem.Option.Server, substring );
                            }

                            player.ChatMessage( $"Получен предмет из магазина: <color=lime>\"{data[ "name" ]}\"</color>" );
                            CuiHelper.DestroyUi( player, $"ui.block{Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.amount{Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.product{Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.command.take{Args[ 1 ]}" );
                            SendGived( new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args[ 2 ]}" } }, player );

                            if (Index[ player ] < 14)
                                Index[ player ] -= 1;
                            Items[ player ] -= 1;

                            if (Items[ player ] < 1)
                            {
                                Items.Remove( player );
                                Index.Remove( player );
                            }

                        }
                        Requests.Remove( player.UserIDString + Args[ 1 ].ToString() );
                        break;
                    case 404:
                        Requests.Remove( player.UserIDString + Args[ 1 ].ToString() );
                        Debug.LogError( "Response code: 404, please check your configurations" );
                        break;
                    default:
                        Debug.LogError( "Api does not responded to a request" );
                        break;
                }
            }, this );

        }
        #endregion

        #region[ConsoleCommand] ui.gives
        [ConsoleCommand( "ui.gives" )]
        private void cmdDestroyItem( ConsoleSystem.Arg Args )
        {
            BasePlayer player = BasePlayer.FindByID( System.Convert.ToUInt64( Args.Args[ 0 ] ) );
            if (Requests.Contains( player.UserIDString + Args.Args[ 1 ].ToString() ))
            {
                player.ChatMessage( $"Дождитесь завершения предведущего запроса" );
                return;
            }
            Requests.Add( player.UserIDString + Args.Args[ 1 ].ToString() );
            webrequest.EnqueueGet( $"{Request}&item=true&steam_id={player.UserIDString}&id={Args.Args[ 2 ]}", ( code, response ) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError( "Api does not responded to a request" );
                        break;
                    case 200:
                        Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>( response, new KeyValuesConverter() );
                        if (!Response.ContainsKey( "data" ))
                        {
                            player.ChatMessage( $"Предмет не найден" );
                            CuiHelper.DestroyUi( player, $"ui.block{Args.Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.amount{Args.Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.product{Args.Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.command.take{Args.Args[ 1 ]}" );
                            Requests.Remove( player.UserIDString + Args.Args[ 1 ].ToString() );
                            return;
                        }
                        Dictionary<string, object> data = Response[ "data" ] as Dictionary<string, object>;
                        if (data[ "type" ].ToString() == "item")
                        {
                            Item Item = ItemManager.CreateByItemID( System.Convert.ToInt32( data[ "item_id" ] ), System.Convert.ToInt32( data[ "amount" ] ) );

                            if (( System.Convert.ToBoolean( Config[ "ITEMS.SPLIT" ] ) && CanTake( player, Item ) >= Item.amount ) || ( !System.Convert.ToBoolean( Config[ "ITEMS.SPLIT" ] ) && ( !player.inventory.containerMain.IsFull() || !player.inventory.containerBelt.IsFull() ) ))
                            {

                                SendGived( new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args.Args[ 2 ]}" } }, player );
                                CuiHelper.DestroyUi( player, $"ui.block{Args.Args[ 1 ]}" );
                                CuiHelper.DestroyUi( player, $"ui.amount{Args.Args[ 1 ]}" );
                                CuiHelper.DestroyUi( player, $"ui.product{Args.Args[ 1 ]}" );
                                CuiHelper.DestroyUi( player, $"ui.command.take{Args.Args[ 1 ]}" );

                                if (System.Convert.ToBoolean( Config[ "ITEMS.SPLIT" ] ))
                                {
                                    List<Item> Items = SplitItem( Item );

                                    foreach (Item item in Items)
                                    {
                                        player.inventory.GiveItem( ItemManager.CreateByItemID( Item.info.itemid, item.amount ), player.inventory.containerMain );
                                        player.ChatMessage( $"Получен предмет из магазина: <color=lime>\"{data[ "name" ]}\"</color> в количестве <color=lime>{Item.amount}</color> шт." );
                                    }
                                }
                                else
                                {
                                    player.inventory.GiveItem( Item, player.inventory.containerMain );
                                    player.ChatMessage( $"Получен предмет из магазина: <color=lime>\"{data[ "name" ]}\"</color> в количестве <color=lime>{data[ "amount" ]}</color> шт." );
                                }

                                if (Index[ player ] < 14)
                                    Index[ player ] -= 1;
                                Items[ player ] -= 1;

                                if (Items[ player ] < 1)
                                {
                                    Items.Remove( player );
                                    Index.Remove( player );
                                }
                            }
                            else
                                player.ChatMessage( $"В инвентаре недостаточно места для получения <color=lime>\"{Item.info.displayName.english}\"</color>" );
                        }
                        else if (data[ "type" ].ToString() == "command")
                        {
                            string command = data[ "command" ].ToString().Replace( '\n', '|' ).ToLower().Replace( "%steamid%", player.UserIDString ).Replace( "%username%", player.displayName );
                            String[] CommandArray = command.Split( '|' );
                            foreach (var substring in CommandArray)
                            {
                                //ConsoleSystem.Run.Server.Normal(substring);
                                ConsoleSystem.Run( ConsoleSystem.Option.Server, substring );

                            }

                            player.ChatMessage( $"Получен предмет из магазина: <color=lime>\"{data[ "name" ]}\"</color>" );
                            CuiHelper.DestroyUi( player, $"ui.block{Args.Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.amount{Args.Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.product{Args.Args[ 1 ]}" );
                            CuiHelper.DestroyUi( player, $"ui.command.take{Args.Args[ 1 ]}" );
                            SendGived( new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args.Args[ 2 ]}" } }, player );

                            if (Index[ player ] < 14)
                                Index[ player ] -= 1;
                            Items[ player ] -= 1;

                            if (Items[ player ] < 1)
                            {
                                Items.Remove( player );
                                Index.Remove( player );
                            }
                        }
                        Requests.Remove( player.UserIDString + Args.Args[ 1 ].ToString() );
                        break;
                    case 404:
                        Debug.LogError( "Response code: 404, please check your configurations" );
                        break;
                }
                Requests.Remove( player.UserIDString + Args.Args[ 1 ].ToString() );
            }, this );
        }
        #endregion

        #region[ChatCommand] ui.destroy
        [ConsoleCommand( "ui.destroy" )]
        private void cmdUi( ConsoleSystem.Arg Args )
        {
            BasePlayer Player = BasePlayer.FindByID( System.Convert.ToUInt64( Args.Args[ 0 ] ) );
            if (Requests.Contains( Player.UserIDString + "0" ))
            {
                Player.ChatMessage( $"Дождитесь завершения всех запросов" );
                return;
            }
            else
            {
                DestroyUI( Player );
            }
        }
        #endregion

        #region[Method] DestroyUI
        private void DestroyUI( BasePlayer Player )
        {
            for (int i = 0; i < 15; i++)
            {
                CuiHelper.DestroyUi( Player, $"ui.background{i}" );
                CuiHelper.DestroyUi( Player, $"ui.amount{i}" );
                CuiHelper.DestroyUi( Player, $"ui.product{i}" );
                CuiHelper.DestroyUi( Player, $"ui.command.take{i}" );
            }

            Items.Remove( Player );
            Index.Remove( Player );

            CuiHelper.DestroyUi( Player, "ui.close" );
            CuiHelper.DestroyUi( Player, "ui.command.takeall" );
            CuiHelper.DestroyUi( Player, "ui.back" );
            CuiHelper.DestroyUi( Player, "ui.next" );
            CuiHelper.DestroyUi( Player, "ui.store" );
        }
        #endregion

        #region[ConsoleCommand] ui.back
        [ConsoleCommand( "ui.back" )]
        private void cmdUiBack( ConsoleSystem.Arg Args )
        {
            BasePlayer Player = BasePlayer.FindByID( System.Convert.ToUInt64( Args.Args[ 0 ] ) );
            webrequest.EnqueueGet( $"{Request}&items=true&steam_id={Player.UserIDString}", ( code, response ) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError( "Api does not responded to a request" );
                        break;
                    case 200:
                        Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>( response, new KeyValuesConverter() );
                        if (!Response.ContainsKey( "data" ))
                            return;
                        List<object> data = Response[ "data" ] as List<object>;
                        List<object> items = new List<object>();
                        if (data == null || Response == null)
                            return;
                        if (Index[ Player ] <= 14)
                        {
                            return;
                        }
                        else
                        {

                            int Page = System.Convert.ToInt32( System.Math.Ceiling( ( System.Convert.ToSingle( Index[ Player ] ) / 14f ) ) );
                            Index[ Player ] = ( Page - 1 ) * 14;

                            for (int i = 0; i != 14; i++)
                            {
                                CuiHelper.DestroyUi( Player, $"ui.block{i}" );
                                CuiHelper.DestroyUi( Player, $"ui.amount{i}" );
                                CuiHelper.DestroyUi( Player, $"ui.product{i}" );
                                CuiHelper.DestroyUi( Player, $"ui.command.take{i}" );
                                Index[ Player ] -= 1;
                            }

                            for (int i = 0; i != 14 && i < data.Count; i++)
                            {
                                items.Add( data[ Index[ Player ] + i ] );
                            }

                            if (Index[ Player ] < 1)
                                Index[ Player ] = 0;

                            CuiElementContainer UI = new CuiElementContainer();
                            for (int index = 0; index != 14 && index < items.Count; index++)
                            {
                                Dictionary<string, object> itemdata = items[ index ] as Dictionary<string, object>;

                                int ItemID = System.Convert.ToInt32( itemdata[ "item_id" ] );
                                int Amount = System.Convert.ToInt32( itemdata[ "amount" ] );
                                string Image = $"{itemdata[ "img" ]}";

                                #region[Element] ImgBlock                                                                                      
                                UI.Add( new CuiElement()
                                {
                                    Name = $"ui.block{index}",
                                    Parent = $"ui.background{index}",
                                    Components =
                                    {
                                        new CuiRawImageComponent
                                        {
                                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                            Url = Image
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = $"0.10 0.10",
                                            AnchorMax = $"0.90 0.90"
                                        },
                                        new CuiOutlineComponent
                                        {
                                            Distance = "1.0 1.0",
                                            Color = "0.0 0.0 0.0 1.0"
                                        }
                                    }
                                } );
                                #endregion

                                #region[Label] Amount
                                UI.Add( new CuiLabel()
                                {
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.0 0.0",
                                        AnchorMax = $"1.0 0.90"
                                    },
                                    Text =
                                    {
                                        Text = $"{Amount} шт. ",
                                        FontSize = 14,
                                        Align = TextAnchor.LowerRight,
                                        Color = "1 1 1 1"
                                    }
                                }, $"ui.background{index}", $"ui.amount{index}" );
                                #endregion

                                #region[ItemName] Product
                                UI.Add( new CuiLabel()
                                {
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.05 0.01",
                                        AnchorMax = $"0.99 0.99"
                                    },
                                    Text =
                                    {
                                        Text = $"{itemdata["name"]}",
                                        FontSize = 14,
                                        Align = TextAnchor.UpperLeft,
                                        Color = "1 1 1 1"
                                    }
                                }, $"ui.background{index}", $"ui.product{index}" );
                                #endregion

                                #region[Button] Take
                                UI.Add( new CuiButton
                                {
                                    Button =
                                    {
                                        Command = $"ui.gives {Player.userID} {index} {itemdata["id"]}",
                                        Color = "0 0 0 0"
                                    },
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.0 0.0",
                                        AnchorMax = $"1.0 1.0"
                                    },
                                    Text =
                                    {
                                        Text = ""
                                    }
                                }, $"ui.background{index}", $"ui.command.take{index}" );
                                #endregion

                                Index[ Player ] += 1;
                            }
                            CuiHelper.AddUi( Player, UI );
                        }
                        break;
                    case 404:
                        Debug.LogError( "Response code: 404, please check your configurations" );
                        break;
                }
            }, this );
        }
        #endregion

        #region[ConsoleCommand] ui.next
        [ConsoleCommand( "ui.next" )]
        private void cmdUiNext( ConsoleSystem.Arg Args )
        {
            BasePlayer Player = BasePlayer.FindByID( System.Convert.ToUInt64( Args.Args[ 0 ] ) );
            webrequest.EnqueueGet( $"{Request}&items=true&steam_id={Player.UserIDString}", ( code, response ) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError( "Api does not responded to a request" );
                        break;
                    case 200:
                        Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>( response, new KeyValuesConverter() );
                        if (!Response.ContainsKey( "data" ))
                            return;
                        List<object> data = Response[ "data" ] as List<object>;
                        if (data == null || Response == null)
                            return;

                        if (data.Count <= Index[ Player ])
                        {
                            return;
                        }
                        else
                        {
                            for (int i = 0; i != Index[ Player ]; i++)
                            {
                                data.RemoveAt( 0 );
                                CuiHelper.DestroyUi( Player, $"ui.block{i}" );
                                CuiHelper.DestroyUi( Player, $"ui.amount{i}" );
                                CuiHelper.DestroyUi( Player, $"ui.product{i}" );
                                CuiHelper.DestroyUi( Player, $"ui.command.take{i}" );
                            }
                            CuiElementContainer UI = new CuiElementContainer();
                            for (int index = 0; index != 14 && index < data.Count; index++)
                            {
                                Dictionary<string, object> itemdata = data[ index ] as Dictionary<string, object>;

                                int ItemID = System.Convert.ToInt32( itemdata[ "item_id" ] );
                                int Amount = System.Convert.ToInt32( itemdata[ "amount" ] );
                                string Image = $"{itemdata[ "img" ]}";

                                #region[Element] ImgBlock                                                                                      
                                UI.Add( new CuiElement()
                                {
                                    Name = $"ui.block{index}",
                                    Parent = $"ui.background{index}",
                                    Components =
                                    {
                                        new CuiRawImageComponent
                                        {
                                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                            Url = Image
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = $"0.10 0.10",
                                            AnchorMax = $"0.90 0.90"
                                        },
                                        new CuiOutlineComponent
                                        {
                                            Distance = "1.0 1.0",
                                            Color = "0.0 0.0 0.0 1.0"
                                        }
                                    }
                                } );
                                #endregion

                                #region[Label] Amount
                                UI.Add( new CuiLabel()
                                {
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.0 0.0",
                                        AnchorMax = $"1.0 0.90"
                                    },
                                    Text =
                                    {
                                        Text = $"{Amount} шт. ",
                                        FontSize = 14,
                                        Align = TextAnchor.LowerRight,
                                        Color = "1 1 1 1"
                                    }
                                }, $"ui.background{index}", $"ui.amount{index}" );
                                #endregion

                                #region[ItemName] Product
                                UI.Add( new CuiLabel()
                                {
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.05 0.01",
                                        AnchorMax = $"0.99 0.99"
                                    },
                                    Text =
                                    {
                                        Text = $"{itemdata["name"]}",
                                        FontSize = 14,
                                        Align = TextAnchor.UpperLeft,
                                        Color = "1 1 1 1"
                                    }
                                }, $"ui.background{index}", $"ui.product{index}" );
                                #endregion

                                #region[Button] Take
                                UI.Add( new CuiButton
                                {
                                    Button =
                                    {
                                        Command = $"ui.gives {Player.userID} {index} {itemdata["id"]}",
                                        Color = "0 0 0 0"
                                    },
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.0 0.0",
                                        AnchorMax = $"1.0 1.0"
                                    },
                                    Text =
                                    {
                                        Text = ""
                                    }
                                }, $"ui.background{index}", $"ui.command.take{index}" );
                                #endregion

                                Index[ Player ] += 1;
                            }
                            CuiHelper.AddUi( Player, UI );
                        }
                        break;
                    case 404:
                        Debug.LogError( "Response code: 404, please check your configurations" );
                        break;
                }
            }, this );
        }
        #endregion

        #region[ChatCommand] /gstop
        [ChatCommand( "gstop" )]
        private void cmdTop( BasePlayer player, string command, string[] args )
        {
            if (System.Convert.ToBoolean( Config[ "COMMAND.TOP" ] ))
            {
                string request = $"{Request}&top=true&steam_id={player.UserIDString}";
                webrequest.EnqueueGet( request, ( code, res ) =>
                {
                    switch (code)
                    {
                        case 0:
                            Debug.LogError( "Api does not responded to a request" );
                            break;
                        case 200:
                            List<object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>( res, new KeyValuesConverter() )[ "data" ] as List<object>;
                            // Debug.LogWarning(res);
                            if (data.Count > 0)
                            {
                                player.ChatMessage( $"Топ игроков: " );
                            }
                            else
                            {
                                player.ChatMessage( $"Топ игроков пуст" );
                            }
                            foreach (object user in data)
                            {
                                Dictionary<string, object> info = user as Dictionary<string, object>;
                                if (!System.Convert.ToBoolean( String.Compare( info[ "steam_id" ].ToString(), player.UserIDString ) ))
                                {
                                    if (System.Convert.ToInt32( info[ "position" ].ToString() ) > 6)
                                        player.ChatMessage( $"..." );
                                    player.ChatMessage( $"#{info[ "position" ]} <color=lime>{info[ "username" ]}</color> : Очков: {info[ "points" ]}, Убийств: {info[ "kill" ]}, Смертей: {info[ "death" ]}" );
                                }
                                else
                                {
                                    player.ChatMessage( $"#{info[ "position" ]} {info[ "username" ]} : Очков: {info[ "points" ]}, Убийств: {info[ "kill" ]}, Смертей: {info[ "death" ]}" );
                                }

                            }
                            break;
                        case 404:
                            Debug.LogError( "Response code: 404, please check your configurations" );
                            break;
                    }
                }, this );
            }
        }
        #endregion

        #region[Helper] CanTake/SplitItem
        private int CanTake( BasePlayer Player, Item Item )
        {
            ItemContainer Container = Player.inventory.containerMain;
            int ItemID = Item.info.itemid;

            if (Item == null || ( Item.MaxStackable() == 1 && ( Container.IsFull() || ( Container.capacity - Container.itemList.Count ) < Item.amount ) ))
                return 0;
            else if (Item.MaxStackable() == 1 && !Container.IsFull())
                return 1 * Item.amount;

            return ( ( Container.FindItemsByItemID( ItemID ).Count + ( Container.capacity - Container.itemList.Count ) ) * Item.MaxStackable() - Container.GetAmount( ItemID, true ) );

        }
        private List<Item> SplitItem( Item Item )
        {
            List<Item> Items = new List<Item>() { Item };
            int MaxStackable = Item.MaxStackable();
            if (Item.amount > MaxStackable)
                for (int Amount = Items[ 0 ].amount; Items[ 0 ].amount > MaxStackable; Items[ 0 ].amount -= MaxStackable)
                    Items.Add( ItemManager.CreateByItemID( Item.info.itemid, MaxStackable ) );

            return Items;
        }
        #endregion

        //Statistic
        #region[HookMethod] OnEntityDeath
        [HookMethod( "OnEntityDeath" )]
        private void OnEntityDeath( BaseCombatEntity entity, HitInfo info )
        {
            if (entity == null || info == null || info.Initiator == null)
                return;

            if (System.Convert.ToBoolean( Config[ "TOP.USERS" ] ))
            {
                BaseEntity initiator = info.Initiator;

                if (entity as BasePlayer == null && initiator as BasePlayer == null)
                    return;

                Dictionary<string, object> args = new Dictionary<string, object>();

                if (initiator as BasePlayer != null)
                {
                    args[ "player_id" ] = initiator.ToPlayer().UserIDString;
                }
                else if (initiator.PrefabName.Contains( "animal" ))
                {
                    args[ "player_id" ] = "1";
                }

                if (entity as BasePlayer != null)
                {
                    args[ "victim_id" ] = entity.ToPlayer().UserIDString;
                    args[ "type" ] = entity.ToPlayer().IsSleeping() ? "sleeper" : "kill";
                }
                else if (entity.PrefabName.Contains( "animal" ))
                {
                    args[ "victim_id" ] = "1";
                    args[ "type" ] = "kill";
                }

                args[ "time" ] = System.Convert.ToInt32( ( DateTime.UtcNow.Subtract( new DateTime( 1970, 1, 1 ) ) ).TotalSeconds ).ToString();

                Stats.Add( args );
            }
        }
        #endregion

        #region[HookMethod] OnPlayerDisconnected
        [HookMethod( "OnPlayerDisconnected" )]
        private void OnPlayerDisconnected( BasePlayer player )
        {
            if (System.Convert.ToBoolean( Config[ "TOP.USERS" ] ))
            {
                if (Config[ "SERVER.ID" ].ToString() == "0")
                {
                    Debug.LogWarning( "Need set SERVER.ID in configurations to send info for top players" );
                }
                else
                {
                    string request = $"{Request}&leave=true&player_id={player.UserIDString}&played={player.net.connection.GetSecondsConnected()}&username={player.displayName}";
                    webrequest.EnqueueGet( request, ( code, res ) => { }, this );
                }
            }
        }
        #endregion

        #region[HookMethod] OnServerSave
        [HookMethod( "OnServerSave" )]
        private void OnServerSave()
        {
            if (System.Convert.ToBoolean( Config[ "TOP.USERS" ] ))
            {
                if (Config[ "SERVER.ID" ].ToString() == "0")
                {
                    Debug.LogWarning( "Need set SERVER.ID in configurations to send info for top players" );
                }
                else
                {
                    string request = $"{Request}&json=true&data={JsonConvert.SerializeObject( Stats )}";
                    //Debug.LogWarning(request);
                    webrequest.EnqueueGet( request, ( code, res ) =>
                    {
                        switch (code)
                        {
                            case 0:
                                Debug.LogError( "Api does not responded to a request" );
                                break;
                            case 200:
                                //Puts(res);
                                break;
                            case 404:
                                Debug.LogError( "Response code: 404, please check your configurations" );
                                break;
                        }
                    }, this );
                    Stats.Clear();
                }
            }
        }
        #endregion

        public class Debug
        {
            public static void LogWarning( object message ) => UnityEngine.Debug.LogWarning( CreateLog( message ) );
            public static void LogError( object message ) => UnityEngine.Debug.LogError( CreateLog( message ) );
            private static string CreateLog( object message ) => $"[{DateTime.Now.TimeOfDay.ToString().Split( '.' )[ 0 ]}] [GameStores]: {message}";
        }
    }
}