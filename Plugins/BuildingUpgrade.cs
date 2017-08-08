// Reference: Oxide.Core.RustyCore

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using RustyCore;
using RustyCore.Utils;
using TAA;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info( "BuildingUpgrade", "bazuka5801", "1.0.0" )]
    class BuildingUpgrade : RustPlugin
    {
        private RCore core = Interface.Oxide.GetLibrary<RCore>();

        private FastMethodInfo payForUpgrade =
            new FastMethodInfo( typeof( BuildingBlock ).GetMethod( "PayForUpgrade",
                BindingFlags.Instance | BindingFlags.NonPublic ) );

        private FastMethodInfo getGrade =
            new FastMethodInfo( typeof( BuildingBlock ).GetMethod( "GetGrade",
                BindingFlags.Instance | BindingFlags.NonPublic ) );

        private FastMethodInfo canUpgrade =
            new FastMethodInfo( typeof( BuildingBlock ).GetMethod( "CanAffordUpgrade",
                BindingFlags.Instance | BindingFlags.NonPublic ) );

        #region Fields

        Dictionary<BuildingGrade.Enum, string> gradesString = new Dictionary<BuildingGrade.Enum, string>()
        {
            {BuildingGrade.Enum.Wood, "Дерево"},
            {BuildingGrade.Enum.Stone, "Камень"},
            {BuildingGrade.Enum.Metal, "Металл"},
            {BuildingGrade.Enum.TopTier, "Армор"},
            {BuildingGrade.Enum.Count, "Ремув"}
        };

        Dictionary<BasePlayer, BuildingGrade.Enum> grades = new Dictionary<BasePlayer, BuildingGrade.Enum>();
        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();

        Dictionary<string, string> gradeImages = new Dictionary<string, string>()
        {
            { "building.upgrade.1", "http://i.imgur.com/Oc3m9k7.png"},
            { "building.upgrade.2", "http://i.imgur.com/3BWQWLe.png"},
            { "building.upgrade.3", "http://i.imgur.com/gy3s5Jh.png"},
            { "building.upgrade.4", "http://i.imgur.com/FBD2El8.png"},
            { "building.upgrade.5", "http://i.imgur.com/Lva2d3R.png"},
        };
        bool loaded = false;
        #endregion

        #region CONFIGURATION

        int resetTime;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable( "Через сколько секунд автоматически выключать улучшение строений", out resetTime, 40 );
            SaveConfig();
        }

        #endregion


        #region COMMANDS

        [ChatCommand( "buildingupgrade" )]
        void cmdBuildingUpgrade( BasePlayer player, string command, string[] args )
        {
            if (player == null) return;

            int grade;
            if (!grades.ContainsKey( player ))
                grade = (int) ( grades[ player ] = BuildingGrade.Enum.Wood );
            else
            {
                grade = (int) grades[ player ];
                grade++;
                grades[ player ] = (BuildingGrade.Enum) Mathf.Clamp( grade, 1, 5 );
            }
            if (grade > 5)
            {
                grades.Remove( player );
                timers.Remove( player );
                DestroyUI( player );
                DeactivateRemove( player.userID );
                return;
            }

            if (grade == 5)
            {
                ActivateRemove( player.userID );
            }

            timers[ player ] = resetTime;
            DrawUI( player, (BuildingGrade.Enum) grade, resetTime );
        }

        #endregion

        #region OXIDE HOOKS


        void OnServerInitialized()
        {
            LoadDefaultConfig();
            timer.Every( 1f, GradeTimerHandler );
            CommunityEntity.ServerInstance.StartCoroutine( StoreImages() );
        }

        IEnumerator StoreImages()
        {
            yield return ImageStorage.Store( gradeImages );
            loaded = true;
        }

        void OnEntityBuilt( Planner planner, GameObject gameobject )
        {
            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            BuildingBlock entity = gameobject.ToBaseEntity() as BuildingBlock;
            if (entity == null || entity.IsDestroyed) return;
            if (player == null) return;
            Grade( entity, player );
        }

        void Grade( BuildingBlock block, BasePlayer player )
        {
            BuildingGrade.Enum grade;
            if (!grades.TryGetValue( player, out grade ) || grade == BuildingGrade.Enum.Count)
                return;
            if (block == null) return;
            if (!( (int) grade >= 1 && (int) grade <= 4 )) return;
            if ((bool) canUpgrade.Invoke( block, grade, player ))
            {
                var ret = Interface.Call( "CanUpgrade", player ) as string;
                if (ret != null)
                {
                    SendReply( player, ret );
                    return;
                }
                payForUpgrade.Invoke( block, getGrade.Invoke( block, grade ), player );
                block.SetGrade( grade );
                block.SetHealthToMax();
                block.UpdateSkin( false );
                Effect.server.Run(
                    string.Concat( "assets/bundled/prefabs/fx/build/promote_", grade.ToString().ToLower(), ".prefab" ),
                    block,
                    0, Vector3.zero, Vector3.zero, null, false );
                timers[ player ] = resetTime;
                DrawUI( player, grade, resetTime );

            }
            else
            {
                player.ChatMessage( "<color=ffcc00><size=16>Для улучшения нехватает ресурсов!!!</size></color>" );
            }
        }

        #endregion

        #region CORE

        int NextGrade( int grade ) => ++grade;

        void GradeTimerHandler()
        {
            foreach (var player in timers.Keys.ToList())
            {
                var seconds = --timers[ player ];
                if (seconds <= 0)
                {
                    BuildingGrade.Enum mode;
                    if (grades.TryGetValue( player, out mode ) && mode == BuildingGrade.Enum.Count)
                        DeactivateRemove( player.userID );
                    grades.Remove( player );
                    timers.Remove( player );
                    DestroyUI( player );
                    continue;
                }
                DrawUI( player, grades[ player ], seconds );
            }
        }

        #endregion


        #region UI

        void DrawUI( BasePlayer player, BuildingGrade.Enum grade, int seconds )
        {
            if (!loaded) return;
            string imgString = seconds.ToString( "00" );
            string image1 = ImageStorage.FetchPng( imgString[ 0 ].ToString() );
            string image2 = ImageStorage.FetchPng( imgString[ 1 ].ToString() );
            core.DrawUI( player, "BuildingUpgrade", "menu", gradeImages[ $"building.upgrade.{(int) grade}" ], image1, image2 );
        }

        void DestroyUI( BasePlayer player )
        {
            core.DestroyUI( player, "BuildingUpgrade", "menu" );
        }


        #endregion

        #region API

        void UpdateTimer( BasePlayer player )
        {
            timers[ player ] = resetTime;
            DrawUI( player, grades[ player ], timers[ player ] );
        }

        void ToggleRemove( BasePlayer player )
        {
            BuildingGrade.Enum grade;
            if (!grades.TryGetValue( player, out grade )) grade = BuildingGrade.Enum.None;
            if (grade != BuildingGrade.Enum.Count)
            {
                grades[ player ] = BuildingGrade.Enum.Count;
                timers[ player ] = resetTime;
                DrawUI( player, BuildingGrade.Enum.Count, resetTime );
                ActivateRemove( player.userID );
            }
            else
            {
                grades.Remove( player );
                timers.Remove( player );
                DestroyUI( player );
                DeactivateRemove( player.userID );
            }
        }

        #endregion

        #region Remove

        [PluginReference] private Plugin Remove;

        void ActivateRemove( ulong userId ) => Remove?.Call( "ActivateRemove", userId );
        void DeactivateRemove( ulong userId ) => Remove?.Call( "DeactivateRemove", userId );

        #endregion
    }
}
