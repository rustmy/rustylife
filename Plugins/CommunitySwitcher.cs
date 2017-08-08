using System.Reflection;
using Rust;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("CommunitySwitcher", "bazuka5801", "1.0.0")]
    class CommunitySwitcher : RustPlugin
    {
        #region FIELDS

        private readonly MethodInfo UpdateServerInformationMethod =
            typeof(ServerMgr).GetMethod("UpdateServerInformation", BindingFlags.Instance | BindingFlags.NonPublic);

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            CommunityEntity.ServerInstance.CancelInvoke("UpdateServerInformation");
            timer.Every(10f, () => {
                UpdateServerInformationMethod.Invoke(CommunityEntity.ServerInstance, new object[] {});
                ModifyTags(false,10000,10000);
            });
        }

        void Unload()
        {
            CommunityEntity.ServerInstance.InvokeRepeating("UpdateServerInformation", 1f, 10f);
        }


        void ModifyTags(bool modded, int cp, int mp)
        {
            string tags = Global.SteamServer.GameTags;

            if (!modded)
            {
                tags = tags.Replace("oxide,modded,", "");
            }
            else
            {
                if (!tags.Contains("oxide,modded,"))
                {
                    tags = tags.Insert(0, "oxide,modded,");
                }
            }

            tags = Regex.Replace(tags, "cp\\d{1,}", $"cp{cp}");
            tags = Regex.Replace(tags, "mp\\d{1,}", $"mp{mp}");

            Global.SteamServer.GameTags = tags;
        }

        #endregion
    }
}
