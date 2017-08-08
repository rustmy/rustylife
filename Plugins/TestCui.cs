// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TestCui", "bazuka5801", "1.0.0")]
    class TestCui : RustPlugin
    {
        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        private bool uiState = false;

        #endregion
        float timestep;
        float maxtimestep;
        protected override void LoadDefaultConfig()
        {
            Config.GetVariable( "Timestep", out timestep, 0.02f );
            Config.GetVariable( "Max Timestep", out maxtimestep, 0.33333333f );
            SaveConfig();
        }
        Component comp;
        void OnServerInitialized()
        {
            LoadDefaultConfig();

            StringBuilder sb = new StringBuilder();
            foreach (var kvp in (Dictionary<uint, string>) typeof( StringPool ).GetField( "toString", BindingFlags.Static | BindingFlags.NonPublic ).GetValue( null ))
            {
                sb.Append($"{{ {kvp.Key}, \"{kvp.Value}\"}},\n");
            }
            LogToFile("main", sb.ToString(), this);
        }

        void Unload()
        {
            //UnityEngine.Object.Destroy( comp );
        }

        class main :MonoBehaviour
        {
            int count = 0;
            int second = 0;
            private void FixedUpdate()
            {
                count++;
                if (second != (int)Time.time)
                {
                    second = (int)Time.time;
                    Interface.Oxide.RootLogger.Write( Core.Logging.LogType.Info, $"FixedUpdate Calls per second: {count}" );
                    count = 0;
                }
            }
        }

        #region COMMANDS
        [ConsoleCommand("testcui.toggle")]
        void cmdTestCuiToggle(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            var player = arg.Player();
            if (player == null) return;
            timer.Every(1f,() =>
            {
                DrawUI(player);
            });
        }

        #endregion


        #region UI

        private int j = 0;

        Dictionary<string, string> images = new Dictionary<string, string>()
        {
            { "0", "http://i.imgur.com/ZZQ0zTi.png" },
            { "1", "http://i.imgur.com/p9Og8KR.png" },
            { "2", "http://i.imgur.com/VtqRckV.png" },
            { "3", "http://i.imgur.com/7bsYddH.png" },
            { "4", "http://i.imgur.com/AMW0GoA.png" },
            { "5", "http://i.imgur.com/YfjABMD.png" },
            { "6", "http://i.imgur.com/dy3hYhY.png" },
            { "7", "http://i.imgur.com/BnE846i.png" },
            { "8", "http://i.imgur.com/OjJxZCe.png" },
            { "9", "http://i.imgur.com/68aDKH9.png" },
        };



        void DrawUI(BasePlayer player)
        {
            var imgs = GetImages(j++);
            core.DrawUI( player, "TestCui", "imgtext", imgs );
        }

        string[] GetImages(int value)
        {
            Puts( value.ToString( "00000" ) );
            return value.ToString("00000").Select(p => images[$"testcui{p}"]).ToArray();
        }

        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI( player, "TestCui", "imgtext" );
        }

        #endregion

        #region CUI
        //""png"": ""3375367014""
        
        List<string> list = new List<string>()
        {
            "assets/content/image effects/lens dirt/lensdirt1.png",
            "assets/content/image effects/lens dirt/lensdirt10.png",
            "assets/content/image effects/lens dirt/lensdirt11.png",
            "assets/content/image effects/lens dirt/lensdirt12.png",
            "assets/content/image effects/lens dirt/lensdirt13.png",
            "assets/content/image effects/lens dirt/lensdirt14.png",
            "assets/content/image effects/lens dirt/lensdirt15.png",
            "assets/content/image effects/lens dirt/lensdirt16.png",
            "assets/content/image effects/lens dirt/lensdirt2.png",
            "assets/content/image effects/lens dirt/lensdirt3.png",
            "assets/content/image effects/lens dirt/lensdirt4.png",
            "assets/content/image effects/lens dirt/lensdirt5.png",
            "assets/content/image effects/lens dirt/lensdirt6.png",
            "assets/content/image effects/lens dirt/lensdirt7.png",
            "assets/content/image effects/lens dirt/lensdirt8.png",
            "assets/content/image effects/lens dirt/lensdirt9.png",
            "assets/content/materials/highlight.png",
            "assets/content/ui/developer/developmentskin/devpanelbg.png",
            "assets/content/ui/developer/developmentskin/devtab-active.png",
            "assets/content/ui/developer/developmentskin/devtab-bright.png",
            "assets/content/ui/developer/developmentskin/devtab-normal.png",
            "assets/content/ui/facepunch-darkbg.png",
            "assets/content/ui/ingame/ui.crosshair.circle.png",
            "assets/content/ui/imgtext menu/rustlogo-blurred.png",
            "assets/content/ui/imgtext menu/rustlogo-normal-transparent.png",
            "assets/content/ui/map/fogofwarbrush.png",
            "assets/content/ui/menu/ui.logo.big.png",
            "assets/content/ui/menu/ui.menu.logo.png",
            "assets/content/ui/menu/ui.menu.rateus.background.png",
            "assets/content/ui/overlay_binocular.png",
            "assets/content/ui/overlay_bleeding.png",
            "assets/content/ui/overlay_freezing.png",
            "assets/content/ui/overlay_helmet_slit.png",
            "assets/content/ui/overlay_poisoned.png",
            "assets/content/ui/overlay_scope_1.png",
            "assets/content/ui/tiledpatterns/circles.png",
            "assets/content/ui/tiledpatterns/stripe_reallythick.png",
            "assets/content/ui/tiledpatterns/stripe_slight.png",
            "assets/content/ui/tiledpatterns/stripe_slight_thick.png",
            "assets/content/ui/tiledpatterns/stripe_thick.png",
            "assets/content/ui/tiledpatterns/stripe_thin.png",
            "assets/content/ui/tiledpatterns/swirl_pattern.png",
            "assets/content/ui/ui.icon.rust.png",
            "assets/icons/add.png",
            "assets/icons/ammunition.png",
            "assets/icons/arrow_right.png",
            "assets/icons/authorize.png",
            "assets/icons/bite.png",
            "assets/icons/bleeding.png",
            "assets/icons/blueprint.png",
            "assets/icons/blueprint_underlay.png",
            "assets/icons/blunt.png",
            "assets/icons/bp-lock.png",
            "assets/icons/broadcast.png",
            "assets/icons/build/stairs.png",
            "assets/icons/build/wall.doorway.door.png",
            "assets/icons/build/wall.window.bars.png",
            "assets/icons/bullet.png",
            "assets/icons/cart.png",
            "assets/icons/change_code.png",
            "assets/icons/circle_closed.png",
            "assets/icons/circle_gradient.png",
            "assets/icons/circle_open.png",
            "assets/icons/clear.png",
            "assets/icons/clear_list.png",
            "assets/icons/close.png",
            "assets/icons/close_door.png",
            "assets/icons/clothing.png",
            "assets/icons/cold.png",
            "assets/icons/community_servers.png",
            "assets/icons/connection.png",
            "assets/icons/construction.png",
            "assets/icons/cooking.png",
            "assets/icons/cup_water.png",
            "assets/icons/deauthorize.png",
            "assets/icons/demolish.png",
            "assets/icons/demolish_cancel.png",
            "assets/icons/demolish_immediate.png",
            "assets/icons/download.png",
            "assets/icons/drop.png",
            "assets/icons/drowning.png",
            "assets/icons/eat.png",
            "assets/icons/electric.png",
            "assets/icons/embrella.png",
            "assets/icons/examine.png",
            "assets/icons/exit.png",
            "assets/icons/explosion.png",
            "assets/icons/extinguish.png",
            "assets/icons/facebook.png",
            "assets/icons/facepunch.png",
            "assets/icons/fall.png",
            "assets/icons/favourite_servers.png",
            "assets/icons/file.png",
            "assets/icons/flags/af.png",
            "assets/icons/flags/ar.png",
            "assets/icons/flags/ca.png",
            "assets/icons/flags/cs.png",
            "assets/icons/flags/da.png",
            "assets/icons/flags/de.png",
            "assets/icons/flags/el.png",
            "assets/icons/flags/en-pt.png",
            "assets/icons/flags/en.png",
            "assets/icons/flags/es-es.png",
            "assets/icons/flags/fi.png",
            "assets/icons/flags/fr.png",
            "assets/icons/flags/he.png",
            "assets/icons/flags/hu.png",
            "assets/icons/flags/it.png",
            "assets/icons/flags/ja.png",
            "assets/icons/flags/ko.png",
            "assets/icons/flags/nl.png",
            "assets/icons/flags/no.png",
            "assets/icons/flags/pl.png",
            "assets/icons/flags/pt-br.png",
            "assets/icons/flags/pt-pt.png",
            "assets/icons/flags/ro.png",
            "assets/icons/flags/ru.png",
            "assets/icons/flags/sr.png",
            "assets/icons/flags/sv-se.png",
            "assets/icons/flags/tr.png",
            "assets/icons/flags/uk.png",
            "assets/icons/flags/vi.png",
            "assets/icons/flags/zh-cn.png",
            "assets/icons/flags/zh-tw.png",
            "assets/icons/fog.png",
            "assets/icons/folder.png",
            "assets/icons/folder_up.png",
            "assets/icons/fork_and_spoon.png",
            "assets/icons/freezing.png",
            "assets/icons/friends_servers.png",
            "assets/icons/gear.png",
            "assets/icons/grenade.png",
            "assets/icons/greyout.png",
            "assets/icons/greyout_large.png",
            "assets/icons/health.png",
            "assets/icons/history_servers.png",
            "assets/icons/home.png",
            "assets/icons/horse_ride.png",
            "assets/icons/hot.png",
            "assets/icons/ignite.png",
            "assets/icons/info.png",
            "assets/icons/inventory.png",
            "assets/icons/isbroken.png",
            "assets/icons/iscooking.png",
            "assets/icons/isonfire.png",
            "assets/icons/key.png",
            "assets/icons/knock_door.png",
            "assets/icons/lan_servers.png",
            "assets/icons/level.png",
            "assets/icons/level_metal.png",
            "assets/icons/level_stone.png",
            "assets/icons/level_top.png",
            "assets/icons/level_wood.png",
            "assets/icons/light_campfire.png",
            "assets/icons/lightbulb.png",
            "assets/icons/loading.png",
            "assets/icons/lock.png",
            "assets/icons/loot.png",
            "assets/icons/maparrow.png",
            "assets/icons/market.png",
            "assets/icons/maximum.png",
            "assets/icons/meat.png",
            "assets/icons/medical.png",
            "assets/icons/menu_dots.png",
            "assets/icons/modded_servers.png",
            "assets/icons/occupied.png",
            "assets/icons/open.png",
            "assets/icons/open_door.png",
            "assets/icons/peace.png",
            "assets/icons/pickup.png",
            "assets/icons/pills.png",
            "assets/icons/player_assist.png",
            "assets/icons/player_carry.png",
            "assets/icons/player_loot.png",
            "assets/icons/poison.png",
            "assets/icons/portion.png",
            "assets/icons/power.png",
            "assets/icons/radiation.png",
            "assets/icons/rain.png",
            "assets/icons/reddit.png",
            "assets/icons/refresh.png",
            "assets/icons/resource.png",
            "assets/icons/rotate.png",
            "assets/icons/rust.png",
            "assets/icons/save.png",
            "assets/icons/shadow.png",
            "assets/icons/sign.png",
            "assets/icons/slash.png",
            "assets/icons/sleeping.png",
            "assets/icons/sleepingbag.png",
            "assets/icons/square_gradient.png",
            "assets/icons/stab.png",
            "assets/icons/steam_inventory.png",
            "assets/icons/stopwatch.png",
            "assets/icons/store.png",
            "assets/icons/study.png",
            "assets/icons/subtract.png",
            "assets/icons/target.png",
            "assets/icons/tools.png",
            "assets/icons/traps.png",
            "assets/icons/twitter.png",
            "assets/icons/unlock.png",
            "assets/icons/upgrade.png",
            "assets/icons/voice.png",
            "assets/icons/vote_down.png",
            "assets/icons/vote_up.png",
            "assets/icons/warning.png",
            "assets/icons/weapon.png",
            "assets/icons/wet.png",
            "assets/icons/workshop.png",
            "assets/icons/xp.png",
            "assets/prefabs/building core/floor.frame/floor.frame.png",
            "assets/prefabs/building core/floor.triangle/floor.triangle.png",
            "assets/prefabs/building core/floor/floor.png",
            "assets/prefabs/building core/foundation.steps/foundation.steps.png",
            "assets/prefabs/building core/foundation.triangle/foundation.triangle.png",
            "assets/prefabs/building core/foundation/foundation.png",
            "assets/prefabs/building core/pillar/pillar.png",
            "assets/prefabs/building core/roof/roof.png",
            "assets/prefabs/building core/stairs.l/stairs_l.png",
            "assets/prefabs/building core/stairs.u/stairs_u.png",
            "assets/prefabs/building core/wall.doorway/wall.doorway.png",
            "assets/prefabs/building core/wall.frame/wall.frame.png",
            "assets/prefabs/building core/wall.low/wall.third.png",
            "assets/prefabs/building core/wall.window/wall.window.png",
            "assets/prefabs/building core/wall/wall.png",
            "assets/standard assets/effects/imageeffects/textures/color correction ramp.png",
            "assets/standard assets/effects/imageeffects/textures/contrastenhanced3d16.png",
            "assets/standard assets/effects/imageeffects/textures/grayscale ramp.png",
            "assets/standard assets/effects/imageeffects/textures/motionblurjitter.png",
            "assets/standard assets/effects/imageeffects/textures/neutral3d16.png",
            "assets/standard assets/effects/imageeffects/textures/noise.png",
            "assets/standard assets/effects/imageeffects/textures/noiseandgrain.png",
            "assets/standard assets/effects/imageeffects/textures/noiseeffectgrain.png",
            "assets/standard assets/effects/imageeffects/textures/noiseeffectscratch.png",
            "assets/standard assets/effects/imageeffects/textures/randomvectors.png",
            "assets/standard assets/effects/imageeffects/textures/vignettemask.png",

        };
        #endregion
    }
}
