// Reference: Oxide.Core.RustyCore

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using RustyCore;
using RustyCore.Utils;

namespace Oxide.Plugins
{
    [Info("KitsGUI", "NorthStorm", 1.0)]
    class KitsGUI : RustPlugin
    {
        private RCore core = Interface.Oxide.GetLibrary<RCore>();
        private const string MAIN_UI_NAME = "KitsUIHUD";

        private HashSet<string> playerIDs = new HashSet<string>();

        private float maxButtonWidth = 0.2f;
        private float buttonSpacing = 0.5f;

        private Dictionary<string, object> KitsPng= new Dictionary<string, object>();

        [PluginReference]
        private Plugin Kits;

        [PluginReference]
        private Plugin CustomGui;

        // It's a good idea to check if the plugin you're trying to hook into
        // has been loaded by oxide (otherwise you can't call the hook)
        private void OnServerInitialized()
        {
            KitsPng = GetConfig("Kits Png", new Dictionary<string, object>());
            // Note: Trying to do this check in the plugin Init() method may
            // fail, as the plugin load order may be different each time
            if(Kits == null)
            {
                PrintWarning("Plugin 'Kits' was not found!");
            }
            CommunityEntity.ServerInstance.StartCoroutine(StoreImages());
        }

        IEnumerator StoreImages()
        {
            var images = KitsPng.ToDictionary(p => p.Key, p => p.Value.ToString());
            yield return ImageStorage.Store(images);
            KitsPng = images.ToDictionary(p => p.Key, p => (object) p.Value);
        }

        // Implement a custom class representing an online player
        class OnlinePlayer
        {
            // This field is required and will be automatically set to the player
            public BasePlayer Player;

            // This constructor can be implemented without any arguments if the player is not needed
            public OnlinePlayer(BasePlayer player)
            {
            }

        }
        // Automatically track online players, connected players are added to this collection and are removed when they disconnect
        [OnlinePlayers]
        Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer>();

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config["ButtonKitOnCDColor"] = "1 0 0 0.5";
            Config["ButtonKitReadyColor"] = "0 1 0 0.5";
            Config["BackroungPanelColor"] = "0 0 0 0.3";
            SaveConfig();
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            player.Command("bind k \"ShowKitsUI\"");
        }

        void Loaded()
        {
            foreach(BasePlayer player in onlinePlayers.Keys)
            {
                player.Command("bind k \"ShowKitsUI\"");
            }
        }

        [ConsoleCommand("showkitsui")]
        void cmdShowKitsUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            if(playerIDs.Contains(player.UserIDString))
            {
                DestroyKitsUI(player);
                playerIDs.Remove(player.UserIDString);
            }
            else
                DrawKitsUI(player);
        }

        [ConsoleCommand("destroykitsui")]
        void cmdDestroyKitsUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            if(playerIDs.Contains(player.UserIDString))
            {
                DestroyKitsUI(player);
                playerIDs.Remove(player.UserIDString);
            }
        }

        void Unload()
        {
            foreach(var player in BasePlayer.activePlayerList)
                DestroyKitsUI(player);
        }

        [ConsoleCommand("trytogetkit")]
        void cmdTryToGetKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if(arg.Args.Length > 0)
            {
                Kits?.Call("TryGiveKitToPlayer", player, arg.Args[0]);
                if(playerIDs.Contains(player.UserIDString))
                {
                    DestroyKitsUI(player);
                    playerIDs.Remove(player.UserIDString);
                }

            }
        }

        private void DestroyKitsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MAIN_UI_NAME);
        }



        private void DrawKitsUI(BasePlayer player)
        {
            float aspect = core.GetAspect(player.userID);
            var kitsList = Kits?.Call("GetAvailableKitList", player) as List<object[]>;
            var elements = new CuiElementContainer();

            if (kitsList != null && kitsList.Count > 0)
            {
                float realSpacing = buttonSpacing;
                float buttonWidth = realSpacing/kitsList.Count;
                if (buttonWidth > maxButtonWidth)
                {
                    realSpacing = maxButtonWidth*kitsList.Count;
                    buttonWidth = maxButtonWidth;
                }
                float buttonWidthSpace = 1f - realSpacing;
                float nearButtonSpace = buttonWidthSpace/(kitsList.Count + 1f);

                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = Config.Get<string>("BackroungPanelColor")
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 " + (0.5f - (buttonWidth/2 + 0.05)*(aspect/2)),
                        AnchorMax = "1 " + (0.5f + (buttonWidth/2 + 0.05)*(aspect/2))
                    },
                    CursorEnabled = true
                }, "Overlay", MAIN_UI_NAME);

                for (int i = 0; i < kitsList.Count; ++i)
                {
                    elements.Add(new CuiElement
                    {
                        Parent = MAIN_UI_NAME,
                        Components =
                        {
                            new CuiRawImageComponent()
                            {
                                Png =
                                    KitsPng.ContainsKey((string) kitsList[i][1])
                                        ? (string) KitsPng[(string) kitsList[i][1]]
                                        : "http://bafthcn.kmu.ac.ir/Images/UserUpload/Image/SH/radio.png",
                                Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                Color = (bool) kitsList[i][0] ? "1 1 1 1" : "1 1 1 0.5"

                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = (nearButtonSpace + i*nearButtonSpace + i*buttonWidth) + " 0.05",
                                AnchorMax =
                                    (nearButtonSpace + i*nearButtonSpace + i*buttonWidth + buttonWidth) + " 0.95"
                            }
                        }
                    });
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "TryToGetKit " + ((string) kitsList[i][1]).ToLower(),
                            Color = "0 0 0 0"
                            //(bool)kitsList[i][0] ? Config.Get<string>("ButtonKitReadyColor") : Config.Get<string>("ButtonKitOnCDColor")
                        },
                        RectTransform =
                        {
                            AnchorMin = (nearButtonSpace + i*nearButtonSpace + i*buttonWidth) + " 0.05",
                            AnchorMax = (nearButtonSpace + i*nearButtonSpace + i*buttonWidth + buttonWidth) + " 0.95"
                        },
                        Text =
                        {
                            Text = ""
                        }
                    }, MAIN_UI_NAME);
                }
            }

            CuiHelper.AddUi(player, elements);
            playerIDs.Add(player.UserIDString);
        }

        #region Helper's

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));

        #endregion


    }
}
