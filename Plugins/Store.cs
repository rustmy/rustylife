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
    [Info("Store", "bazuka5801", "1.0.0")]
    class Store : RustPlugin
    {
        #region CONFIGURATION

        string secret;
        string shopId;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("SECRET.KEY", out secret, "");
            Config.GetVariable("SHOP.ID", out shopId, "");
            SaveConfig();
        }

        #endregion


        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        #endregion

        #region COMMANDS

        [ConsoleCommand("store.money.plus")]
        void cmdStoreMoneyAdd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;

            if (arg.Args.Length != 2)
            {
                SendReply(arg, $"YOU NEED WRITE 2 PARAMS");
                return;
            }

            ulong userid;
            if (!ulong.TryParse(arg.Args[0], out userid))
            {
                SendReply(arg, $"FIRST PARAM NEED BE AS STEAM_ID");
                return;
            }

            int amount;
            if (!int.TryParse(arg.Args[1], out amount))
            {
                SendReply(arg, $"SECOND PARAM NEED BE AS AMOUNT");
                return;
            }

            MoneyPlus(userid, amount);
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized() => LoadDefaultConfig();
        
        #endregion

        #region CORE

        void MoneyPlus(ulong userId, int amount)
        {
            ExecuteApiRequest(new Dictionary<string, string>()
            {
                { "action", "moneys" },
                { "type", "plus" },
                { "steam_id", userId.ToString() },
                { "amount", amount.ToString() }
            });
        }

        void MoneyMinus(ulong userId, int amount)
        {
            ExecuteApiRequest(new Dictionary<string, string>()
            {
                { "action", "moneys" },
                { "type", "minus" },
                { "steam_id", userId.ToString() },
                { "amount", amount.ToString() }
            });
        }

        void ExecuteApiRequest(Dictionary<string, string> args)
        {
            string url = $"http://panel.gamestores.ru/api?shop_id={shopId}&secret={secret}" +
                         $"{string.Join("",args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
            webrequest.EnqueueGet(url, (i, s) =>
            {
                if (i != 200)
                {
                    PrintError($"{url}\nCODE {i}: {s}");
                }
                else
                {
                    Puts($"SUCCESS: {s}");
                }
            }, this);
        }

        #endregion

        #region DATA

        #endregion
        
    }
}
