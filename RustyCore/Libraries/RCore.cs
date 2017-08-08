using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using RustyCore.Plugins;
using RustyCore.Utils;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using UnityEngine;
using Timer = Oxide.Core.Libraries.Timer;

namespace RustyCore
{
    public class RCore : Library
    {
        CuiGenerator CuiGenerator;
        PlayerFinder PlayerFinder;
        HookSequence HookSequence;
        WipeManager WipeManager;

        private Oxide.Core.Libraries.Timer timer = Interface.Oxide.GetLibrary<Oxide.Core.Libraries.Timer>("Timer");
        public Oxide.Plugins.Timer Once(float seconds, Action callback)
        {
            return new Oxide.Plugins.Timer(this.timer.Once(seconds, callback));
        }

        public RCore()
        {

        }

        public void DrawUI(BasePlayer player, string plugin, string function, params object[] args)
        {
            CuiGenerator.DrawUI(player,plugin,function,args);
        }

        public void DrawUIWithEx(BasePlayer player, string plugin,string function, CuiElementContainer additionalContainer, params object[] args)
        {
            CuiGenerator.DrawUIWIthEx(player, plugin, function, additionalContainer, args);
        }

        public void DestroyUI(BasePlayer player, string plugin, string function)
        {
            CuiGenerator.DestroyUI(player, plugin, function);
        }
        public float GetAspect(ulong uid) => CuiGenerator.GetAspect(uid);

        public IEnumerator StoreImage(string name, string url)
        {
            yield return ImageStorage.Store(name, url);
        }

        public IEnumerator StoreImages(Dictionary<string, string> images)
        {
            yield return ImageStorage.Store(images);
        }

        public string FindDisplayname(ulong uid) => PlayerFinder.FindDisplayname(uid);
        public BasePlayer FindBasePlayer(string nameOrUserId) => PlayerFinder.FindBasePlayer(nameOrUserId);
        public ulong FindUid(string name) => PlayerFinder.FindUid(name);

        public BasePlayer FindOnline( string nameOrUserId )
        {
            nameOrUserId = nameOrUserId.ToLower();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains( nameOrUserId ) || player.UserIDString == nameOrUserId)
                    return player;
            }
            return default( BasePlayer );
        }

        public void AddHook(Plugin plugin, string hookname, int weight) => HookSequence.AddHook(plugin, hookname, weight);
        public string TimeToString(double time)
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";

            if (days > 0) s += $"{days}дн.";
            if (hours > 0) s += $"{hours}ч. ";
            if (minutes > 0) s += $"{minutes}мин. ";
            if (seconds > 0) s += $"{seconds}сек.";
            else s = s.TrimEnd(' ');
            return s;
        }
       
        public long StringToTime(string time)
        {
            time = time.Replace(" ", "").Replace("d", "d ").Replace("h", "h ").Replace("m", "m ").Replace("s", "s ").TrimEnd(' ');
            var arr = time.Split(' ');
            long seconds = 0;
            foreach (var s in arr)
            {
                var n = s.Substring(s.Length - 1, 1);
                var t = s.Remove(s.Length - 1, 1);
                int d = int.Parse(t);
                switch (n)
                {
                    case "s":
                        seconds += d;
                        break;
                    case "m":
                        seconds += d *60;
                        break;
                    case "h":
                        seconds += d *3600;
                        break;
                    case "d":
                        seconds += d * 86400;
                        break;
                }
            }
            return seconds;
        }


        #region TELEPORTATION

        public void Teleport(BasePlayer player, BasePlayer target) => Teleport(player, target.transform.position);

        public void Teleport(BasePlayer player, float x, float y, float z) => Teleport(player, new Vector3(x, y, z));

        public void Teleport(BasePlayer player, Vector3 position)
        {
            if (player.IsDead() && player.IsConnected)
            {
                player.RespawnAt(position, Quaternion.identity);
                return;
            }
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading", null, null, null, null, null);
            
            player.StartSleeping();
            player.MovePosition(position);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            //player.UpdatePlayerCollider(true, false);
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            //TODO temporary for potential rust bug
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }

        #endregion

        public void SleepWaiting(BasePlayer player)
        {
            if (!player.IsConnected) return;
            Once(0.2f, () =>
            {
                if (!player.IsConnected) return;
                if (player.IsReceivingSnapshot || player.IsDead())
                {
                    SleepWaiting(player);
                    return;
                }
                if (player.IsSleeping())
                    player.EndSleeping();
            });
        }

        public DateTime GetWipeTime() => WipeManager?.GetWipeTime() ?? new DateTime(1970, 0, 0, 0, 0, 0);



        #region CHAT

        public void BroadcastChat(string message, string name = "", ulong uid = 0)
        {
            ConsoleNetwork.BroadcastToAllClients( "chat.add", uid, string.IsNullOrEmpty( name ) ? $"{message}" : $"{name}: {message}" );
        }

        #endregion
    }

}
