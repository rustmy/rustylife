using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("FriendsChat", "bazuka5801", "1.0.0")]
    class FriendsChat : RustPlugin
    {
        #region FIELDS

        [PluginReference]
        private Plugin Friends;

        #endregion

        #region COMMANDS

        [ChatCommand("f")]
        void cmdChatFriendChat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, "/f Сообщение");
                return;
            }
            string message = string.Join(" ", args);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            ChatFriends(player, message);
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
        }

        #endregion

        #region CORE

        void ChatFriends(BasePlayer player, string message)
        {
            var friends = Friends?.Call("ApiGetActiveFriends", player) as List<BasePlayer>;
            if (friends == null || friends.Count == 0)
            {
                SendReply(player, Messages["friendsMissing"]);
                return;
            }
            foreach (var friend in friends)
                FriendMessage(player, friend, message);
            FriendMessage(player, player, message);
            LogToFile("log", $"[{player.displayName}] -> {string.Join(", ", friends.Select(p=>$"[{p.displayName}]").ToArray())}: {message}", this);
        }

        void FriendMessage(BasePlayer sender, BasePlayer player, string message)
        {
            player.SendConsoleCommand("chat.add", player.userID, $"<color=#22E100>[F]</color> <color=#6E71D3>{sender.displayName}</color>: <color=#ffffff>{message}</color>");
        }
        #endregion
        

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "friendsMissing", "Не один ваш друг не в сети" }
        };

        #endregion
    }
}
