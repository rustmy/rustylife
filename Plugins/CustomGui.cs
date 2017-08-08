using Rust;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Network;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("CustomGui", "vaalberith / bazuka5801", "2.0.0")]
    class CustomGui : RustPlugin
    {

        [PluginReference]
        Plugin CuiGenerator;
        [PluginReference]
        Plugin InfoPanel;


        void DrawUI(BasePlayer player, string function, params object[] args)
        {
            CuiGenerator?.Call("DrawUI", player, Name, function, args);
        }
        void DestroyUI(BasePlayer player, string function)
        {
            CuiGenerator?.Call("DestroyUI", player, Name, function);
        }
        //Хранение данных
        Dictionary<ulong, string> Gui = new Dictionary<ulong, string>();
        Dictionary<ulong, int> playerPrefs = new Dictionary<ulong, int>();
        readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("CustomGuiPlayerPrefs");
        //Хуки__________________________________________________________________________________________
        void OnServerInitialized()
        {
            if (CuiGenerator == null) return;
            playerPrefs = dataFile.ReadObject<Dictionary<ulong, int>>();

            BasePlayer.activePlayerList.ForEach(p =>
            {
                int style;
                if (playerPrefs.TryGetValue(p.userID, out style))
                    drawtoplayer(p, style);
            });
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            int style;
            if(playerPrefs.TryGetValue(player.userID, out style))
                drawtoplayer(player, style);
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "CuiGenerator")
                OnServerInitialized();
        }

        [HookMethod("OnPlayerAspectChanged")]
        void OnPlayerAspectChanged(BasePlayer player)
        {
            if (!playerPrefs.ContainsKey(player.userID))
            {
                drawSkinmenutoplayer(player);
                return;
            }
            int skin;
            if (!playerPrefs.TryGetValue(player.userID, out skin) && skin <= 0)
                return;
            drawtoplayer(player, skin);
        }
        //РИСОВАНИЕ_______________________________________________________________________


        //Меню скинов и стилей
        void drawSkinmenutoplayer(BasePlayer player)
        {
            DrawUI(player,  "menu");
        }

        //Оверлеи, оформления
        void drawtoplayer(BasePlayer player, int style)
        {
            if (style <= 0) return;
            DrawUI(player, $"info{style}");
            RefreshPanel(player);
        }

        void RefreshPanel(BasePlayer player)
        {
            if (InfoPanel == null)
            {
                timer.Once(0.1f, () => RefreshPanel(player));
                return;
            }
            InfoPanel.Call("RefreshInfoPanel", player);
        }

        void OnCuiGeneratorInitialized()
        {
            BasePlayer.activePlayerList.ForEach(p=> {
                int style;
                if (playerPrefs.TryGetValue(p.userID, out style))
                    drawtoplayer(p, style); RefreshPanel(p);});
        }




        //консоль команды (обработки кнопок)________________________________________________________________________

        //команда для обрабатывания команд кнопок выбора скина
        [ConsoleCommand("customgui.guiskin")]
        void conssetskin(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                int skin = int.Parse(arg.Args[0]);
                BasePlayer player = arg.Connection.player as BasePlayer;

                DestroyUI(player,  "menu");

                int prevSkin;
                if (playerPrefs.TryGetValue(player.userID, out prevSkin) && prevSkin > 0)
                    DestroyUI(player,  $"info{playerPrefs[player.userID]}");
                playerPrefs[player.userID] = skin;
                dataFile.WriteObject(playerPrefs);
                drawtoplayer(player, skin);
            }
        }
        
        //чат-команды_______________________________________________________________________________________

        //команда в чате вызывает меню скинов (аналог кнопки)
        [ChatCommand("gui")]
        void cmdshowgui(BasePlayer player, string command, string[] args)
        {
            drawSkinmenutoplayer(player);
        }
        
    }
}