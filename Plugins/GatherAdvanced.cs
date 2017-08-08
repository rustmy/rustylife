// Reference: Oxide.Core.RustyCore
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries;
using RustyCore;

namespace Oxide.Plugins
{
    [Info("GatherAdvanced", "bazuka5801", "1.0.0")]
    public class GatherAdvanced : RustPlugin
    {

        #region CLASSES
        public static int START_TIME;
        public Dictionary<int, int> BONUSES;
        public Dictionary<string, int> BONUSMULTIPLIER;
        
        public class GatherData
        {
            public int Time = START_TIME;
            public int TotalAmount = 0;
            public string shortname;
            public int amount;
        }

        #endregion
        
        #region VARIABLES

        RCore core = Interface.Oxide.GetLibrary<RCore>();
        readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("GatherAdvanced");
        Dictionary<BasePlayer, int> notifierLasthit = new Dictionary<BasePlayer, int>();
        Dictionary<BasePlayer, int> bonuses = new Dictionary<BasePlayer, int>();

        Dictionary<string, string> itemsLoaclization = new Dictionary<string, string>()
        {
            {"hq.metal.ore", "МВК РУДА"},
            {"metal.ore", "ЖЕЛЕЗНАЯ РУДА"},
            {"sulfur.ore", "СЕРНАЯ РУДА"},
            {"stones", "КАМНИ"},
            {"metal.fragments", "МЕТАЛЛ. ФРАГ." },
            {"charcoal", "УГОЛЬ" },
            {"metal.refined", "МВК" }
        };

        Dictionary<int, int> gatherBonuses = new Dictionary<int, int>();
        Dictionary<BasePlayer, GatherData> gathers = new Dictionary<BasePlayer, GatherData>();

        protected override void LoadDefaultConfig()
        {
            Config["StartTime"] = START_TIME = GetConfig(80, "StartTime");
            Config["Bonuses"] = BONUSES = GetConfig(new Dictionary<int, int>() { {100,100}}, "Bonuses");
            Config["BonusMultiplier"] = BONUSMULTIPLIER = GetConfig(new Dictionary<string, int>()
            {
                {"hq.metal.ore", 1},
                {"metal.ore", 20},
                {"sulfur.ore", 10},
                {"stones", 30}
            }, "BonusMultiplier");
            
            SaveConfig();
        }

        void ReloadSettings()
        {
            LoadDefaultConfig();
        }
        #endregion

        #region Oxide Hooks

        void OnServerInitialized()
        {
            ReloadSettings();
            timer.Every(1, NotifierLasthitLoop);
            timer.Every(1, GatherTimerLoop);
            timer.Every(1, BonusTimerLoop);
            core.AddHook(this, nameof(OnDispenserGather), 999);
        }

