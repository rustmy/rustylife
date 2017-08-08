using Oxide.Core;
using Oxide.Core.Extensions;
using  RustyCore.Plugins;
using RustyCore.Utils;

namespace RustyCore
{
    public class RustyCoreExtension : Extension
    {
        public override string Author => "bazuka5801";

        public override string Name => "RustyCore";

        public override VersionNumber Version => new VersionNumber(1, 1, 0);

        internal RCore core;
        public RustyCoreExtension(ExtensionManager manager) : base(manager)
        {

        }

        public override void Load()
        {
            base.Manager.RegisterLibrary("RCore", core = new RCore());
            base.Manager.RegisterPluginLoader(new RustyCorePluginLoader(this));
        }

        public override void LoadPluginWatchers(string s)
        {

        }

        public override void OnModLoad()
        {
        }
    }
}
