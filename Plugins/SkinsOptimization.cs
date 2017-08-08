using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("SkinsOptimization", "bazuka5801", "1.0.0")]
    class SkinsOptimization : RustPlugin
    {
        #region FIELDS

        private List<ulong> PlayersActivated = new List<ulong>();
        
        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadData();
            foreach (var player in BasePlayer.activePlayerList) OnPlayerInit(player);
        }

        void Unload()
        {
            SaveData();
        }

        void OnPlayerConnected(Network.Message packet)
        {
            var player = packet.Player();
            if (player)
                OnPlayerInit(player);
        }

        void OnPlayerInit(BasePlayer player)
        {
            ItemSkinsCommand(player, PlayersActivated.Contains(player.userID));
        }

        [ChatCommand("skinon")]
        void cmdChatSkinOn(BasePlayer player, string command, string[] args)
        {
            if (!PlayersActivated.Contains(player.userID))
            {
                PlayersActivated.Add(player.userID);
                ItemSkinsCommand(player, true);
                SendReply(player, Messages["skinsOn"]);
            }
            else
            {
                SendReply(player, Messages["alreadyOn"]);
            }
        }

        [ChatCommand("skinoff")]
        void cmdChatSkinOff(BasePlayer player, string command, string[] args)
        {
            if (PlayersActivated.Contains(player.userID))
            {
                PlayersActivated.Remove(player.userID);
                ItemSkinsCommand(player, false);
                SendReply(player, Messages["skinsOff"]);
            }
            else
            {
                SendReply(player, Messages["alreadyOff"]);
            }
        }

        #endregion

        #region CORE

        void ItemSkinsCommand(BasePlayer player, bool value)
        {
            player.SendConsoleCommand($"itemskins {(value ? 1 : 0)}");
        }
        
        #endregion

        #region DATA

        private readonly DynamicConfigFile PlayersActivatedFile = Interface.Oxide.DataFileSystem.GetFile("Skins.PlayersActivated");

        void OnServerSave() => SaveData();

        void LoadData()
        {
            PlayersActivated = PlayersActivatedFile.ReadObject<List<ulong>>();
        }

        void SaveData()
        {
            PlayersActivatedFile.WriteObject(PlayersActivated);
        }

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "skinsCurrentlyDisabled", "У вас выключены скины!\nЧтобы включить используйте /skin on\nЧтобы выключить используйте /skin off" },
            { "skinsOff", "Скины отключены!\nЧтобы включить используйте /skin on\nЧтобы выключить используйте /skin off" },
            { "skinsOn", "Скины включены!\nЧтобы включить используйте /skin on\nЧтобы выключить используйте /skin off" },
            { "alreadyOn", "У вас уже включены скины!" },
            { "alreadyOff", "У вас уже выключены скины!" }
        };

        #endregion
    }
}
