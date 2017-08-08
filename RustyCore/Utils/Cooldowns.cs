using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;

namespace RustyCore.Utils
{
    public static class Cooldowns
    {
        private class Cooldown
        {
            public ulong UserId;
            public double Expired;
            [JsonIgnore]
            public Action OnExpired;
        }

        private static DynamicConfigFile cooldownsFile = Interface.Oxide.DataFileSystem.GetFile("Core/Cooldowns");
        private static Dictionary<string, List<Cooldown>> cooldowns;

        internal static void Load()
        {
            cooldowns = cooldownsFile.ReadObject<Dictionary<string, List<Cooldown>>>() ??
                        new Dictionary<string, List<Cooldown>>();
        }

        internal static void Save()
        {
            cooldownsFile.WriteObject(cooldowns);
        }

        internal static void Service()
        {
            var time = GrabCurrentTime();
            List<string> toRemove = new List<string>();
            foreach (var cd in cooldowns)
            {
                var keyCooldowns = cd.Value;
                List<string> toRemoveCooldowns = new List<string>();
                for (var i = keyCooldowns.Count - 1; i >= 0; i--)
                {
                    var cooldown = keyCooldowns[i];
                    if (cooldown.Expired < time)
                    {
                        cooldown.OnExpired?.Invoke();
                        keyCooldowns.RemoveAt(i);;
                    }
                }
                if (keyCooldowns.Count == 0) toRemove.Add(cd.Key);
            }
            toRemove.ForEach(p=>cooldowns.Remove(p));
        }

        public static void SetCooldown(this BasePlayer player, string key, int seconds, Action onExpired = null)
        {
            List<Cooldown> keyCooldowns;
            if (!cooldowns.TryGetValue(key, out keyCooldowns)) cooldowns[key] = keyCooldowns = new List<Cooldown>();
            keyCooldowns.Add(new Cooldown(){UserId = player.userID, Expired = GrabCurrentTime()+seconds, OnExpired = onExpired } );
        }
        public static int GetCooldown( this BasePlayer player, string key)
        {
            List<Cooldown> keyCooldowns = new List<Cooldown>();
            if (cooldowns.TryGetValue(key, out keyCooldowns))
            {
                var cooldown = keyCooldowns.FirstOrDefault(p => p.UserId == player.userID);
                if (cooldown != null)
                {
                    return (int)(cooldown.Expired - GrabCurrentTime());
                }
            }
            return 0;
        }
        static double GrabCurrentTime() => DateTime.UtcNow.Subtract( new DateTime( 1970, 1, 1, 0, 0, 0 ) ).TotalSeconds;
    }
}
