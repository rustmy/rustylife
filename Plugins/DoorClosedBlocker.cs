using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Plugins;

namespace Oxide.Plugins
{
    [Info("DoorClosedBlocker", "bazuka5801", "1.0.0")]
    class DoorClosedBlocker : RustPlugin
    {
        [PluginReference]
        private Plugin AutoDoorClosed;

        private DynamicConfigFile saveFile = Interface.Oxide.DataFileSystem.GetFile("DoorClosedBlocker");

        List<ulong> playersBlocked = new List<ulong>();

        private const int BLOCK_SECONDS = 600;

        void Unload()
        {
            saveFile.WriteObject(playersBlocked);
        }

        void OnServerInitialized()
        {
            playersBlocked = saveFile.ReadObject<List<ulong>>() ?? new List<ulong>();
            foreach (var playerId in playersBlocked)
            {
                RemoveIgnore(playerId);
            }
            playersBlocked.Clear();
        }

        void CanBeWounded(BasePlayer victim, HitInfo info)
        {
            OnWoundedOrDeath(victim, info);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var victim = entity as BasePlayer;
            OnWoundedOrDeath(victim, info);
        }

        void OnWoundedOrDeath(BasePlayer victim, HitInfo info)
        {
            if (info == null || victim == null) return;

            if (info.Initiator is BaseNPC) return; // if animals are off and the initiator is an animal, return

            var killer = info.Initiator as BasePlayer;
            if (killer == null || killer == victim) return;

            if (!playersBlocked.Contains(victim.userID))
            {
                var userId = victim.userID;
                AddIgnore(victim.userID);
                timer.Once(BLOCK_SECONDS, () =>
                {
                    playersBlocked.Remove(userId);
                    RemoveIgnore(userId);
                });
            }
        }

        void AddIgnore(ulong playerId)
        {
            AutoDoorClosed.Call("ApiAddIgnore", playerId);
        }

        void RemoveIgnore(ulong playerId)
        {
            AutoDoorClosed.Call("ApiRemoveIgnore", playerId);
        }
    }
}
