// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using RustyCore;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HotKeys", "Calytic", "0.0.2", ResourceId = 2135)]
    class HotKeys : RustPlugin
    {
        class RustBind
        {
            [JsonProperty("bind")] public string Bind = "";
            [JsonProperty("defaultkey")] public string DefaultKey = "";
        }

        Dictionary<string, RustBind> binds;

        Dictionary<string, string> defaultRustBinds = new Dictionary<string, string>()
        {
            {"f1", "consoletoggle"},
            {"backquote", "consoletoggle"},
            {"f7", "bugreporter"},
            {"w", "+forward"},
            {"s", "+backward"},
            {"a", "+left"},
            {"d", "+right"},
            {"mouse0", "+attack"},
            {"mouse1", "+attack2"},
            {"mouse2", "+attack3"},
            {"1", "+slot1"},
            {"2", "+slot2"},
            {"3", "+slot3"},
            {"4", "+slot4"},
            {"5", "+slot5"},
            {"6", "+slot6"},
            {"7", "+slot7"},
            {"8", "+slot8"},
            {"leftshift", "+sprint"},
            {"rightshift", "+sprint"},
            {"leftalt", "+altlook"},
            {"r", "+reload"},
            {"space", "+jump"},
            {"leftcontrol", "+duck"},
            {"e", "+use"},
            {"v", "+voice"},
            {"return", "chat.open"},
            {"mousewheelup", "+invnext"},
            {"mousewheeldown", "+invprev"},
            {"tab", "inventory.toggle "}
        };

        List<string> keys = new List<string>()
        {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "a",
            "b",
            "c",
            "d",
            "e",
            "f",
            "g",
            "h",
            "i",
            "j",
            "k",
            "l",
            "m",
            "n",
            "o",
            "p",
            "q",
            "r",
            "s",
            "t",
            "u",
            "v",
            "w",
            "x",
            "y",
            "z",
            "at",
            "f1",
            "f2",
            "f3",
            "f4",
            "f5",
            "f6",
            "f7",
            "f8",
            "f9",
            "end",
            "f10",
            "f11",
            "f12",
            "f13",
            "f14",
            "f15",
            "tab",
            "hash",
            "help",
            "home",
            "less",
            "menu",
            "plus",
            "altgr",
            "break",
            "caret",
            "clear",
            "colon",
            "comma",
            "minus",
            "pause",
            "print",
            "quote",
            "slash",
            "space",
            "delete",
            "dollar",
            "equals",
            "escape",
            "insert",
            "mouse0",
            "mouse1",
            "mouse2",
            "mouse3",
            "mouse4",
            "mouse5",
            "mouse6",
            "pageup",
            "period",
            "return",
            "sysreq",
            "exclaim",
            "greater",
            "keypad0",
            "keypad1",
            "keypad2",
            "keypad3",
            "keypad4",
            "keypad5",
            "keypad6",
            "keypad7",
            "keypad8",
            "keypad9",
            "leftalt",
            "numlock",
            "uparrow",
            "asterisk",
            "capslock",
            "pagedown",
            "question",
            "rightalt",
            "ampersand",
            "backquote",
            "backslash",
            "backspace",
            "downarrow",
            "leftarrow",
            "leftparen",
            "leftshift",
            "semicolon",
            "keypadplus",
            "rightapple",
            "rightarrow",
            "rightparen",
            "rightshift",
            "scrolllock",
            "underscore",
            "doublequote",
            "keypadenter",
            "keypadminus",
            "leftbracket",
            "leftcommand",
            "leftcontrol",
            "leftwindows",
            "keypaddivide",
            "keypadequals",
            "keypadperiod",
            "mousewheelup",
            "rightbracket",
            "rightcontrol",
            "rightwindows"
        };

        Dictionary<ulong, Dictionary<string, string>> playersBinds;

        void Loaded()
        {
            CheckConfig();
            binds = GetConfig("Settings", "Keys", GetDefaultKeys()).Select(
                p => new KeyValuePair<string, RustBind>(p.Key, new RustBind()
                    {
                        Bind = ((Dictionary<string, object>) p.Value)["bind"].ToString(),
                        DefaultKey = ((Dictionary<string, object>) p.Value)["defaultkey"].ToString()
                    }
                )).ToDictionary(p => p.Key, p => p.Value);
            LoadData();
            foreach (var uid in playersBinds.Keys.ToList())
            {
                SetBinds(uid, playersBinds[uid]);
            }
            SaveData();
        }

        void OnServerInitialized()
        {
            BindAll();
            SaveData();
        }

        void OnPlayerInit(BasePlayer player)
        {
            BindKeys(player);
        }

        #region COMMANDS

        [ConsoleCommand("hotkey.bind")]
        private void ccHotKeyBind(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 1)
            {
                return;
            }
            else if (arg.Args.Length == 3)
            {
                string caption = arg.Args[0];
                string keyCombo = arg.Args[1].Trim();
                string bind = arg.Args[2].Trim();
                var defbind = new RustBind() {Bind = bind, DefaultKey = keyCombo};
                ;
                if (binds.ContainsKey(keyCombo))
                {
                    SendReply(arg, $"[HotKeys] Replaced \"{caption}\"" + keyCombo + ": " + bind);
                    binds[caption] = defbind;
                }
                else
                {
                    SendReply(arg, $"[HotKeys] Bound \"{caption}\"" + keyCombo + ": " + bind);
                    binds.Add(caption, defbind);
                }
                foreach (var pBinds in playersBinds.Values)
                    pBinds.Add(caption, keyCombo);
                SaveBinds();
                BindAll();
            }
            else
            {
                SendReply(arg, "[HotKeys] Invalid Syntax. hotkey.bind \"Название\" \"Кнопка\" [bind]");
            }
        }

        [ConsoleCommand("hotkey.unbind")]
        private void ccHotKeyUnbind(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 1)
            {
                return;
            }

            if (arg.Args.Length == 1)
            {
                string caption = arg.Args[0].Trim();

                if (binds.ContainsKey(caption))
                {
                    string bind = binds[caption].Bind;
                    string key = binds[caption].DefaultKey;
                    if (defaultRustBinds.ContainsKey(key))
                    {
                        SendReply(arg, "[HotKeys] Reverted " + key + ": " + defaultRustBinds[key]);
                    }
                    else
                    {
                        SendReply(arg, "[HotKeys] Unbound " + key + ": " + bind);
                    }

                    UnbindAll(caption);
                    binds.Remove(caption);
                    SaveBinds();
                    SaveData();
                }
            }
            else
            {
                SendReply(arg, "[HotKeys] Invalid Syntax. hotkey.unbind \"Название\"");
            }
        }

        #endregion

        #region CORE


        Dictionary<string, string> GetBinds(BasePlayer player)
        {
            Dictionary<string,string> binds;
            if (!playersBinds.TryGetValue(player.userID, out binds))
            {
                binds = this.binds.ToDictionary(k => k.Key, v => v.Value.DefaultKey);
                playersBinds.Add(player.userID, binds);
                return binds;
            }
            binds = binds.Where(b => this.binds.Keys.Contains(b.Key)).ToDictionary(k => k.Key, v => v.Value);
            
            foreach (var bind in this.binds)
            {
                if (!binds.ContainsKey(bind.Key))
                    binds.Add(bind.Key, bind.Value.DefaultKey);
            }
            return binds.OrderBy(key => key.Key).ToDictionary(k => k.Key, v => v.Value);
        }

        void SetBinds(ulong uid, Dictionary<string, string> lBinds)
        {
            try
            {
                if (lBinds == null) lBinds = playersBinds[uid];
                var toRemove = new List<string>();
                foreach (var bind in lBinds)
                {
                    if (binds.ContainsKey(bind.Key))
                    {
                        if (binds[bind.Key].DefaultKey == bind.Value) toRemove.Add(bind.Key);
                    }
                    else toRemove.Add(bind.Key);
                }
                foreach (var caption in toRemove)
                    lBinds.Remove(caption);
                if (lBinds.Count <= 0) playersBinds.Remove(uid);
                else playersBinds[uid] = lBinds;
            }
            catch (Exception ex)
            {
                var msg = "";
                foreach (var s in lBinds)
                    msg += $"{s.Key}: {s.Value}\n";
                PrintError(msg);
            }
        }

        void BindAll()
        {
            ulong player = 1;
            try
            {
                foreach (var player2 in BasePlayer.activePlayerList)
                {
                    player = player2.userID;
                    BindKeys(player2);
                }

            }
            catch (Exception)
            {
                Puts(player.ToString());
            }
        }

        void UnbindAll(string caption)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                UnbindKey(player, caption);
            }
        }

        void BindKeys(BasePlayer player)
        {
            if (player == null) return;
            foreach (KeyValuePair<string, string> kvp in GetBinds(player))
            {
                if (binds.ContainsKey(kvp.Key))
                    Bind(player, kvp.Value, binds[kvp.Key].Bind);
            }
        }

        void UnbindKey(BasePlayer player, string caption)
        {
            var key = GetBinds(player)[caption];
            string defaultRustBind = ".";
            if (defaultRustBinds.ContainsKey(key))
            {
                defaultRustBind = defaultRustBinds[key];
            }
            var binds = GetBinds(player);
            binds.Remove(caption);
            SetBinds(player.userID, binds);
            Bind(player, key, defaultRustBind);
        }

        #endregion

        #region UI

        #region CUIGENERATOR

        RCore core = Interface.Oxide.GetLibrary<RCore>();
        #endregion

        [ChatCommand("binds")]
        void ShowBindsMenu(BasePlayer player)
        {
            core.DrawUI(player,"HotKeys","menu");
            CuiHelper.DestroyUi(player, "bindscontainer");
            var container = new CuiElementContainer();
            var mainPanel = container.Add(new CuiPanel() { Image = { Color = "0 0 0 0" } }, "hotkeyBindsPanel", "bindscontainer");

            float width = 0.5f;
            float height = 0.065f;
            float y = 1-height;
            float x = 0;
            foreach (var bind in GetBinds(player))
            {
                var bindpanel = container.Add(new CuiPanel()
                {
                    RectTransform = { AnchorMin = $"{x} {y}", AnchorMax = $"{x+width} {y+height}",OffsetMax = "-9 -7",OffsetMin = "5 0"},
                    Image = { Color = "0.5 0.5 0.5 1"}
                }, mainPanel);

                container.Add(new CuiLabel()
                {
                    RectTransform = {AnchorMin = "0.05 0", AnchorMax = "0.7 1" },
                    Text = {Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", FontSize = 14, Text = bind.Key}
                },bindpanel);
                
                container.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.75 0.1", AnchorMax = "0.985 0.9"},
                    Text = {Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 14, Text = bind.Value.ToUpper()},
                    Button = {Command = $"hotkeybinds.selectkey {bind.Key}", Color = "0 0 0 0.7"}
                }, bindpanel);

                y -= height;
                if (y <= 0.01)
                {
                    y = 1 - height;
                    x += 0.5f;
                }
            }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("hotkeybinds.selectkey")]
        void cmdSelectBindKey(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            ShowBindsKeys(arg.Player(), arg.FullString);
        }
        void ShowBindsKeys(BasePlayer player, string caption)
        {
            CuiHelper.DestroyUi(player, "bindscontainer");

            var container = new CuiElementContainer();
            var mainPanel = container.Add(new CuiPanel() { Image = { Color = "0 0 0 0" } }, "hotkeyBindsPanel", "bindscontainer");
            var playerBinds = GetBinds(player);
            float gap = 0.01f;
            float height = 0.04f;
            float startxBox = gap;
            float startyBox = 1f - height - 0.05f;

            float xmin = startxBox;
            float ymin = startyBox;
            foreach (var key in keys.Where(k=> !playerBinds.ContainsValue(k)))
            {
                var width = 0.03f + 0.015*key.Length;

                if (xmin + width+gap >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + gap;
                }

                var min = $"{xmin} {ymin}";
                var max = $"{xmin + width} {ymin + height}";
                container.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = min, AnchorMax = max},
                    Text = {Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 14, Text = key.ToUpper()},
                    Button = { Command = $"hotkeybinds.bind \"{caption}\" \"{key}\"", Color = "0 0 0 0.7" }
                },mainPanel);

                xmin +=(float) width + gap;
            }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("hotkeybinds.bind")]
        void cmdBind(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            var player = arg.Player();
            var match = Regex.Match(arg.FullString, @"^.*\\\""(?<caption>.*?)\\\""\s\\\""(?<key>.*)\\\""");
            var caption = match.Groups["caption"].Value;
            var key = match.Groups["key"].Value;
            var playerBinds = GetBinds(player);
            var lastKey = playerBinds[caption];

            // Unbind Last Key
            bool anotherBind = false;
            foreach (var bind in playerBinds)
                if (bind.Value == lastKey && bind.Key !=caption)
                {
                    anotherBind = true;
                    Bind(player, lastKey, binds[bind.Key].Bind);
                }
            if (!anotherBind)
            {
                string defaultRustBind = ".";
                if (defaultRustBinds.ContainsKey(lastKey))
                {
                    defaultRustBind = defaultRustBinds[lastKey];
                }
                Bind(player, lastKey, defaultRustBind);
            }
            // End Unbind

            // Bind Key
            playerBinds[caption] = key;
            SetBinds(player.userID, playerBinds);
            Bind(player,key, binds[caption].Bind);
            
            // End Bind
            SaveData();
            ShowBindsMenu(player);
        }

        void Bind(BasePlayer player, string key, string command)
        {
            if (!Net.sv.IsConnected())
                return;
            Net.sv.write.Start();
            Net.sv.write.PacketID(Network.Message.Type.ConsoleCommand);
            Net.sv.write.String($"bind {key} {command}");
            Net.sv.write.Send(new SendInfo(player.net.connection));
        }

        [ConsoleCommand("hotkeybinds.close")]
        void BindsMenuClose(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            core.DestroyUI(arg.Player(), "HotKeys", "menu");
        }

        #endregion

        #region DATA

        private DynamicConfigFile playersKeys_file = Interface.Oxide.DataFileSystem.GetFile(nameof(HotKeys) + "_data");

        void LoadData()
        {
            try
            {
                playersBinds = playersKeys_file.ReadObject<Dictionary<ulong, Dictionary<string, string>>>();
            }
            catch (Exception)
            {
                playersBinds = new Dictionary<ulong, Dictionary<string, string>>();
            }
        }

        void SaveData() => playersKeys_file.WriteObject(playersBinds);
        #endregion

        #region CONFIGURATION

        void SaveBinds()
        {
            Config["Settings", "Keys"] = binds;
            Config.Save();
        }

        void LoadDefaultConfig()
        {
            Config["Settings", "Keys"] = GetDefaultKeys();

            Config["VERSION"] = Version.ToString();
        }

        private Dictionary<string,object> GetDefaultKeys()
        {
            return new Dictionary<string, object>() { {"Атака", new RustBind() {DefaultKey = "z",Bind = "+attack"} }};
        }

        void CheckConfig()
        {
            if (Config["VERSION"] == null)
            {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig();
            }
            else if (GetConfig<string>("VERSION", "") != Version.ToString())
            {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig();
            }
        }

        protected void ReloadConfig()
        {
            Config["VERSION"] = Version.ToString();

            // NEW CONFIGURATION OPTIONS HERE
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole("Upgrading configuration file");
            SaveConfig();
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private T GetConfig<T>(string name, string name2, T defaultValue)
        {
            if (Config[name, name2] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name, name2], typeof(T));
        }

        #endregion
    }
}
