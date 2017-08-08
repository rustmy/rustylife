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
    [Info("WoundedFix", "bazuka5801", "1.0.0")]
    class WoundedFix : RustPlugin
    {
        void CanBeWounded(BasePlayer player)
        {
            Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_scream.prefab", player.transform.position, Vector3.zero, null, false);
        }
    }
}
