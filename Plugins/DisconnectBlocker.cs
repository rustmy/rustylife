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
    [Info("DisconnectBlocker", "bazuka5801", "1.0.0")]
    class DisconnectBlocker : RustPlugin
    {
        #region FIELDS

        int RayLayer = LayerMask.GetMask(new string[] { "Construction", "Deployed", "Tree", "Terrain", "Resource", "World", "Default", "Prevent Building" });

        #endregion

        #region OXIDE HOOKS

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.GetCenter(), Vector3.down, out hit, 1000f, RayLayer))
            {
                player.transform.position = hit.point;
            }
        }

        #endregion
    }
}
