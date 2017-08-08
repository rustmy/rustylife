using System;
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Configuration;
using System.Reflection;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
    class BlockBuildInBuildingBlock : RustPlugin
    {

        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;
            var privilege = player.GetBuildingPrivilege();
            if (privilege != null && privilege.authorizedPlayers.Count(p => p.userid == player.userID) == 0)
            {
                player.ChatMessage("Строительство в BuildingBlocked запрещено!");
                entity.Kill();
                return;
            }
        }
    }
}
