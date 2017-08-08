// Reference: Oxide.Core.RustyCore
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RustyCore.Utils;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoCollision","bazuka5801","1.0.0")]
    class NoCollision : RustPlugin
    {
        bool ignoreCollision;
        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Игнорировать коллизию World Item", out ignoreCollision, true);
            SaveConfig();
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            Physics.IgnoreLayerCollision(26,26,ignoreCollision);
        }
    }
}
