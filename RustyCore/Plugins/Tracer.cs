using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Oxide.Plugins;

namespace RustyCore.Plugins
{
    [Info("Tracer", "bazuka5801", "1.0.0")]
    class Tracer : RustPlugin
    {
        [ConsoleCommand("tracer.record")]
        void cmdTraceShow(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            TraceManager.StartRecord(arg.GetInt(0, 10));
        }
    }
}
