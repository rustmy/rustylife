// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RustyCore;
using RustyCore.Utils;
using UnityEngine;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("MagicTools", "bazuka5801", "1.0.0")]
    class MagicTools : RustPlugin
    {
        Dictionary<string, ItemDefinition> coockables = new Dictionary<string, ItemDefinition>()
        {
            { "sulfur.ore", ItemManager.FindItemDefinition("sulfur") },
            { "hq.metal.ore", ItemManager.FindItemDefinition("metal.refined") },
            { "metal.ore", ItemManager.FindItemDefinition("metal.fragments") },
            { "wolfmeat.raw", ItemManager.FindItemDefinition("wolfmeat.cooked") },
            { "meat.boar", ItemManager.FindItemDefinition("meat.pork.cooked") },
            { "fish.raw", ItemManager.FindItemDefinition("fish.cooked") },
            { "chicken.raw", ItemManager.FindItemDefinition("chicken.cooked") },
            { "bearmeat", ItemManager.FindItemDefinition("bearmeat.cooked") },
            { "deermeat.raw", ItemManager.FindItemDefinition("chicken.cooked") },
            { "wood", ItemManager.FindItemDefinition("charcoal") },
        };

        Dictionary<ulong, PlayerData> m_Data;

        [ChatCommand("mtools")]
        void cmdChatMagicTools(BasePlayer player, string command, string[] args)
        {
            PlayerData playerData;
            if (!m_Data.TryGetValue(player.userID, out playerData))
            {
                SendReply(player, Messages["accessDenied"]);
                return;
            }
            if (args.Length == 0)
            {
                SendReply(player, string.Format(Messages["remainHits"],playerData.Hits) + System.Environment.NewLine + Messages["help"]);
            }
            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "on":
                        if (playerData.State)
                        {
                            SendReply(player, Messages["turnAlreadyOn"]);
                            return;
                        }
                        else
                        {
                            playerData.State = true;
                            SendReply(player, Messages["turnOn"]);
                            return;
                        }
                    case "off":
                        if (!playerData.State)
                        {
                            SendReply(player, Messages["turnAlreadyOff"]);
                            return;
                        }
                        else
                        {
                            playerData.State = false;
                            SendReply(player, Messages["turnOff"]);
                            return;
                        }
                }
            }
        }

        [ConsoleCommand("magictools.addhits")]
        void cmdAddHits(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Args.Length == 2) return;
            ulong userId = ulong.Parse(arg.Args[0]);
            int addedHits = int.Parse(arg.Args[1]);

            PlayerData playerData;
            if (m_Data.TryGetValue(userId, out playerData))
            {
                playerData.Hits += addedHits;
            }
            else
            {
                m_Data[userId] = new PlayerData() { Hits = addedHits };
            }
        }

        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadData();
        }

        void Unload() => SaveData();
        
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity ent, Item item)
        {
            if (dispenser == null || ent == null || item == null)
            {
                return;
            }

            BasePlayer player = ent as BasePlayer;
            if (player == null) return;
            
            ItemDefinition def;
            if (coockables.TryGetValue(item.info.shortname, out def))
            {
                if (ProcessHit(player.userID))
                {
                    ReplaceContents(def, item);
                }
            }
        }

        bool ProcessHit(ulong userId)
        {
            PlayerData playerData;
            if (m_Data.TryGetValue(userId, out playerData) && playerData.State)
            {
                if (--playerData.Hits <= 0)
                {
                    m_Data.Remove(userId);
                }
                return true;
            }
            return false;
        }

        #region Helpers

        private void ReplaceContents(ItemDefinition def, Item item)
        {
            Item _item = ItemManager.Create(def, item.amount);
            item.info = def;
            item.contents = _item.contents;
        }

        #endregion


        #region Data

        DynamicConfigFile m_DataFile = Interface.Oxide.DataFileSystem.GetFile("MagicTools_Players");

        void LoadData()
        {
            m_Data = m_DataFile.ReadObject<Dictionary<ulong, PlayerData>>();
        }

        void OnServerSave() => SaveData();

        void SaveData()
        {
            m_DataFile.WriteObject(m_Data);
        }

        #endregion

        #region NestedType: PlayerData

        public class PlayerData
        {
            [JsonProperty("hits")]
            public int Hits;
            [JsonProperty("state")]
            public bool State = true;
        }

        #endregion

        #region Localization

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "remainHits", "Внимание! У вас осталось {0} раскалённых ударов" },
            { "hitsEnded", "Внимание! У вас закончились раскалённые удары!" },
            { "accessDenied", "Внимание! У вас нет доступа к данной команде!" },
            { "help", "Для включения/отключения используйте <color=red>/mtools on/off</color>" },
            { "turnOff", "Раскалённые инструменты выключены" },
            { "turnOn", "Раскалённые инструменты включены" },
            { "turnAlreadyOff", "Невозможно! Раскалённые инструменты уже выключены" },
            { "turnAlreadyOn", "Невозможно! Раскалённые инструменты уже включены" },
        };
        
        #endregion
    }
}
