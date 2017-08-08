using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ConVar;
using Oxide.Core;
using Oxide.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;
using Console = System.Console;
using Graphics = System.Drawing.Graphics;
using LogType = Oxide.Core.Logging.LogType;

namespace RustyCore.Utils
{
    public static class ImageStorage
    {
        [ProtoContract]
        internal class ImageFile
        {
            [ProtoMember( 1 )]
            public string Url;
            [ProtoMember( 2 )]
            public string Png;
            [ProtoMember( 3 )]
            public byte[] Data;
        }

        private static Dictionary<string, ImageFile> ImageFiles = new Dictionary<string, ImageFile>();
        private static string StorageDirectory = string.Concat( Server.rootFolder, "/storage/" );
        private static string SaveFilename = string.Concat(StorageDirectory, "Images.bin" );
        private static Timer SaveTimer = null;

        internal static void Init()
        {
            ReadFiles();
            RemoveLastStorage();
            VerifyStorage();
            _GenerateClientCacheJson();
        }

        internal static void RemoveLastStorage()
        {
            var lastIdFile = Interface.Oxide.DataFileSystem.GetFile("Temp/ImageStorage");
            string str = string.Concat(Server.rootFolder, "/storage/", lastIdFile.ReadObject<uint>());
            if (Directory.Exists( str ))
            {
                ( new DirectoryInfo( str ) ).Delete( true );
                Logger.Info($"Папка \"{str}\" успешно удалена");

                foreach (var img in ImageFiles)
                {
                    img.Value.Png = UploadToStorage(img.Value.Data).ToString();
                }
            }
            lastIdFile.WriteObject(CommunityEntity.ServerInstance.net.ID);
        }

        private static void ReadFiles()
        {
            if (!File.Exists(SaveFilename))
            {
                ImageFiles = new Dictionary<string, ImageFile>();
                return;
            }
            try
            {
                using (FileStream stream = new FileStream(SaveFilename, FileMode.Open, FileAccess.Read))
                {
                    ImageFiles = Serializer.Deserialize<Dictionary<string, ImageFile>>(stream);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.Message + "\n" + e.StackTrace);
            }
        }

        private static void WriteFiles()
        {
            using (FileStream stream = new FileStream(SaveFilename, FileMode.Create, FileAccess.Write ))
            {
                Serializer.Serialize(stream, ImageFiles);
                stream.Flush();
            }
            _GenerateClientCacheJson();
        }

        private static void OnFilesChanged()
        {
            if (SaveTimer != null) RustyCore.Plugins.BaseCore.GetTimer().Destroy( ref SaveTimer );
            SaveTimer = RustyCore.Plugins.BaseCore.GetTimer().Once( 20f, WriteFiles );
        }

        private static void VerifyStorage()
        {
            int uploaded = 0;
            foreach (var img in ImageFiles)
            {
                if (img.Value.Png != null && GetImageBytes(img.Key) == null)
                {
                    img.Value.Png = UploadToStorage(img.Value.Data).ToString();
                    OnFilesChanged();
                    uploaded++;
                }
            }
            if (uploaded > 0)
            {
                Logger.Info($"Подгрузил {uploaded} текстур в FileStorage");
            }
        }

        private static void UpdateOrCreate(string name, string url, uint fileId, byte[] data)
        {
            ImageFile img;
            if (!ImageFiles.TryGetValue( name, out img )) ImageFiles[ name ] = img = new ImageFile();
            img.Url = url;
            img.Png = fileId.ToString();
            img.Data = data;
            OnFilesChanged();
        }

        public static IEnumerator Store(Dictionary<string, string> images)
        {
            foreach (var img in images.ToList())
            {
                yield return CommunityEntity.ServerInstance.StartCoroutine(Store(img.Key, img.Value));
                images[img.Key] = FetchPng(img.Key);
            }
        }

        public static IEnumerator Store(string name, string url, int size = -1)
        {
            ImageFile img;
            if (ImageFiles.TryGetValue(name, out img))
            {
                if (img.Url == url && !string.IsNullOrEmpty(img.Png))
                {
                    yield break;
                }
            }
            else
            {
                ImageFiles[name] = new ImageFile { Url = url};
            }
            yield return CommunityEntity.ServerInstance.StartCoroutine(LoadImageCoroutine( name, url, size));
        }

        public static string FetchPng(string name)
        {
            ImageFile img;
            ImageFiles.TryGetValue(name, out img );
            return img?.Png ?? string.Empty;
        }

        static IEnumerator LoadImageCoroutine(string name, string url, int size = -1)
        {
            using (WWW www = new WWW(url))
            {
                yield return www;
                if (string.IsNullOrEmpty(www.error))
                {
                    var bytes = size == -1 ? www.bytes : Resize(www.bytes, size);
                    var fileId = UploadToStorage(bytes);
                    UpdateOrCreate(name, url, fileId, bytes);
                }
            }
        }

        static byte[] Resize(byte[] bytes, int size)
        {
            Image img = (Bitmap)(new ImageConverter().ConvertFrom(bytes));
            Bitmap cutPiece = new Bitmap(size, size);
            Graphics graphic = Graphics.FromImage(cutPiece);
            graphic.DrawImage(img, new Rectangle(0, 0, size, size), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
            graphic.Dispose();
            MemoryStream ms = new MemoryStream();
            cutPiece.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }

        private static uint UploadToStorage(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                stream.Position = 0;
                stream.SetLength( 0 );
                stream.Write(data, 0, data.Length);
                return FileStorage.server.Store(stream, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
            }
        }

        public static List<byte> GetImageBytes(string name)
        {
            ImageFile file;
            if (!ImageFiles.TryGetValue(name, out file ))
            {
                return null;
            }
            return FileStorage.server.Get(uint.Parse(file.Png), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID)?.ToList();
        }


        internal static void OnPlayerConnected(BasePlayer player)
        {
            //CuiHelper.AddUi(player, CLIENT_CACHE_JSON);
            //CuiHelper.DestroyUi(player, "ImageCachePanel");
        }

        #region CLIENT CACHE JSON

        private static void _GenerateClientCacheJson()
        {
            var cont = new CuiElementContainer
            {
                new CuiElement
                {
                    Name = "ImageCachePanel",
                    Parent = "Hud",
                    Components =
                    {
                        new CuiRawImageComponent {Color = "0 0 0 0"},
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0.1 0.1"}
                    }
                }
            };
            cont.AddRange(ImageFiles.Values.Select(img => new CuiElement
            {
                Name = "ImageCache",
                Parent = "ImageCachePanel",
                Components =
                {
                    new CuiRawImageComponent {Color = "0 0 0 0", Png = img.Png},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0.1 0.1"}
                }
            }));
            CLIENT_CACHE_JSON = CuiHelper.ToJson(cont);
        }

        private static string CLIENT_CACHE_JSON;

        #endregion
    }
}
