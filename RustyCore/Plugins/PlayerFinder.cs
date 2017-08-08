using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;

namespace RustyCore.Plugins
{
    internal class PlayerFinder : RustPlugin
    {
        DynamicConfigFile players_file = Interface.Oxide.DataFileSystem.GetFile("Players");
        DynamicConfigFile nicknames_file = Interface.Oxide.DataFileSystem.GetFile("Nicknames");
        private Dictionary<ulong, string> players = new Dictionary<ulong, string>();
        private Dictionary<string, ulong> nicknames = new Dictionary<string, ulong>();
        void Loaded()
        {
            players = players_file.ReadObject<Dictionary<ulong, string>>() ?? new Dictionary<ulong, string>();
            nicknames = nicknames_file.ReadObject<Dictionary<string, ulong>>() ?? new Dictionary<string, ulong>();
        }

        private bool init = false;
        void OnServerInitialized()
        {
            foreach (var player in UnityEngine.Object.FindObjectsOfType<BasePlayer>())
            {
                //Puts("A " + player.displayName);
                players[player.userID] = player.displayName.ToLower();
                NicknameUpdate(player);
            }
            init = true;
        }

        void NicknameUpdate(BasePlayer player)
        {
            var lastname = nicknames.FirstOrDefault(p => p.Value == player.userID).Key;
            if (lastname != default(string)) nicknames.Remove(lastname);
            nicknames[player.displayName] = player.userID;
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (!init) return;
        }

        void OnPlayerInit(BasePlayer player)
        {
            players[player.userID] = player.displayName.ToLower();
            NicknameUpdate(player);
        }

        void OnServerSave()
        {
            players_file.WriteObject(players);
            nicknames_file.WriteObject(nicknames);
        }

        void Unload()
        {
            OnServerSave();
        }

        public ulong FindUid(string name)
        {
            name = name.ToLower();
            ulong uid;
            if (nicknames.TryGetValue(name, out uid)) return uid;
            return nicknames.FirstOrDefault(player => player.Key.ToLower().Contains(name)).Value;
        }



        public BasePlayer FindBasePlayer(string nameOrUserId)
        {
            nameOrUserId = nameOrUserId.ToLower();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId)
                    return player;
            }
            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId)
                    return player;
            }
            return default(BasePlayer);
        }

        public string FindDisplayname(ulong uid)
        {
            string name;
            if (players.TryGetValue(uid, out name)) return name;
            return uid.ToString();
        }
    }
}
