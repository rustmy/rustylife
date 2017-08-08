// Reference: Oxide.Core.RustyCore
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using RustyCore;
using RustyCore.Utils;
using UnityEngine;
using Global = Rust.Global;

namespace Oxide.Plugins
{
    [Info( "ItemBlocker", "vaalberith & bazuka5801", "2.0.0")]
    class ItemBlocker : RustPlugin
    {
        #region Variables

        private RCore core = Interface.Oxide.GetLibrary<RCore>();
        bool loaded = false;


        #endregion
        
        #region Data

        readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("Hard/ItemBlocker");
        Dictionary<string, List<string>> blockedItems;

        void ReadData()
        {
            blockedItems = dataFile.ReadObject<Dictionary<string, List<string>>>();
        }

        #endregion

        #region Hook's

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            ReadData();
            RunTimer();
        }

        void OnWipe()
        {
            PrintWarning( "Вайп прошёл успешно!" );
        }

        Dictionary<string, int> ItemLists = new Dictionary<string, int>();
        
    
        void OnCuiGeneratorInitialized()
        {
            loaded = true;
        }

        public string GetTime(Item item)
        {
            var time = blockedItems.FirstOrDefault(p => p.Value.Contains(item.info.shortname)).Key;
            if (time == default(string)) return string.Empty;
            return core.TimeToString(core.StringToTime(time)- ( DateTime.Now.Subtract( core.GetWipeTime() ).TotalSeconds ) );
        }

        bool? CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (InDuel(player)) return true;
            var ret =  blockedItems.All(entry => !entry.Value.Contains(item.info.shortname)) ? (bool?)null : false;
            if (ret == false)
            {
                core.DrawUI(player, "ItemBlocker", "blockedItem", GetTime(item));
                timer.Once(2f, () => core.DestroyUI(player, "ItemBlocker", "blockedItem"));
            }
            return ret;
        }

        bool? CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null || InDuel( player )) return null;
            if (item.info.shortname == "stash.small") return false;
            var ret = blockedItems.All( entry => !entry.Value.Contains( item.info.shortname ) ) ? (bool?) null : false;
            if (ret == false)
            {
                core.DrawUI( player, "ItemBlocker", "blockedItem", GetTime( item ) );
                timer.Once( 2f, () => core.DestroyUI( player, "ItemBlocker", "blockedItem" ) );
            }
            return ret;
        }

        [ConsoleCommand("itemblocker.notify.close")]
        void cmdNotifyClose(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;
            core.DestroyUI(player, "ItemBlocker", "blockedItem");
        }

        #endregion

        #region Timer

        void RunTimer()
        {
            timer_OnTick();
        }

        void timer_OnTick()
        {
            var afterWipe = DateTime.Now.Subtract(core.GetWipeTime()).TotalSeconds;
            for (int i = blockedItems.Count - 1; i >= 0; i--)
            {
                var item = blockedItems.ElementAt(i);
                var timeout = core.StringToTime(item.Key);
                if (afterWipe >= timeout)
                {
                    blockedItems.Remove(item.Key);
                    continue;
                }
            }
            if (blockedItems.Count > 0)
            {
                timer.Once((float)(core.StringToTime(blockedItems.ElementAt(0).Key) - afterWipe), timer_OnTick);
            }
        }

        #endregion

            #region Console Command's
        

        #endregion
        
        #region Helpers

        DateTime ParseTime(string s) => DateTime.ParseExact(s, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            #endregion

        #region Player Duel
        
        [PluginReference]
        Plugin Duels;

        bool InDuel(BasePlayer player)
        {
            if(Duels == null) return false;
            try
            {
                var ret = Duels?.Call("inDuel", player);
                bool result = ret != null && (bool)ret;
                return result;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}