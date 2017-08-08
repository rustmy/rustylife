// Reference: System.Drawing
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info( "AutoGrade", "bazuka5801", "1.0.0" )]
    class AutoGrade : RustPlugin
    {
        private MethodInfo payForUpgrade =
            typeof( BuildingBlock ).GetMethod( "PayForUpgrade",
                BindingFlags.Instance | BindingFlags.NonPublic );

        private MethodInfo getGrade =
            typeof( BuildingBlock ).GetMethod( "GetGrade",
                BindingFlags.Instance | BindingFlags.NonPublic );

        private MethodInfo canUpgrade =
            typeof( BuildingBlock ).GetMethod( "CanAffordUpgrade",
                BindingFlags.Instance | BindingFlags.NonPublic );

        #region Fields

        Dictionary<BuildingGrade.Enum, string> gradesString = new Dictionary<BuildingGrade.Enum, string>()
        {
            {BuildingGrade.Enum.Wood, "Дерево"},
            {BuildingGrade.Enum.Stone, "Камень"},
            {BuildingGrade.Enum.Metal, "Металл"},
            {BuildingGrade.Enum.TopTier, "Армор"}
        };

        Dictionary<BasePlayer, BuildingGrade.Enum> grades = new Dictionary<BasePlayer, BuildingGrade.Enum>();
        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();

        Dictionary<string, string> gradeImages = new Dictionary<string, string>()
        {
            { "building.upgrade.1", ""},
            { "building.upgrade.2", ""},
            { "building.upgrade.3", ""},
            { "building.upgrade.4", "http://example.png"},
        };
        bool loaded = false;
        #endregion

        #region CONFIGURATION

        int resetTime;

        protected override void LoadDefaultConfig()
        {
            GetVariable( Config, "Через сколько секунд автоматически выключать улучшение строений", out resetTime, 40 );
            SaveConfig();
        }
        public static void GetVariable<T>( DynamicConfigFile config, string name, out T value, T defaultValue )
        {
            config[ name ] = value = config[ name ] == null ? defaultValue : (T) Convert.ChangeType( config[ name ], typeof( T ) );
        }

        #endregion


        #region COMMANDS

        [ChatCommand( "autograde" )]
        void cmdAutoGrade( BasePlayer player, string command, string[] args )
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
            if (grade > 4)
            {
                grades.Remove( player );
                timers.Remove( player );
                DestroyUI( player );
                return;
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
            InitFileManager();
            CommunityEntity.ServerInstance.StartCoroutine( StoreImages() );
        }

        IEnumerator StoreImages()
        {
            foreach (var img in gradeImages)
            {
                yield return m_FileManager.LoadFile( img.Key, img.Value );

            }
            var keys = gradeImages.Keys.ToList();
            foreach (string t in keys)
            {
                gradeImages[ t ] = m_FileManager.GetPng( t );
            }
            PrintWarning($"Картинки загружены: {string.Join(", ", gradeImages.Values.ToArray())}");
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
            if ((bool) canUpgrade.Invoke( block, new object[] { grade, player } ))
            {
                var ret = Interface.Call( "CanUpgrade", player ) as string;
                if (ret != null)
                {
                    SendReply( player, ret );
                    return;
                }
                payForUpgrade.Invoke( block, new object[] { getGrade.Invoke( block, new object[] { grade } ), player } );
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
            DestroyUI( player );
            CuiHelper.AddUi( player,
                GUI.Replace( "{0}", gradesString[ grade ] ).Replace( "{1}", seconds.ToString() )
                    .Replace( "{2}", gradeImages[ $"building.upgrade.{(int) grade}" ] ) );
        }

        void DestroyUI( BasePlayer player )
        {
            CuiHelper.DestroyUi( player, "autograde" );
            CuiHelper.DestroyUi( player, "autogradetext" );
        }


        private string GUI = @"[{
	""name"": ""autograde"",
	""parent"": ""Overlay"",
	""components"": [{
		""type"": ""UnityEngine.UI.RawImage"",
        ""sprite"":""assets/content/textures/generic/fulltransparent.tga"",
		""png"": ""{2}""
	}, {
		""type"": ""RectTransform"",
		""anchormin"": ""0.6463542 0.02685203"",
		""anchormax"": ""0.7166667 0.151852"",
		""offsetmin"": ""0.5 0"",
		""offsetmax"": ""-0.5 0""
	}]
}, {
	""name"": ""autogradetext"",
	""parent"": ""Overlay"",
	""components"": [{
		""type"": ""UnityEngine.UI.Text"",
		""text"": ""{0}" + Environment.NewLine + @"{1} сек."",
		""fontSize"": 10,
		""align"": ""MiddleCenter""
	}, {
		""type"": ""UnityEngine.UI.Outline"",
		""color"": ""0 0 0 1"",
		""distance"": ""0.5 -0.5""
	}, {
		""type"": ""RectTransform"",
		""anchormin"": ""0.6463542 0.02685185"",
		""anchormax"": ""0.7166667 0.1518518"",
		""offsetmin"": ""0.5 0"",
		""offsetmax"": ""-0.5 0""
	}]
}]";

        #endregion

        #region API

        void UpdateTimer( BasePlayer player )
        {
            timers[ player ] = resetTime;
            DrawUI( player, grades[ player ], timers[ player ] );
        }

        #endregion
        private GameObject FileManagerObject;
        private FileManager m_FileManager;

        /// <summary>
        /// Инициализация скрипта взаимодействующего с файлами сервера
        /// </summary>
        void InitFileManager()
        {
            FileManagerObject = new GameObject( "MAP_FileManagerObject" );
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }
        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;

            public bool IsFinished => needed == loaded;
            const ulong MaxActiveLoads = 10;
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();

            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile( "AutoGradeImages" );

            private class FileInfo
            {
                public string Url;
                public string Png;
            }

            public void SaveData()
            {
                dataFile.WriteObject( files );
            }

            public string GetPng( string name ) => files[ name ].Png;

            private void Awake()
            {
                files = dataFile.ReadObject<Dictionary<string, FileInfo>>() ?? new Dictionary<string, FileInfo>();
            }

            public IEnumerator LoadFile( string name, string url, int size = -1 )
            {
                if (files.ContainsKey( name ) && files[ name ].Url == url && !string.IsNullOrEmpty( files[ name ].Png )) yield break;
                files[ name ] = new FileInfo() { Url = url };
                needed++;
                yield return StartCoroutine( LoadImageCoroutine( name, url, size ) );
            }

            IEnumerator LoadImageCoroutine( string name, string url, int size = -1 )
            {
                using (WWW www = new WWW( url ))
                {
                    yield return www;
                    using (MemoryStream stream = new MemoryStream())
                    {
                        if (string.IsNullOrEmpty( www.error ))
                        {
                            stream.Position = 0;
                            stream.SetLength( 0 );

                            var bytes = size == -1 ? www.bytes : Resize( www.bytes, size );

                            stream.Write( bytes, 0, bytes.Length );

                            var entityId = CommunityEntity.ServerInstance.net.ID;
                            var crc32 = FileStorage.server.Store( stream, FileStorage.Type.png, entityId ).ToString();
                            files[ name ].Png = crc32;
                        }
                    }
                }
                loaded++;
            }

            static byte[] Resize( byte[] bytes, int size )
            {
                Image img = (Bitmap) ( new ImageConverter().ConvertFrom( bytes ) );
                Bitmap cutPiece = new Bitmap( size, size );
                System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage( cutPiece );
                graphic.DrawImage( img, new Rectangle( 0, 0, size, size ), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel );
                graphic.Dispose();
                MemoryStream ms = new MemoryStream();
                cutPiece.Save( ms, ImageFormat.Jpeg );
                return ms.ToArray();
            }
        }

    }
}
