using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HeliManager", "bazuka5801", "1.0.0")]
    class HeliManager : RustPlugin
    {
        #region CONFIGURATION

        private const int PATROL_SECONDS = 20;
        private const int CALL_TIMEOUT = 30;

        #endregion

        #region FIELDS

        private bool spawning = false;
        private bool destroy = false;
        private Timer destroyPatrolTimer;
        private bool patrol;
        private float patrolNext;
        private float deathPatrol;
        #endregion

        #region COMMANDS

        [ChatCommand("heli")]
        void cmdChatHeli(BasePlayer player, string command, string[] args)
        {
            if (patrol)
            {
                int totalSeconds = Convert.ToInt32(deathPatrol - Time.realtimeSinceStartup);
                var span = TimeSpan.FromSeconds(totalSeconds);
                SendReply(player, string.Format(Messages["patrol"], span.Hours, span.Minutes, span.Seconds));
            }
            else
            {
                int patrolNextSeconds = Convert.ToInt32(this.patrolNext - Time.realtimeSinceStartup);
                var span = TimeSpan.FromSeconds(patrolNextSeconds);
                int patrolLastSeconds = Convert.ToInt32(CALL_TIMEOUT - (this.patrolNext - Time.realtimeSinceStartup));
                var span2 = TimeSpan.FromSeconds(patrolLastSeconds);
                SendReply(player, string.Format(Messages["patrolNext"], span.Hours, span.Minutes, span.Seconds, span2.Hours, span2.Minutes, span2.Seconds));
            }
        }
        
        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            patrolNext = Time.realtimeSinceStartup + CALL_TIMEOUT;
            timer.Every(CALL_TIMEOUT, () =>
            {
                spawning = true;
                HeliCall();
                patrolNext = Time.realtimeSinceStartup + CALL_TIMEOUT;
                deathPatrol = Time.realtimeSinceStartup + PATROL_SECONDS;
            });
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity.ShortPrefabName.Contains("patrolhelicopter") && !entity.ShortPrefabName.Contains("gibs"))
            {
                if (!spawning)
                {
                    destroy = true;
                    entity.Kill();
                    Puts("1");
                }
                else
                {
                    spawning = false;
                    patrol = true;
                    destroyPatrolTimer = timer.Once(PATROL_SECONDS, () => entity?.Kill());
                }
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity.ShortPrefabName.Contains("patrolhelicopter") && !entity.ShortPrefabName.Contains("gibs"))
            {
                if (destroy)
                {
                    destroy = false;
                    return;
                }
                patrol = false;
            }
        }

        #endregion

            #region CORE

        void HeliCall()
        {
            GameManager gameManager = GameManager.server;
            Vector3 vector3 = new Vector3();
            Quaternion quaternion = new Quaternion();
            BaseEntity baseEntity = gameManager.CreateEntity("Assets/Prefabs/NPC/Patrol Helicopter/PatrolHelicopter.prefab", vector3, quaternion, true);
            if (baseEntity)
            {
                baseEntity.Spawn();
            }
        }

        #endregion

        #region DATA

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "patrol", "Вертолет патрулирует в данный момент.\r\nДо конца патрулирования осталось: {0}ч. {1}м. {2}с."},
            { "patrolNext", "Время до патрулирования вертолета: {0}ч. {1}м. {2}с.\r\nПоследние патрулирование было: {3}ч. {4}м. {5}с. назад" }
        };

        #endregion
    }
}
