// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Grant", "bazuka5801", "1.0.0")]
    class Grant : RustPlugin
    {

        #region FIELDS

        float interval = 5f;

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        Dictionary<ulong, Dictionary<string, long>> grants;

        #endregion

        #region COMMANDS

        [ConsoleCommand("grant.permission")]
        void cmdGrantPermission(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            var steamid = ulong.Parse(arg.Args[0]);
            for (int i = 1; i < arg.Args.Length; i += 2)
            {
                var perm = arg.Args[i];
                var time = arg.Args[i+1];
                GrantPermission(steamid, perm, time);
            }
        }
        [ConsoleCommand( "grantallusersdata" )]
        void cmdgrantallusersdata( ConsoleSystem.Arg arg )
        {
            if (arg.Connection != null) return;

            int i = 0;
            foreach (var player in grants)
            {
                foreach (var privelege in player.Value)
                {
                    Server.Command("grant", "user", player.Key, privelege.Key);
                    i++;
                }
            }
            Puts($"Granted {i} perms for {grants.Count} players");
        }
        [ConsoleCommand("grant.dropuser")]
        void cmdDropUser(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            var steamid = ulong.Parse(arg.Args[0]);

            Puts($"Dropped user {steamid}");

            var perms = grants[steamid];
            var msg = $"grant.permission \"{steamid}\"";
            foreach (var perm in perms.ToList())
            {
                var elapsedTime = TimeSpan.FromSeconds(perm.Value-GetTimeStamp());
                string time = "";

                int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
                int hours = elapsedTime.Hours;

                if (days > 0) time += $"{days}d";
                if (hours > 0) time += $"{hours}h";
                RevokePermission(steamid, perm.Key);
                msg += $" \"{perm.Key}\" \"{time}\"";
            }
            Puts(msg);
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadData();
            timer.Every(interval, GrantTimerHandler);
        }

        void Unload()
        {
            SaveData();
        }


        #endregion

        #region CORE

        void GrantTimerHandler()
        {
            bool changed = false;
            long timestamp = GetTimeStamp();
            foreach (var user in grants.ToArray())
                foreach (var privilege in user.Value.ToArray())
                    if (timestamp > privilege.Value)
                    {
                        RevokePermission(user.Key, privilege.Key);
                        changed = true;
                    }
            if (changed)
                SaveData();
        }

        void GrantPermission(ulong uid, string perm, string remaintime)
        {
            Dictionary<string, long> perms;
            if (!grants.TryGetValue(uid, out perms))
                grants[uid] = perms = new Dictionary<string, long>();

            var seconds = core.StringToTime(remaintime);
            var time = GetTimeStamp() + seconds;
            long permtime;
            if (perms.TryGetValue(perm, out permtime))
            {
                time = permtime + seconds;
                Log($"Продление: \"{uid}\" \"{perm}\" на {remaintime}");
            }
            perms[perm] = time;

            rust.RunServerCommand($"oxide.grant user \"{uid}\" \"{perm}\"");
            Log($"grant \"{uid}\" \"{perm}\" {remaintime}");

            SaveData();
        }

        void RevokePermission(ulong uid, string perm)
        {
            Dictionary<string, long> perms;
            rust.RunServerCommand($"oxide.revoke user \"{uid}\" \"{perm}\"");
            if (grants.TryGetValue(uid, out perms) && perms.ContainsKey(perm))
            {
                perms.Remove(perm);
                if (perms.Count == 0)
                    grants.Remove(uid);
                Log($"revoke \"{uid}\" \"{perm}\"");
                SaveData();
            }
        }

        long GetTimeStamp()
        {
            return (long) DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        #endregion

        #region DATA

        private DynamicConfigFile grantsFile = Interface.Oxide.DataFileSystem.GetFile("GrantData");

        void OnServerSave()
        {
            SaveData();
        }

        void LoadData()
        {
            grants = grantsFile.ReadObject<Dictionary<string, Dictionary<string, long>>>().ToDictionary(k=>ulong.Parse(k.Key), v => v.Value);
        }

        void SaveData()
        {
            grantsFile.WriteObject(grants.ToDictionary(k => k.Key.ToString(), v => v.Value));
        }

        #endregion

        #region LOG

        void Log(string message)
        {
            LogToFile("grants", message,this);
        }

        #endregion
    }
}
