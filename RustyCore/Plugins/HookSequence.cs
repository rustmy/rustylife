using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Plugins;
using RustyCore.Utils;
using UnityEngine;

namespace RustyCore.Plugins
{
    [Info("HookSequence", "bazuka5801", "1.0.0")]
    internal class HookSequence : RustPlugin
    {
        class Hook
        {
            public Plugin plugin;
            public int weight;

            public Hook(Plugin plugin, int weight)
            {
                this.plugin = plugin;
                this.weight = weight;
            }
        }

        private readonly FieldInfo hooksField = typeof(PluginManager).GetField("hookSubscriptions",BindingFlags.Instance | BindingFlags.NonPublic);
        new readonly Dictionary<string, List<Hook>> hooks = new Dictionary<string, List<Hook>>();

        void OnPluginLoaded(Plugin name)
        {
            SortHooks();
        }

        public void AddHook(Plugin plugin, string hookname,  int weight)
        {
            List<Hook> hookList;
            if (!hooks.TryGetValue(hookname, out hookList))
            {
                hooks[hookname] = hookList = new List<Hook>();
            }
            hookList.Add(new Hook(plugin, weight));
            SortHooks();
        }

        void SortHooks()
        {
            var hooksDescriptions =
                       ((IDictionary<string, IList<Plugin>>)hooksField.GetValue(Interface.Oxide.RootPluginManager));
            foreach (var hookname in hooks.Keys.ToList())
            {
                hooksDescriptions[hookname] =
                    hooksDescriptions[hookname].OrderByDescending(x => hooks[hookname].Find(p => p.plugin == x)?.weight ?? 0)
                        .ToList();
            }
        }
    }
}
