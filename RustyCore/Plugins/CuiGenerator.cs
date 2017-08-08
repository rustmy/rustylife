using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections;
using Network;
using RustyCore.Utils;
using Oxide.Plugins;
using LogType = Oxide.Core.Logging.LogType;

namespace RustyCore.Plugins
{
    public interface ICuiPlugin
    {
        void OnCuiGeneratorInitialized();
        void OnPlayerAspectChanged(BasePlayer player);
    }

    [Info("CuiGenerator","bazuka5801","2.0.0")]
    internal class CuiGenerator : RustPlugin
    {
        #region Fields

        Dictionary<string, Dictionary<string, CuiFunction>> functions;
        Dictionary<BasePlayer, List<UIData>> players = new Dictionary<BasePlayer, List<UIData>>();
        Dictionary<ulong, int> aspects = new Dictionary<ulong, int>(); //TODO: LOAD, SAVE

        bool isLoaded = false;

        bool isDebug = false;
        int drawCache = 0;
        int draw = 0;
        #endregion

        #region Data
            
        readonly DynamicConfigFile uiDB = Interface.Oxide.DataFileSystem.GetFile("Core/CuiGenerator");
        readonly DynamicConfigFile aspectDB = Interface.Oxide.DataFileSystem.GetFile("Core/CuiGenerator_aspectDB");
        readonly DynamicConfigFile imagesDB = Interface.Oxide.DataFileSystem.GetFile( "Core/Images" );

        #endregion

        #region Classes

        [Serializable]
        class CuiFunction
        {
            [JsonProperty("16x9")] public string _16x9 = "";
            [JsonProperty("16x10")] public string _16x10 = "";
            [JsonProperty("5x4")] public string _5x4 = "";
            [JsonProperty("4x3")] public string _4x3 = "";
            public readonly List<List<List<CuiElement>>> cacheArgs = new List<List<List<CuiElement>>>();

            private int _argc = -1;

            public int argc()
            {
                if (_argc >= 0) return _argc;
                _argc = 0;
                while (_16x9.Contains("{" + _argc + "}"))
                    _argc++;
                return _argc;
            }

