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
    [Info("AFKManager", "bazuka5801", "1.0.0")]
    class AFKManager : RustPlugin
    {
        #region CLASSES

        class AFKPlayer
        {
            public BasePlayer Moderator { get; private set; }
            public BasePlayer Suspect { get; private set; }
            public int seconds { get; private set; }

            private Vector3 startPosition;
            private Vector3 endPosition;
            private Vector3 lastPosition;
            private bool Move = false;

            public AFKPlayer(BasePlayer moder, BasePlayer suspect, int seconds = 30)
            {
                this.Moderator = moder;
                this.Suspect = suspect;
                this.seconds = seconds;
                startPosition = suspect.transform.position;
                lastPosition = startPosition;
            }

            public void Step()
            {
                if (--seconds > 0)
                {
                    var position = Suspect.GetNetworkPosition();
                    if (Vector3.Distance(position, lastPosition) > 0.1f)
                    {
                        endPosition = position;
                        Move = true;
                        seconds = 0;
                        return;
                    }
                }
                else if (seconds == 0)
                {
                    endPosition = Suspect.transform.position;
                }
            }

            public bool IsFinish() => seconds <= 0;

            public string Result() => $"****Отчёт****\n{Suspect.displayName} {(Move ? "двигался" : "АФК")}\nСместился на {Vector3.Distance(startPosition,endPosition):F1}m";
        }

        #endregion


        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        const string MODER_PERM = "chatplus.moder";

        List<AFKPlayer> afkQueue = new List<AFKPlayer>();

        #endregion

        #region Commands

        [ChatCommand("online.check")]
        void cmdOnlineCheck(BasePlayer player, string command, string[] args)
        {
            if (!PermissionService.HasPermission(player.userID, MODER_PERM))
            {
                SendReply(player, Messages["notallowed"]);
                return;
            }
            if (args.Length != 1)
            {
                SendReply(player, Messages["exampleCmdCheck"]);
                return;
            }
            var playerSuspect = core.FindBasePlayer(args[0]);
            if (playerSuspect == null)
            {
                SendReply(player, Messages["playerNotFound"]);
                return;
            }
            if (!playerSuspect.IsConnected)
            {
                SendReply(player, Messages["playerNotConnected"]);
                return;
            }
            if (afkQueue.Any(afk => afk.Moderator == player))
            {
                SendReply(player, Messages["checkBusy"]);
                return;
            }
            afkQueue.Add(new AFKPlayer(player,playerSuspect));
            SendReply(player, Messages["checkStart"]);
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            timer.Every(1f, AFK_TimerHandler);
        }
        
        #endregion

        #region CORE

        void AFK_TimerHandler()
        {
            for (int i = afkQueue.Count - 1; i >= 0; i--)
            {
                var afk = afkQueue[i];
                afk.Step();

                if (afk.IsFinish())
                {
                    SendReply(afk.Moderator, afk.Result());
                    afkQueue.RemoveAt(i);
                    continue;
                }
            }
        }

        #endregion

        #region DATA

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "notallowed","У вас нет доступа к данной команде" },
            { "playerNotFound", "Игрок не найден!" },
            { "exampleCmdCheck", "Неправильно! Пример: /mute вася 15m" },
            { "checkStart", "Проверка началась, ожидайте..." },
            { "checkBusy", "Ошибка!\nВы уже проверяете другого игрока" },
            { "playerNotConnected", "Игрок не в сети!" }
        };

        #endregion
    }
}
