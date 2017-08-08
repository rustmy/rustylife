using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Logging;
using Oxide.Core.Plugins;
using System.IO;

namespace RustyCore.Utils
{
    public static class Logger
    {
        private static readonly CompoundLogger logger = Interface.Oxide.RootLogger;

        public static void Info(string msg)
        {
            logger.Write(LogType.Info, msg);
        }

        public static void Error(string msg)
        {
            logger.Write(LogType.Error, msg);
        }

        public static void Warning(string msg)
        {
            logger.Write(LogType.Warning, msg);
        }
        public static void Debug(string msg)
        {
            logger.Write(LogType.Debug, msg);
        }

        public static void Log(this Plugin plugin, string filename, string text, bool console = false)
        {
            string str = Path.Combine( Interface.Oxide.LogDirectory, plugin.Name );
            if (!Directory.Exists( str ))
            {
                Directory.CreateDirectory( str );
            }
            if (console) Info( $"[{plugin.Name}/{filename}] {text}" );
            filename = $"{plugin.Name.ToLower()}_{filename.ToLower()}-{DateTime.Now:yyyy-MM-dd}.txt";
            using (StreamWriter streamWriter = new StreamWriter( Path.Combine( str, Utility.CleanPath( filename ) ), true ))
            {
                streamWriter.WriteLine( $"{DateTime.Now:G}: {text}" );
            }
        }
    }
}
