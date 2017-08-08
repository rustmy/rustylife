// Reference: Oxide.Core.RustyCore

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using RustyCore;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info( "Cupboard Notify", "bazuka5801","1.0.0")]
    public class CupboardNotify : RustPlugin
    {
        RCore core = Interface.Oxide.GetLibrary<RCore>();

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (go == null || plan == null) return;
            if (go.name != "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab") return;
            var player = plan.GetOwnerPlayer();
            core.DrawUI(player, "CupboardNotify", "main");
            timer.Once(15f, () => core.DestroyUI(player, "CupboardNotify", "main"));
        }
    }
}
