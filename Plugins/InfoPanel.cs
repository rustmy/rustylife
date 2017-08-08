// Reference: Oxide.Core.RustyCore
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConVar;
using Oxide.Core;
using Oxide.Core.Plugins;
using RustyCore;
using RustyCore.Plugins;
using RustyCore.Utils;

namespace Oxide.Plugins
{
    [Info("InfoPanel", "bazuka5801", "1.0.0")]
    public class InfoPanel : RustPlugin, ICuiPlugin
    {
        #region Fields

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        private int sleepers;
        private int online;
        private int queue;

        #endregion

        #region Oxide Hooks

        private Dictionary<string, string> messageImages = new Dictionary<string, string>();

        protected override void LoadDefaultConfig()
        {
            List<object> images = new List<object>();
            Config.GetVariable("IMAGES", out images, new List<object>() {});
            for (var i = 0; i < images.Count; i++)
            {
                var img = images[i];
                messageImages[$"message.img{i}"] = img.ToString();
            }
            Config.Save();
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            if (messageImages.Count == 0)
            {
                PrintError("Вставьте ссылку на картинку в конфиг!");
                Server.Command("oxide.unload", nameof(InfoPanel));
                return;
            }
            CommunityEntity.ServerInstance.StartCoroutine(LoadImage());
        }

        private int PngCurrent = 0;

        private void ChangePicture()
        {
            if (++PngCurrent == messageImages.Count) PngCurrent = 0;
            Redraw();
        }

        private bool loaded = false;

        IEnumerator LoadImage()
        {
            yield return CommunityEntity.ServerInstance.StartCoroutine(ImageStorage.Store(messageImages));
            timer.Every(60f, ChangePicture);
            loaded = true;
            Redraw();
        }

        void OnPlayerInit( BasePlayer player )
        {
            if (player.HasPlayerFlag( BasePlayer.PlayerFlags.ReceivingSnapshot ))
            {
                timer.Once( 2f, () => { OnPlayerInit( player ); } );
                return;
            }
            NextTick( Redraw );
        }

        void OnPlayerSleepEnded(BasePlayer player) => timer.Once(0.5f,()=>DrawUI(player));
        void OnPlayerDisconnected( BasePlayer player ) => NextTick(Redraw);
        void CanClientLogin(Network.Connection connection) => timer.Once(1f,Redraw);
        #endregion

        #region Core

        private void UpdateValues()
        {
            sleepers = BasePlayer.sleepingPlayerList.Count;
            online = Player.Players.Count;
            queue = ServerMgr.Instance.connectionQueue.Joining+ServerMgr.Instance.connectionQueue.Queued;
        }

        private void Redraw()
        {
            UpdateValues();
            foreach (var player in Player.Players.Where(p=>!p.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)))
            {
                DrawUI(player);
            }
        }

        #endregion

        #region UI

        void DrawUI(BasePlayer player)
        {
            if (!loaded)
            {
                timer.Once(0.1f,()=> DrawUI(player));
                return;
            }
            string imageStr = string.Join("",
                new[] {sleepers.ToString("000"), online.ToString("000"), queue.ToString("000")});
            var args = GetImages(imageStr);
            Array.Resize( ref args, args.Length + 1 );
            args[ args.Length - 1 ] = messageImages.ElementAt(PngCurrent).Value;
            core.DrawUI(player, "InfoPanel", "main", args );
        }

        void DestroyUI(BasePlayer player)
        {
            core.DestroyUI(player, "InfoPanel", "main");
        }

        [HookMethod( "OnCuiGeneratorInitialized" )]
        public void OnCuiGeneratorInitialized()
        {
            Redraw();
        }

        [HookMethod( "OnPlayerAspectChanged" )]
        public void OnPlayerAspectChanged( BasePlayer player )
        {
            timer.Once(0.5f, ()=>DrawUI(player));
        }

        string[] GetImages( string value ) => value.Select( p => ImageStorage.FetchPng( p.ToString() ) ).ToArray();

        #endregion

    }
}
