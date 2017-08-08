using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AutoBroadcast", "Wulf/lukespragg", "1.0.0", ResourceId = 684)]
    [Description("Sends randomly configured chat messages every X amount of seconds")]

    class AutoBroadcast : CovalencePlugin
    {
        #region Initialization

        int interval;

        protected override void LoadDefaultConfig()
        {
            Config["Broadcast Interval (Seconds)"] = interval = GetConfig("Broadcast Interval (Seconds)", 300);

            SaveConfig();
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();

            if (lang.GetMessages("en", this) == null)
            {
                lang.RegisterMessages(new Dictionary<string, string>
                {
                    ["ExampleMessage"] = "This is an example. Change it, remove it, translate it, whatever!",
                }, this);
            }
            else
            {
                var messages = lang.GetMessages("en", this);
                lang.RegisterMessages(messages, this);
            }

            Broadcast();
        }

        #endregion

        #region Broadcasting

        void Broadcast()
        {
            timer.Every(interval, () =>
            {
                foreach (var player in players.Connected)
                {
                    var messages = lang.GetMessages("en", this);
                    var randomKey = messages.ElementAt(new Random().Next(0, messages.Count)).Key;
                    player.Message(Lang(randomKey, player.Id));
                }
            });
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
