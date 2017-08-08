// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DuelShop", "bazuka5801", "1.0.0")]
    class DuelShop : RustPlugin
    {
        #region CONFIGURATION

        private int playerStartMoney;
        private int playerMaxMoney;
        private int playerKillReward;
        private int teamWinerBonus;
        private int teamLooserBonus;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Стартовый баланс", out playerStartMoney, 800);
            Config.GetVariable("Максимальный баланс", out playerMaxMoney, 800);
            Config.GetVariable("Награда за убийство", out playerKillReward, 300);
            Config.GetVariable("Награда за победу", out teamWinerBonus, 3250);
            Config.GetVariable("Награда за поражение", out teamLooserBonus, 1400);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        static RCore core = Interface.Oxide.GetLibrary<RCore>();

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
        }

        void Unload()
        {

        }

        #endregion

        #region CORE

        #endregion

        #region UI

        #region MONEY

        static void DrawMoney(BasePlayer player, int money)
        {
            core.DrawUI(player, "DuelShop", "money", money);
        }

        static void DestroyMoney(BasePlayer player)
        {
            core.DestroyUI(player, "DuelShop", "money");
        }

        #endregion

        #region MENU

        static void DrawMenu(DuelShopPlayer shopPlayer)
        {
            core.DrawUI(shopPlayer.Player, "DuelShop", "money");
        }

        static void DestroyMenu(BasePlayer player)
        {
            core.DestroyUI(player, "DuelShop", "money");
        }

        #endregion

        #endregion

        #region DATA

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {

        };

        #endregion

        #region NestedType: Duel

        class Duel
        {

        }

        #endregion

        #region NestedType: Team

        class Team : List<BasePlayer>
        {
            public void AddMoney(int count)
            {
                
            }
        }

        #endregion

        #region NestedType: DuelShopPlayer

        class DuelShopPlayer : IDisposable
        {
            public int Money { get; private set; } = 0;
            public BasePlayer Player;

            #region CONSTRUCTORS
            
            private DuelShopPlayer(BasePlayer player)
            {
                this.Player = player;
            }

            public static DuelShopPlayer Create(BasePlayer player)
            {
                return new DuelShopPlayer(player);
            }

            #endregion

            #region FUNCTIONS

            public void AddMoney(int count) => Money += count;

            public void ResetMoney() => Money = 0;

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            #endregion
        }

        #endregion
    }
}
