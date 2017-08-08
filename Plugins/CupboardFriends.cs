using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CupboardFriends", "bazuka5801", "1.0.0")]
    class CupboardFriends : RustPlugin
    {

        #region FIELDS

        /// <summary>
        /// Информация о методе, который обновляет авторизованных в ящике
        /// </summary>
        readonly MethodInfo updateAllPlayers = typeof(BuildingPrivlidge).GetMethod("UpdateAllPlayers",BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Информация о переменной, которая содержит список авторизованных в замке
        /// </summary>
        readonly FieldInfo whiteListField = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Instance | BindingFlags.NonPublic));
        readonly FieldInfo codeField = typeof(CodeLock).GetField("code", (BindingFlags.Instance | BindingFlags.NonPublic));

        /// <summary>
        /// Плагин Friends
        /// </summary>
        [PluginReference]
        private Plugin Friends;

        #endregion

        #region COMMANDS

        /// <summary>
        /// Команда, которая позволяет вкл/выкл автоматическую авторизацию друзей в шкафах и замках
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [ChatCommand("au")]
        void cmdAU(BasePlayer player, string command, string[] args)
        {
            if (ignorePlayers.Contains(player.userID))
            {
                ignorePlayers.Remove(player.userID);
                SendReply(player, Messages["chatAURemoveIgnoreMesssage"]);
            }
            else
            {
                ignorePlayers.Add(player.userID);
                SendReply(player, Messages["chatAUAddIgnoreMesssage"]);
            }
        }

        #endregion

        #region OXIDE HOOKS

        /// <summary>
        /// Вызывается, когда сервер завершил инициализацию
        /// </summary>
        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadData();
        }

        void Unload() => SaveData();

        /// <summary>
        /// Вызывается, когда игрок что-либо построил
        /// </summary>
        /// <param name="plan">Шпатель</param>
        /// <param name="go">Строение, которое игрок построил</param>
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (go == null || plan == null) return;
            BuildingPrivlidge privlidge = go.ToBaseEntity() as BuildingPrivlidge;
            if (privlidge == null)return;
            var player = plan.GetOwnerPlayer();
            if (player == null) return;
            if (ignorePlayers.Contains(player.userID)) return;
            List<ulong> friends = Friends?.Call("ApiGetFriends", player.userID) as List<ulong>;
            if (friends == null) return;
            CupboardAuth(privlidge, friends);
            SendReply(player, Messages["deployCupboardMessage"]);
        }

        /// <summary>
        /// Вызывается, когда игрок что-то поставил
        /// </summary>
        /// <param name="deployer">Строитель</param>
        /// <param name="entity">Предмет, который игрок поставил</param>
        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (!(entity is CodeLock)) return;
            var codelock = (CodeLock)entity;
            var player = deployer.GetOwnerPlayer();
            codelock.OwnerID = player.userID;

            if (ignorePlayers.Contains(player.userID)) return;
            List<ulong> friends = Friends?.Call("ApiGetFriends", player.userID) as List<ulong>;
            if (friends == null) return;
            whiteListField.SetValue(codelock, new List<ulong>() { player.userID });
            CodeLockAuth(codelock, friends);

            codeField.SetValue(codelock, UnityEngine.Random.Range(1000, 9999));
            codelock.SetFlag(BaseEntity.Flags.Locked, true);
            SendReply(player, Messages["deployCodelockMessage"]);
        }

        #endregion

        #region CORE
        
        /// <summary>
        /// Авторизирует список игроков в шкафу
        /// </summary>
        /// <param name="privlidge">Шкаф</param>
        /// <param name="friends">Список игроков</param>
        void CupboardAuth(BuildingPrivlidge privlidge, List<ulong> friends)
        {
            bool changed = false;
            foreach (var friendUserId in friends.Where(p=> !privlidge.authorizedPlayers.Select(uid=>uid.userid).Contains(p)))
            {
                privlidge.authorizedPlayers.Add(new PlayerNameID()
                {
                    userid = friendUserId,
                    username = friendUserId.ToString()
                });
                changed = true;
            }
            if (changed)
            {
                updateAllPlayers.Invoke(privlidge, new object[] {});
                privlidge.SendNetworkUpdate();
            }
        }

        /// <summary>
        /// Авторизирует список игроков в кодовом замке
        /// </summary>
        /// <param name="codelock">Кодовый замок</param>
        /// <param name="friends">Список игроков</param>
        void CodeLockAuth(CodeLock codelock, List<ulong> friends)
        {
            List<ulong> whitelist = (List<ulong>) whiteListField.GetValue(codelock);
            foreach (var friendUserId in friends.Where(p => !whitelist.Contains(p)))
            {
                whitelist.Add(friendUserId);
            }
        }

        object CanUseLock(BasePlayer player, BaseLock @codelock)
        {
            if (!(@codelock is CodeLock)) return null;
            var ownerLock = GetCodeLockOwner((CodeLock)@codelock);
            if (ownerLock == 0) return null;

            List<ulong> friends = Friends?.Call("ApiGetFriends", player.userID) as List<ulong>;
            if (friends == null) return null;
            if (friends.Contains(ownerLock))
                return true;
            return null;
        }
        ulong GetCodeLockOwner(CodeLock codelock)
        {
            var authorized = ((List<ulong>)whiteListField.GetValue(codelock));
            if (authorized.Count > 0)
                return authorized[0];
            if (codelock.OwnerID > 0)
                return codelock.OwnerID;
            return 0;
        }
        #endregion

        #region DATA

        /// <summary>
        /// Файл для сохранения текущего состояния плагина
        /// </summary>
        DynamicConfigFile saveFile = Interface.Oxide.DataFileSystem.GetFile("CupboardFriends");

        /// <summary>
        /// Список игроков, выключивших автоматическую авторизацию
        /// </summary>
        List<ulong> ignorePlayers;

        /// <summary>
        /// Загружает список игроков, выключивших автоматическую авторизацию
        /// </summary>
        void LoadData()=> ignorePlayers = saveFile.ReadObject<List<ulong>>() ?? new List<ulong>();

        /// <summary>
        /// Сохраняет текущее состояние плагина
        /// </summary>
        void OnServerSave() => SaveData();

        /// <summary>
        /// Сохраняет список игроков, выключивших автоматическую авторизацию
        /// </summary>
        void SaveData() => saveFile.WriteObject(ignorePlayers);

        #endregion

        #region LOCALIZATION

        /// <summary>
        /// Сообщения
        /// </summary>
        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "deployCupboardMessage", "Ваши друзья автоматически авторизированны в шкафу\nВыключить автоматическую авторизацию: /au"},
            { "deployCodelockMessage", "Ваши друзья автоматически авторизированны в замке\nВыключить автоматическую авторизацию: /au"},
            { "chatAUAddIgnoreMesssage", "Автоматически авторизация друзей в шкафах и замках выключена" },
            { "chatAURemoveIgnoreMesssage", "Автоматически авторизация друзей в шкафах и замках включена" }
        };

        #endregion
    }
}