            public string this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case 0:
                            return _16x9;
                        case 1:
                            return _16x10;
                        case 2:
                            return _5x4;
                        case 3:
                            return _4x3;
                        default:
                            return string.Empty;
                    }
                }
                set
                {
                    switch (index)
                    {
                        case 0:
                            _16x9 = value;
                            break;
                        case 1:
                            _16x10 = value;
                            break;
                        case 2:
                            _5x4 = value;
                            break;
                        case 3:
                            _4x3 = value;
                            break;
                        default:
                            return;
                    }
                }
            }
        }

        class UIData
        {

            public string plugin;
            public string funcName;
            public object[] args;
            public int aspect;

            public UIData(string plugin, string funcName, int aspect, params object[] args)
            {
                this.plugin = plugin;
                this.funcName = funcName;
                this.args = args;
                this.aspect = aspect;
            }

            public override string ToString()
                =>
                $"Plugin \"{plugin}\" Function \"{funcName}\" Args \"{string.Join(", ", args.Select(arg => arg.ToString()).ToArray())}\"";
        }

        #endregion

        #region COMMANDS

        [ConsoleCommand("cui.debug")]
        void cmdDebug(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            isDebug = !isDebug;
            Puts($"Cui Debug: {isDebug}");
        }

       /* [ConsoleCommand("wipestorage")]
        void cmdWipeStorage(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            ImageStorage.WipeStorage();
        }*/
        
        #endregion

        #region Oxide Hooks
            
        
        [HookMethod("OnServerInitialized")]
        private void OnServerInitialized()
        {
            LoadAspects();
            CommunityEntity.ServerInstance.StartCoroutine(LoadFunctions());
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
            foreach (var player in BasePlayer.sleepingPlayerList)
                OnPlayerInit(player);

            timer.Every(1f, () =>
            {
                if (isDebug)
                {
                    Puts($"CuiDraw: {draw}\nCuiDrawCache: {drawCache}");
                    draw = drawCache = 0;
                }
            });
        }

        [HookMethod("Unload")]
        private void Unload()
        {
            foreach (var player in players)
                DestroyAllUI(player.Key);
            SaveAspects();
        }

        private Dictionary<ulong, List<string>> uiCache = new Dictionary<ulong, List<string>>();

        void CacheUI(ulong userId, string json)
        {
            if (!uiCache.ContainsKey(userId))
                uiCache[userId] = new List<string>();
            var list = uiCache[userId];
            list.Add(json);
            if (list.Count > 5)
            {
                list.RemoveAt(0);
            }
        }

        void PutsCacheUI(ulong userId)
        {
            if (!uiCache.ContainsKey(userId)) return;
            var msg = string.Join("\n", uiCache[userId].ToArray());
            PrintError("AddUi error:\n"+msg);
        }

        private List<ulong> connected = new List<ulong>();

        [HookMethod("OnPlayerSleepEnded")]
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null) return;
            if (connected.Contains(player.userID))
            {
                ImageStorage.OnPlayerConnected( player );
                connected.Remove(player.userID);
            }
            if (!aspects.ContainsKey(player.userID))
                AspectUI(player);
        }

        [HookMethod("OnPlayerInit")]
        void OnPlayerInit(BasePlayer player)
        {
            connected.Add(player.userID);
            player.displayName = CleanName(player.displayName);
        }

        static string CleanName(string strIn)
        {
            // Replace invalid characters with empty strings.
            try
            {
                return Regex.Replace(strIn, @"\$|\@|\\|\/", "",
                    RegexOptions.None);
            }
            // If we timeout when replacing invalid characters, 
            // we should return Empty.
            catch
            {
                return strIn;
            }
        }

        [HookMethod("OnPlayerAddUiDisconnected")]
        void OnPlayerAddUiDisconnected(ulong userId, string reason)
        {
            if (reason.ToLower().Contains( "addui" ))
            {
                PutsCacheUI( userId );
            }
        }

        [ HookMethod("OnPlayerDisconnected")]
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!players.ContainsKey(player)) return;
            players.Remove(player);
        }

        //[HookMethod("OnPluginLoaded")]
        //void OnPluginLoaded(Plugin plugin)
        //{
        //    if (!string.IsNullOrEmpty(Net.sv.ip))
        //    {
        //        NextTick(()=>plugin.CallHook( "OnServerInitialized" ));
        //    }
        //}

        [HookMethod("OnPluginUnloaded")]
        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == Name) return;
            foreach (var playerPair in players)
            {
                for (int i = playerPair.Value.Count - 1; i >= 0; i--)
                {
                    var data = playerPair.Value[i];
                    if (data.plugin == plugin.Name)
                        IDestroyUI(playerPair.Key, data);
                }
            }
        }

        #endregion

        #region UI

        void IDrawUI(BasePlayer player, UIData data, CuiElementContainer additionalContainer)
        {
            DateTime now = DateTime.Now;
            if (!players.ContainsKey(player))
                players.Add(player, new List<UIData>());

            var funcs = players[player];
            for (int i = funcs.Count - 1; i >= 0; i--)
            {
                var func = funcs[i];
                if (func.plugin != data.plugin || func.funcName != data.funcName) continue;
                players[player][i] = data;
                DrawUIWithoutCache(player, func, data, additionalContainer);
                return;
            }
            var json = functions[data.plugin][data.funcName][data.aspect];
            if (data.args.Length > 0)
                json = HandleArgs(json, data.args);
            players[player].Add(data);
            if (additionalContainer.Count > 0)
            {
                var elements = CuiHelper.FromJson(json);
                elements.AddRange(additionalContainer);
                json = CuiHelper.ToJson(elements);
            }
            CacheUI( player.userID, json );
            CuiHelper.AddUi(player, json);
            if (isDebug)
            {
                draw++;
                Puts(json);
            }
        }

        void DrawUIWithoutCache(BasePlayer player, UIData dataOld, UIData dataNew, CuiElementContainer additionalContainer)
        {
            if (dataNew.aspect != dataOld.aspect)
            {
                IDestroyUI(player, dataOld);
                IDrawUI(player, dataNew, additionalContainer);
                return;
            }
            
            var func = functions[dataOld.plugin][dataOld.funcName];

            var changedArgs = new List<int>();
            for (int i = 0; i < func.argc(); i++)
                if (dataOld.args[i].ToString() != dataNew.args[i].ToString()) changedArgs.Add(i);
            if (changedArgs.Count == 0)
            {
                return;
            }
            var destroylist = new List<CuiElement>();
            foreach (int arg in changedArgs)
                destroylist.AddRange(func.cacheArgs[dataOld.aspect][arg]);
            destroylist = destroylist.Distinct(new CuiElementComparer()).ToList();
            for (int i = destroylist.Count - 1; i >= 0; i--)
                CuiHelper.DestroyUi(player, destroylist[i].Name);


            var createlist = new List<CuiElement>();
            foreach (int arg in changedArgs)
                createlist.AddRange(func.cacheArgs[dataNew.aspect][arg]);
            createlist = createlist.Distinct(new CuiElementComparer()).ToList();
            SortHierarchy(createlist);
            createlist.AddRange(additionalContainer);
            var json = CuiHelper.ToJson(createlist);
            if (dataNew.args.Length > 0)
                json = HandleArgs(json, dataNew.args);
            CacheUI( player.userID, json );
            CuiHelper.AddUi(player, json);
            if (isDebug)
            {
                Puts( json );
                drawCache++;
            }
        }

        void GetHierarchy(CuiElement element, List<CuiElement> function, List<CuiElement> hierarchy = null)
        {
            if (hierarchy == null)
                hierarchy = new CuiElementContainer();

            hierarchy.Add(element);

            var elementChilds = function.Where(child => child.Parent == element.Name).ToList();
            if (elementChilds.Count <= 0) return;

            foreach (var child in elementChilds)
                GetHierarchy(child, function, hierarchy);
        }

        int GetDept(CuiElement obj)
        {
            if (obj == null || obj.Parent == "Hud" || obj.Parent == "Overlay") return 0;
            return GetDept(sortContainer.Find(p => p.Name == obj.Parent)) + 1;
        }

        private List<CuiElement> sortContainer;

        public List<CuiElement> SortHierarchy(List<CuiElement> container)
        {
            sortContainer = container;
            return container.OrderBy(GetDept).ToList();
        }

        Rect GetRect(CuiElement e)
        {
            var transform = (CuiRectTransformComponent) e.Components.Find(c => c.Type == "RectTransform");
            var min = ParseVector(transform.AnchorMin);
            var max = ParseVector(transform.AnchorMax);
            return new Rect(min, max - min);
        }

        bool ValueInRange(float value, float min, float max) => (value > min) && (value < max);

        bool Intersect(Rect a, Rect b)
        {
            var xOverlap = ValueInRange(a.x, b.x, b.x + b.width) ||
                           ValueInRange(b.x, a.x, a.x + a.width);

            var yOverlap = ValueInRange(a.y, b.y, b.y + b.height) ||
                           ValueInRange(b.y, a.y, a.y + a.height);

            return xOverlap && yOverlap;
        }

        public static Vector2 ParseVector(string p)
        {
            string[] strArrays = p.Split(new char[] {' '});
            if ((int) strArrays.Length != 2)
            {
                return Vector2.zero;
            }
            return new Vector2(float.Parse(strArrays[0]), float.Parse(strArrays[1]));
        }

        string HandleArgs(string json, object[] args)
        {
            for (int i = 0; i < args.Length; i++)
                json = json.Replace("{" + i + "}", args[i].ToString());
            return json;
        }

        void IDestroyUI(BasePlayer player, UIData data)
        {
            if (player == null)
            {
                PrintError("DestroyUI - player = null");
                return;
            }
            var uid = player.userID;

            var json = functions[data.plugin][data.funcName][data.aspect];
            if (data.args.Length > 0)
                json = HandleArgs(json, data.args).Replace("$", "");
            var container = CuiHelper.FromJson(json);
            container.Reverse();
            container.ForEach(e =>
            {
                if (e.Name != "AddUI CreatedPanel")
                CuiHelper.DestroyUi(player, e.Name);
            });

            players[player].Remove(data);
        }

        void DestroyAllUI(BasePlayer player)
        {
            var data = players[player];
            for (int i = data.Count - 1; i >= 0; i--)
                IDestroyUI(player, data[i]);
        }

        #endregion

        #region ASPECT UI

        void AspectUI(BasePlayer player)
        {
            IDrawUI(player, new UIData("CuiGenerator", "aspect", 0),new CuiElementContainer());
        }

        #endregion

        #region UI COMMANDS

        [ConsoleCommand("cuigenerator.aspect")]
        void cmdAspect(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;
            var aspect = arg.GetInt(0, -1);
            if (aspect == -1) return;
            aspects[player.userID] = aspect;
            IDestroyUI(player, new UIData(Name, "aspect", 0));
            onPlayerAspectChanged(player);
        }

        #endregion

        #region CHAT COMMANDS

        [ChatCommand("aspect")]
        void cmdChatAcpect(BasePlayer player, string command, string[] args)
        {
            AspectUI(player);
        }

        #endregion

        #region EXTERNAL CALLS

        internal void onPlayerAspectChanged(BasePlayer player)
        {
            DestroyAllUI(player);
            Interface.CallHook("OnPlayerAspectChanged", player);
        }

        internal void onCuiGeneratorInitialized()
        {
            isLoaded = true;
            Puts($"{nameof(CuiGenerator)} initialized!");
            Interface.CallHook("OnCuiGeneratorInitialized");
        }

        #endregion

        #region API

        public void DrawUI(BasePlayer player, string plugin, string funcName, params object[] args)
        {
            DrawUIWIthEx(player, plugin, funcName, new CuiElementContainer(), args);
        }

        public void DrawUIWIthEx(BasePlayer player, string plugin, string funcName, CuiElementContainer additionalContainer, params object[] args)
        {
            if (!isLoaded)
            {
                timer.Once(0.1f, () => DrawUI(player, plugin, funcName, args));
                return;
            }
            var data = new UIData(plugin, funcName, GetAspectID(player.userID), args);
            Dictionary<string, CuiFunction> pluginFuncs;
            if (!functions.TryGetValue(plugin, out pluginFuncs))
            {
                PrintError(
                    $"Draw UI:\r {plugin} not found for {player.userID}:{player.displayName} \nDebug: {data}");
                return;
            }
            CuiFunction func;
            if (!functions[plugin].TryGetValue(funcName, out func))
            {
                PrintError(
                    $"Draw UI:\r {plugin} doesn't contains \"{funcName}\" {player.userID}:{player.displayName} \nDebug: {data}");
                return;
            }

            IDrawUI(player, data, additionalContainer);
        }

        public void DestroyUI(BasePlayer player, string plugin, string funcName)
        {
            if (!isLoaded)
            {
                timer.Once(0.1f, () => DestroyUI(player, plugin, funcName));
                return;
            }
            List<UIData> uiList;
            if (!players.TryGetValue(player, out uiList))
            {
                PrintError(
                    $"Destroy UI:\r{player.userID}:{player.displayName} doesn't have Cui\nDebug: Plugin \"{plugin}\" Function \"{funcName}\"");
                return;
            }
            var uiData = players[player];
            var data = uiData.Find(f => f.plugin == plugin && f.funcName == funcName);
            if (data == null)
            {
                //Puts($"Plugin = {plugin.Name} Func = {funcName} Data == NULL");
                return;
            }
            IDestroyUI(player, data);
        }

        #endregion

        #region Functions

        IEnumerator LoadFunctions()
        {
            yield return ImageStorage.Store( imagesDB.ReadObject<Dictionary<string, string>>() ?? new Dictionary<string, string>() );

            var funcs = uiDB.ReadObject<Dictionary<string, CuiFunction>>();
            functions = new Dictionary<string, Dictionary<string, CuiFunction>>();

            var comparer = new CuiElementComparer();
            
            foreach (var funcPair in funcs)
            {
                var func = funcPair.Key;
                string plugin, funcName;
                SplitFunc(func, out plugin, out funcName);

                if (!functions.ContainsKey( plugin ))
                    functions[ plugin ] = new Dictionary<string, CuiFunction>();

                functions[ plugin ].Add( funcName, funcPair.Value );

                var function = functions[ plugin ][ funcName ];
                var argc = function.argc();
                for (int i = 0; i < 4; i++)
                {
                    function.cacheArgs.Add(new List<List<CuiElement>>());
                    var json = function[ i];
                    if (string.IsNullOrEmpty( json )) continue;
                    var elements = CuiHelper.FromJson(json);

                    foreach (var e in elements)
                    {
var component = e.Components.FirstOrDefault( c => c.Type == "UnityEngine.UI.RawImage" );
                        var rawImage = component as CuiRawImageComponent;
                        if (!string.IsNullOrEmpty(rawImage?.Png))
                        {
                            rawImage.Sprite = "assets/content/textures/generic/fulltransparent.tga";

                            if (rawImage.Png.StartsWith("{"))
                            {
                                if (rawImage.Png == "{colon}")
                                {
                                    rawImage.Png = ImageStorage.FetchPng("colon");
                                }
                            }
                            else
                            {
                                var img = ImageStorage.FetchPng(rawImage.Png);
                                if (string.IsNullOrEmpty(img))
                                {
                                    yield return CommunityEntity.ServerInstance.StartCoroutine(ImageStorage.Store(rawImage.Png, rawImage.Png));
                                    if (ImageStorage.FetchPng( rawImage.Png ) == string.Empty)
                                        Puts("NOT LOADED: "+rawImage.Png );
                                    rawImage.Png = ImageStorage.FetchPng(rawImage.Png);
                                    //Puts(e.Name + ": " + rawImage.Png);
                                }
                                else
                                {
                                    rawImage.Png = img;
                                }
                            }
                        }
                        else if (rawImage!= null && string.IsNullOrEmpty(rawImage.Url) && rawImage.Sprite == "Assets/Icons/rust.png")
                        {
                            rawImage.Sprite = "Assets/Content/UI/UI.Background.Tile.psd";
                        }
                    }

                    function[ i ] = json= CuiHelper.ToJson( elements );

                        var jsonArgs = json;
                    if (argc == 0)
                    {
                        function.cacheArgs[i].Add(elements);
                        continue;
                    }
                    for (int j = 0; j < argc; j++)
                        jsonArgs = jsonArgs.Replace("{" + j + "}", "");
                    var elementsArgs = CuiHelper.FromJson(jsonArgs);

                        var changedElements = elements.Except(elementsArgs, comparer).ToList();

                        var argsElements = changedElements.Select(element => CuiHelper.ToJson(new List<CuiElement>() { element })).ToList();
                        List<int> argNumbers = new List<int>();
                        for (int j = 0; j < argc; j++)
                            for (int k = 0; k < argsElements.Count; k++)
                                if (argsElements[k].Contains("{" + j + "}"))
                                    argNumbers.Add(k);
                        for (int j = 0; j < argNumbers.Count; j++)
                        {
                            var e = changedElements[argNumbers[j]];
                            var argsReferences = elements.Where(
                                o => o != e &&
                                     o.Parent == e.Parent && Intersect(GetRect(e), GetRect(o)) &&
                                     !changedElements.Contains(o)).ToList();
                            argsReferences.Insert(0, e);
                            var newReferences = new List<CuiElement>();
                            for (var index = 0; index < argsReferences.Count; index++)
                            {
                                var element = argsReferences[index];
                                newReferences.AddRange(GetChildsRecursive(elements, element.Name));
                            }
                            argsReferences.AddRange(newReferences);
                            argsReferences = argsReferences.Distinct(comparer).ToList();
                            function.cacheArgs[i].Add(argsReferences);
                        }

                    }
                }
            foreach (var pluginFunctions in functions)
                foreach (var func in pluginFunctions.Value)
                {
                    for (int i = 0; i < 4; i++)
                    {

                        var funcAspect = func.Value[i];
                        if (string.IsNullOrEmpty(funcAspect)) continue;
                        var elements = CuiHelper.FromJson(funcAspect);
                        
                    }
                }
            onCuiGeneratorInitialized();
        }

        List<CuiElement> GetChildsRecursive(List<CuiElement> elements, string name)
        {
            List<CuiElement> childs = new List<CuiElement>();
            foreach (var element in elements)
            {
                if (element.Parent == name)
                {
                    childs.Add(element);
                    childs.AddRange(GetChildsRecursive(elements, element.Name));
                }
            }
            return childs;
        }

        void LoadAspects()
        {
            aspects = aspectDB.ReadObject<Dictionary<ulong, int>>() ?? new Dictionary<ulong, int>();
        }

        void SaveAspects()
        {
            aspectDB.WriteObject(aspects);
        }

        void SplitFunc(string func, out string plugin, out string funcName)
        {
            plugin = funcName = string.Empty;
            try
            {
            plugin = func.Substring(0, func.IndexOf('_'));
            funcName = func.Substring(func.IndexOf('_') + 1, func.Length - func.IndexOf('_') - 1);

            }
            catch (Exception e)
            {
                Interface.Oxide.RootLogger.Write(LogType.Error, $"'{func}' dont have separator");
            }
        }

        int GetAspectID(ulong uid) => aspects.ContainsKey(uid) ? aspects[uid] : 0;
        
        public float GetAspect(ulong uid)
        {
            int id = GetAspectID(uid);
            switch (id)
            {
                case 0:
                    return 1.777f;
                case 1:
                    return 1.6f;
                case 2:
                    return 1.25f;
                case 3:
                    return 1.333f;
                default:
                    return 1.777f;
            }
        }

        #endregion
        public class CuiElementComparer : IEqualityComparer<CuiElement>
        {
            bool IEqualityComparer<CuiElement>.Equals(CuiElement x, CuiElement y)
            {
                return CuiElenentEx.EqualElement(x, y);
            }

            int IEqualityComparer<CuiElement>.GetHashCode(CuiElement obj)
            {
                return obj.GetHashCode();
            }
        }
    }

    #region Ex Methods

    public static class CuiElenentEx
    {
        public static bool EqualElement(CuiElement e1, CuiElement e2)
        {
            if (e1.Name != e2.Name) return false;
            if (e1.Parent != e2.Parent) return false;
            if (Math.Abs(e1.FadeOut - e2.FadeOut) > 0.01) return false;
            if (e1.Components.Count != e2.Components.Count) return false;

            return !e1.Components.Where((t, i) => !EqualComponent(t, e2.Components[i])).Any();
        }

        static bool EqualComponent(ICuiComponent e1, ICuiComponent e2)
        {
            if (e1.Type != e2.Type) return false;
            switch (e1.Type)
            {
                case "RectTransform":
                    return EqualComponent((CuiRectTransformComponent)e1, (CuiRectTransformComponent)e2);
                case "UnityEngine.UI.RawImage":
                    return EqualComponent((CuiRawImageComponent)e1, (CuiRawImageComponent)e2);
                case "UnityEngine.UI.Text":
                    return EqualComponent((CuiTextComponent)e1, (CuiTextComponent)e2);
                case "UnityEngine.UI.Image":
                    return EqualComponent((CuiImageComponent)e1, (CuiImageComponent)e2);
                case "UnityEngine.UI.Button":
                    return EqualComponent((CuiButtonComponent)e1, (CuiButtonComponent)e2);
                case "UnityEngine.UI.Outline":
                    return EqualComponent((CuiOutlineComponent)e1, (CuiOutlineComponent)e2);
            }
            return false;
        }

        static bool EqualComponent(CuiRectTransformComponent e1, CuiRectTransformComponent e2)
        {
            if (e1.AnchorMin != e2.AnchorMin) return false;
            if (e1.AnchorMax != e2.AnchorMax) return false;
            if (e1.OffsetMin != e2.OffsetMin) return false;
            return e1.OffsetMax == e2.OffsetMax;
        }

        static bool EqualComponent(CuiTextComponent e1, CuiTextComponent e2)
        {
            if (e1.Align != e2.Align) return false;
            if (e1.Color != e2.Color) return false;
            if (e1.Font != e2.Font) return false;
            if (e1.Text != e2.Text) return false;
            return !(Math.Abs(e1.FadeIn - e2.FadeIn) > 0.01);
        }

        static bool EqualComponent(CuiButtonComponent e1, CuiButtonComponent e2)
        {
            if (e1.Command != e2.Command) return false;
            if (e1.Close != e2.Close) return false;
            if (e1.Color != e2.Color) return false;
            if (e1.Sprite != e2.Sprite) return false;
            if (e1.Material != e2.Material) return false;
            if (e1.ImageType != e2.ImageType) return false;
            return !(Math.Abs(e1.FadeIn - e2.FadeIn) > 0.01);
        }

        static bool EqualComponent(CuiRawImageComponent e1, CuiRawImageComponent e2)
        {
            if (e1.Sprite != e2.Sprite) return false;
            if (e1.Color != e2.Color) return false;
            if (e1.Material != e2.Material) return false;
            if (e1.Png != e2.Png) return false;
            if (e1.Url != e2.Url) return false;
            return !(Math.Abs(e1.FadeIn - e2.FadeIn) > 0.01);
        }

        static bool EqualComponent(CuiImageComponent e1, CuiImageComponent e2)
        {
            if (e1.Sprite != e2.Sprite) return false;
            if (e1.Color != e2.Color) return false;
            if (e1.Png != e2.Png) return false;
            if (e1.Material != e2.Material) return false;
            if (e1.ImageType != e2.ImageType) return false;
            return !(Math.Abs(e1.FadeIn - e2.FadeIn) > 0.01);
        }

        static bool EqualComponent(CuiOutlineComponent e1, CuiOutlineComponent e2)
        {
            if (e1.Color != e2.Color) return false;
            if (e1.Distance != e2.Distance) return false;
            return e1.UseGraphicAlpha == e2.UseGraphicAlpha;
        }
    }

    #endregion
}