        void OnPluginLoaded(Plugin name)
        {
            if (name.ToString() == "ExtPlugin" && name.Author == "Sanlerus, Moscow.OVH")
            {
                Unsubscribe("OnDispenserGather");
                Subscribe("OnDispenserGather");
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            notifierLasthit.Remove(player);
            gathers.Remove(player);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!entity.ToPlayer()) return;
            var player = entity.ToPlayer();
            var gatherType = dispenser.gatherType.ToString("G");

            if (gatherType == "Ore")
            {
                GatherData data;
                if (!gathers.TryGetValue(player, out data))
                    gathers.Add(player, data = new GatherData());
                var lastAmount = data.TotalAmount;
                float del = 1;
                if (PermissionService.HasPermission(player, "gatherplus.vip"))
                    del = 1.66f;
                else if (PermissionService.HasPermission(player, "gatherplus.premium"))
                    del = 1.33f;
                var am = (int) (item.amount/del);
                data.TotalAmount += am;
                int bonusKey;
                if (GetBonus(lastAmount, data.TotalAmount, out bonusKey))
                    GiveBonus(player, bonusKey);
                data.amount = am;
                data.shortname = item.info.shortname;
                data.Time = START_TIME;
                UIDrawNotifier(player, data);
            }
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BasePlayer))return;
            var player = (BasePlayer)entity;
            gathers.Remove(player);
            core.DestroyUI(player, "GatherAdvanced","notifier");
        }
        #endregion

        #region Timers

        void GatherTimerLoop()
        {
            List<BasePlayer> removeList = new List<BasePlayer>();
            foreach (var gatherPair in gathers)
            {
                var data = gatherPair.Value;
                data.Time--;
                if (data.Time >= 0)
                {
                    UIDrawNotifier(gatherPair.Key, data, false);
                    continue;
                }
                removeList.Add(gatherPair.Key);
                core.DestroyUI(gatherPair.Key, "GatherAdvanced", "notifier");
            }
            foreach (var p in removeList)
                gathers.Remove(p);
        }

        void BonusTimerLoop()
        {
            var time = Time;
            var removeList = (from bonusPair in bonuses
                              where bonusPair.Value <= time
                              select bonusPair.Key).ToList();
            for (int i = removeList.Count - 1; i >= 0; i--)
            {
                var player = removeList[i];
                core.DestroyUI(player, "GatherAdvanced", "bonus");
                bonuses.Remove(player);
            }
        }

        void NotifierLasthitLoop()
        {
            var time = Time;
            List<BasePlayer> removeList = (from lasthitPair in notifierLasthit where lasthitPair.Value <= time select lasthitPair.Key).ToList();
            for (int i = removeList.Count - 1; i >= 0; i--)
            {
                var player = removeList[i];
                notifierLasthit.Remove(player);
            }
        }

        #endregion

        #region FUNCTIONS
        
        Dictionary<string,int> itemIDS = new Dictionary<string, int>()
        {
            {"hq.metal.ore", 2133577942},
            {"metal.ore", -1059362949},
            {"sulfur.ore", 889398893},
            {"stones", -892070738}
        };

        void GiveBonus(BasePlayer player, int bonusKey)
        {
            var bonusAmount = BONUSES[bonusKey];
            var bonusType = GetBonusType();
            int amount = bonusAmount*(int) BONUSMULTIPLIER[bonusType];
            Item item = ItemManager.CreateByItemID(itemIDS[bonusType], amount);
            //Puts(player.displayName + ": "+item.info.shortname + " " + item.amount);
            player.inventory.GiveItem(item);

            UIDrawBonus(player, bonusKey, $"{amount} {itemsLoaclization[item.info.shortname]}");
        }

        public string GetBonusType() => BONUSMULTIPLIER.Keys.ToList()[UnityEngine.Random.Range(0, BONUSMULTIPLIER.Count)];

        public bool GetBonus(int lastAmount, int newAmount, out int bonusKey)
        {
            bonusKey = -1;
            foreach (var bonus in BONUSES)
                if (lastAmount < bonus.Key && newAmount >= bonus.Key)
                {
                    bonusKey = bonus.Key;
                    return true;
                }
            return false;
        }

        #endregion

        #region UI

        void UIDrawNotifier(BasePlayer player, GatherData data, bool destroy = true)
        {
            if (destroy && data.amount != data.TotalAmount)
            {
                core.DestroyUI(player, "GatherAdvanced","notifier");
                notifierLasthit[player] = Time + 2;
            }
            if (!itemsLoaclization.ContainsKey(data.shortname))
                Puts("Invalid item: "+data.shortname);
            core.DrawUI(player, "GatherAdvanced", "notifier", data.amount, itemsLoaclization[data.shortname], data.TotalAmount, data.Time);
        }


        void UIDrawBonus(BasePlayer player, int bonusKey, string item)
        {
            var bonusAmount = BONUSES[bonusKey];
            if (bonuses.ContainsKey(player))
            {
                core.DestroyUI(player, "GatherAdvanced", "bonus");
            }
            core.DrawUI(player, "GatherAdvanced", "bonus", bonusKey.ToString(), item.ToString());
            bonuses[player] = Time + 4;
        }

        #endregion

        #region CONFIG

        T GetConfig<T>(T defaultValue, string firstKey, string secondKey = null, string thirdKey = null)
        {
            try
            {
                object value;

                // get the value associated with the provided keys
                if(thirdKey != null)
                {
                    value = Config[firstKey, secondKey, thirdKey];
                }
                else if(secondKey != null)
                {
                    value = Config[firstKey, secondKey];
                }
                else
                {
                    value = Config[firstKey];
                }

                // if the value is a dictionary, add the key/value pairs to a dictionary and return it
                // this particular implementation only handles dictionarys with string key/value pairs
                if(defaultValue.GetType() == typeof(Dictionary<string,int>))           // checks if the value is a dictionary
                {
                    Dictionary<string, int> valueDictionary = Config.ConvertValue<Dictionary<string, int>>(value);
                    
                    return (T)Convert.ChangeType(valueDictionary, typeof(T));
                }
                if(defaultValue.GetType() == typeof(Dictionary<int, int>))           // checks if the value is a dictionary
                {
                    Dictionary<string, int> valueDictionary =Config.ConvertValue<Dictionary<string,int>>(value);
                    Dictionary<int, int> values = valueDictionary.Keys.ToDictionary(int.Parse, key => (int) valueDictionary[key]);

                    return (T)Convert.ChangeType(values, typeof(T));
                }
                // if the value is a list, add the list elements to a list and return it
                // this particular implementation only handles lists with char elements
                else if(value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(List<>))             // checks if the value is a list
                {
                    IList valueList = (IList)value;
                    List<char> values = new List<char>();

                    foreach(object obj in valueList)
                    {
                        if(obj is string)
                        {
                            char result;
                            if(char.TryParse((string)obj, out result))
                            {
                                values.Add(result);
                            }
                        }
                    }
                    return (T)Convert.ChangeType(values, typeof(T));
                }
                // handles every other type
                else
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch(Exception)
            {
                return defaultValue;
            }
        }

        #endregion

        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                if (player == null || string.IsNullOrEmpty(permissionName))
                    return false;

                var uid = player.UserIDString;
                if (permission.UserHasPermission(uid, permissionName))
                    return true;

                return false;
            }

            public static void RegisterPermissions(Plugin owner, List<string> permissions)
            {
                if (owner == null) throw new ArgumentNullException("owner");
                if (permissions == null) throw new ArgumentNullException("commands");

                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }

        int Time => (int) (DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
    }
}
