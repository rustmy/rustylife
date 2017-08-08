using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Logger", "bazuka5801", "1.0.0")]
    public class Logger : RustPlugin
    {
        private const int COMMANDS_LIMIT = 20;
        private Dictionary<ulong, LoggerPlayer> players = new Dictionary<ulong, LoggerPlayer>();

        private static Logger m_Instance;

        List<string> cmdIgnore = new List<string>()
        {
            "chat.say",
            "craft.add"
        };

        void Loaded()
        {
            m_Instance = this;
            timer.Every(60f, Service);
        }

        void Service()
        {
            foreach (var player in players.Values)
            {
                player.Service();
            }
        }

        void OnServerCommand( ConsoleSystem.Arg arg )
        {
            if (arg.Connection == null) return;

            var userId = arg.Connection.userid;
            var command = arg.cmd.FullName;
            var args = arg.GetString( 0 );

            if (cmdIgnore.Contains(command)) return;

            LoggerPlayer player;
            if (!players.TryGetValue( userId, out player))
                player = players[ userId ] = new LoggerPlayer($"{userId}/{arg.Connection.username}");

            player.LogCommand( command, args);
        }

        void OnPlayerDisconnected( BasePlayer player )
        {
            players.Remove(player.userID);
        }

        public static void Log(string prefix, string text)
        {
            m_Instance.Puts( prefix );
            m_Instance.LogToFile("log_commands", prefix + text, m_Instance);
        }

        private class LoggerPlayer
        {
            private string m_Player;
            private Dictionary<string, List<string>> excecutedCommands = new Dictionary<string, List<string>>();
            

            public LoggerPlayer(string player)
            {
                this.m_Player = player;
            }

            public void LogCommand( string command, string args )
            {
                List<string> cmdCache;
                if (!excecutedCommands.TryGetValue( command, out cmdCache ))
                    cmdCache = excecutedCommands[ command ] = new List<string>();
                cmdCache.Add( args );
                if (cmdCache.Count > COMMANDS_LIMIT)
                {
                    Log($"COMMANDS_LIMIT ({COMMANDS_LIMIT}) {command}: {m_Player}",$"\n [ {string.Join(", ", cmdCache.ToArray())} ] ");
                    cmdCache.Clear();
                }
            }

            public void Service()
            {
                excecutedCommands.Clear();
            }
        }

    }
}
