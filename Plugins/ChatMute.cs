using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChatMute", "bazuka5801", "1.0.0")]
    class ChatMute : RustPlugin
    {
        Dictionary<ulong, int> mutes = new Dictionary<ulong, int>();
        private List<string> badWordsList;

        const int MUTE_SECONDS = 120;

        DynamicConfigFile saveFile = Interface.Oxide.DataFileSystem.GetFile("ChatMutes");

        void OnServerInitialized()
        {
            DynamicConfigFile bwFile = Interface.Oxide.DataFileSystem.GetFile("ChatMuteWords");
            badWordsList = bwFile.ReadObject<List<string>>() ?? new List<string>();
            if (badWordsList.Count == 0) bwFile.WriteObject(new List<string>() {"fuck"});
            timer.Every(1f, () =>
            {
                List<ulong> toRemove = mutes.Keys.ToList().Where(uid => --mutes[uid] < 0).ToList();
                toRemove.ForEach(p => mutes.Remove(p));
            });
            mutes = saveFile.ReadObject<Dictionary<ulong, int>>();
        }

        void Unload()
        {
            saveFile.WriteObject(mutes);
        }

        bool? OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args.Length == 0) return null;

            if (mutes.ContainsKey(player.userID))
            {
                SendReply(player, $"Chat blocked!!! {mutes[player.userID]}sec.");
                return false;
            }

            var message = arg.GetString(0);

            foreach (var word in badWordsList)
                if (message.Contains(word))
                {
                    Mute(player);
                    return false;
                }
            return null;
        }

        void Mute(BasePlayer player)
        {
            mutes[player.userID] = MUTE_SECONDS;
        }
    }
}
