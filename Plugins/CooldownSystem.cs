// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CooldownSystem", "bazuka5801", "1.0.0")]
    class CooldownSystem : RustPlugin
    {
        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();
        List<CooldownPlayer> cdPlayers = new List<CooldownPlayer>();
        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            timer.Every(1f, CooldownTimerHandler);
            foreach (var player in BasePlayer.activePlayerList)
            {
                DrawUI(CDPlayer(player));
            }
        }

        void Unload()
        {

        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            NextTick(()=>DrawUI(CDPlayer(player)));
        }
        CooldownPlayer CDPlayer(BasePlayer player)
        {
            var cdPlayer = cdPlayers.Find(p => p.player == player);
            if (cdPlayer != null)
            {
                cdPlayer.player = player;
                return cdPlayer;
            }
            cdPlayer = new CooldownPlayer() {player = player};
            cdPlayers.Add(cdPlayer);
            return cdPlayer;
        }

        #endregion

        #region API

        void SetCooldown(BasePlayer player, string name, int seconds)
        {
            CDPlayer(player).SetCooldown(name,seconds);
        }

        #endregion

        #region CORE

        void CooldownTimerHandler()
        {
            var block = GetWeaponBlockCoolDown();
            for (int i = cdPlayers.Count - 1; i >= 0; i--)
            {
                var cdPlayer = cdPlayers[i];
                cdPlayer.Next();
                cdPlayer.cooldowns[2] = block;
                if (cdPlayer.IsFinish())
                {
                    cdPlayers.RemoveAt(i);
                    continue;
                }
                DrawUI(cdPlayer);
            }
        }

        #endregion

        #region WEAPON RESTRICT

        [PluginReference] private Plugin WeaponRestrict;

        private string WeaponBlock = "00:00";

        int GetWeaponBlockCoolDown()
        {
            return WeaponRestrict?.Call("restrictSeconds") as int? ?? 0;
        }

        #endregion

        #region UI

        void DrawUI(CooldownPlayer player)
        {
            if (!player.player.IsConnected || player.player.IsSleeping()) return;
            
            
            int i = 0;
            string numString = string.Join("",player.cooldowns.Select(cd =>
            {
                var ts = TimeSpan.FromSeconds(cd);
                i++;
                return i == 3 ? $"{ts.Hours:00}{ts.Minutes:00}{ts.Seconds:00}" : $"{ts.Minutes:00}{ts.Seconds:00}";
            }).ToArray());
            core.DrawUI(player.player, "CooldownSystem", "menu", GetImages (numString));
        }

        string[] GetImages(string value) => value.Select(p => ImageStorage.FetchPng(p.ToString())).ToArray();
    
        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {

        };

        #endregion


        #region Nested type: CooldownPlayer

        class CooldownPlayer
        {
            public BasePlayer player;
            public List<int> cooldowns = new List<int>();

            private static readonly List<string> cooldownsNames = new List<string>() {"home", "tp", "weapon.block", "duel", "raid"};

            public CooldownPlayer()
            {
                for (int i = 0; i < cooldownsNames.Count; i++)
                {
                    cooldowns.Add(0);
                }
            }

            public void Next()
            {
                for (int i = 0; i < cooldowns.Count; i++)
                {
                    if (cooldowns[i] > 0)
                        --cooldowns[i];
                }
            }

            public bool IsFinish() => cooldowns.All(cd => cd <= 0);

            public void SetCooldown(string name, int seconds)
            {
                cooldowns[cooldownsNames.IndexOf(name)] = seconds;
            }
        }

        #endregion

    }
}
