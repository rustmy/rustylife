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
    [Info("DayInfinite", "bazuka5801", "1.0.0")]
    class DayInfinite : RustPlugin
    {
        #region CONFIGURATION
        int nightHour;
        int morningHour;
        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Начало ночи (0-24)",out nightHour,20);
            Config.GetVariable("начало утра (0-24)",out morningHour,10);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            timer.Every(1f, () =>
            {
                if (Mathf.FloorToInt(TOD_Sky.Instance.Cycle.Hour) == nightHour)
                    OnNight();
            });
            ClearFog();
        }

        void ClearFog()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.fog 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.clouds  0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.rain  0");
        }

        void OnNight()
        {
            rust.RunServerCommand($"env.time {morningHour}");
            ConsoleNetwork.BroadcastToAllClients("chat.add", 0,  Messages["nightMissing"]);
        }

        #endregion
        
        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "nightMissing", "<size=17><color=#fec384>Ночь пропущена!</color></size>"}
        };

        #endregion
    }
}
