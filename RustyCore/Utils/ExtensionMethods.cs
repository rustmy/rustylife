using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core.Configuration;

namespace RustyCore.Utils
{
    public static class ExtensionMethods
    {
        public static void GetVariable<T>(this DynamicConfigFile config,string name, out T value, T defaultValue)
        {
            config[name] = value = config[name] == null ? defaultValue : (T) Convert.ChangeType(config[name], typeof(T));
        }

        public static bool ContainsAny(this string value, params string[] args)
        {
            return args.Any(value.Contains);
        }


    }
}
