// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using RustyCore;
using RustyCore.Utils;
using TAA;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingBlocked", "bazuka5801", "1.0.0")]
    class BuildingBlocked : RustPlugin
    {
        private RCore _rCore = Interface.Oxide.GetLibrary<RCore>();

        int terrainMask = LayerMask.GetMask("Terrain");
        int constructionMask = LayerMask.GetMask("Construction");
        int waterLevel;
        int maxHeight;
        bool caveBuild;
        bool roadRestrict;
        bool adminIgnore;
        float treeRadius;
        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Запрещать строительство глубже n метров под водой", out waterLevel,1);
            Config.GetVariable("Запрещать строительство выше n метров", out maxHeight, 25);
            Config.GetVariable("Разрешить строительство в пещерах", out caveBuild, true);
            Config.GetVariable("Запрещать строительство на дорогах", out roadRestrict, true);
            Config.GetVariable("Игнорировать админов", out adminIgnore, true);
            Config.GetVariable("Минимальный радиус от дерева", out treeRadius, 4f);
            SaveConfig();
        }
        readonly FieldInfo whiteListField = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Instance | BindingFlags.NonPublic));

        WaterCollision collision;

        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadDefaultConfig();
            collision = UnityEngine.Object.FindObjectOfType<WaterCollision>();
            _rCore.AddHook(this, nameof(OnEntityBuilt), 1000);
            /*List<string> names = new List<string>();
            foreach (Collider transform in UnityEngine.Object.FindObjectsOfType<Collider>())
            {
                //if (transform.name.Contains("ladder") || transform.name.Contains("volume"))
                        if (transform.isTrigger && transform.gameObject.layer == 18)
                    names.Add(transform.name+" "+transform.gameObject.layer);
            }
            names = names.Distinct().ToList();
            Puts("\n"+string.Join("\n", names.ToArray()));*/
            //timer.Every(1f, () =>
            //{
            //    RaycastHit hit;
            //var player = BasePlayer.FindByID(76561198118495969);//
            //   // var player = BasePlayer.activePlayerList[0];//
            //if (Physics.Raycast(player.eyes.HeadRay(), out hit))
            //    {
            //        var t = hit.transform;
            //        if (hit.GetEntity()?.ShortPrefabName != null)
            //        {
            //            Puts($"ShortName: {hit.GetEntity().ShortPrefabName}");
            //        }//
            //        var comps = t.GetComponents<Component>().Select(c => c.GetType().Name).ToArray();
            //        player.ChatMessage(
            //            $"{t.name} [{t.gameObject.layer}: {LayerMask.LayerToName(t.gameObject.layer)}]: {string.Join(", ", comps)}");
            //        /*var msg = t.root.name + "\nQQQQQQQQQ\n";
            //        if (t.root.childCount > 50)
            //        {
            //            player.ChatMessage("ROOT CHILDS = "+t.root.childCount);
            //            return;
            //        }*/
            //        /*var i = 0;
            //        foreach (Transform tr in t.root)
            //        {
            //            msg += $"{i++}: {tr.name}\n";
            //        }
            //        player.ChatMessage(msg);*/
            //        /*var boxCollider = t.GetComponent<Collider>();
            //        if (boxCollider) { 
            //                player.ChatMessage(boxCollider.name);
            //                player.ChatMessage("Trigger: " + boxCollider.isTrigger);
            //                DrawBox(player, boxCollider.bounds.center, t.rotation, boxCollider.bounds.size);
                        
            //        }*/
            //        var boxCollider = t.GetComponentsInChildren<Collider>();
            //        if (boxCollider.Length > 0)
            //            {
            //                foreach (var b in boxCollider)
            //                {
            //                    player.ChatMessage(b.name);
            //                    player.ChatMessage("Trigger: " + b.isTrigger);
            //                    DrawBox(player, b.bounds.center, t.rotation, b.bounds.size);
            //                }
            //            }
            //        //var door = hit.GetEntity() as Door;
            //        //if (door == null) return;
            //        //var codelock = door.GetSlot(BaseEntity.Slot.Lock) as CodeLock;
            //        //if (codelock == null) return;
            //        //var authorized = ((List<ulong>)whiteListField.GetValue(codelock));
            //        //Puts(string.Join(", ",authorized.Select(s=>$"[{s}]").ToArray()));
            //        //}
            //    }
            //    //Puts(string.Join(", ",UnityEngine.Object.FindObjectsOfType<Canvas>().Select(p=>p.name).ToArray()));
            //});
        }

        static void DrawBox(BasePlayer player, Vector3 center, Quaternion rotation, Vector3 size)
        {
            size /= 2;
            var point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
            var point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
            var point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
            var point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
            var point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
            var point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
            var point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
            var point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point1, point2);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point1, point3);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point1, point5);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point4, point2);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point4, point3);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point4, point8);

            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point5, point6);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point5, point7);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point6, point2);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point8, point6);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point8, point7);
            player.SendConsoleCommand("ddraw.line", 1, Color.blue, point7, point3);
        }

        static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }
        /*bool CanBuild(Planner plan, Construction prefab)
        {
            return false;
        }*/

        private bool HasCupboard(Vector3 pos, float radius, BaseEntity deployed)
        {
            var hits = Physics.OverlapSphere( pos, 25f, Rust.Layers.Server.Deployed );
            List<BuildingPrivlidge> buildingPrivilege = new List<BuildingPrivlidge>();
            foreach (var collider in hits)
            {
                var cBoard = collider.GetComponentInParent<BuildingPrivlidge>();
                if (cBoard == null || cBoard == deployed) continue;
                buildingPrivilege.Add( cBoard );
            }
            return buildingPrivilege.Count > 0;
        }
        private bool CupboardPrivlidge(BasePlayer player, Vector3 position, BaseEntity entity)
        {
            var hits = Physics.OverlapSphere(position, 25f, Rust.Layers.Server.Deployed);
            List<BuildingPrivlidge> buildingPrivilege = new List<BuildingPrivlidge>();
            foreach (var collider in hits)
            {
                var cBoard = collider.GetComponentInParent<BuildingPrivlidge>();
                if (cBoard == null || cBoard == entity) continue;
                buildingPrivilege.Add(cBoard);
            }

            if (buildingPrivilege.Count == 0) return true;


            BuildingPrivlidge cupboard = null;
            for (int i = 0; i < buildingPrivilege.Count; i++)
            {
                BuildingPrivlidge item = buildingPrivilege[i];
                if (item.Dominates(cupboard))
                {
                    cupboard = item;
                }
            }
            
            List<ulong> ids = (from id in cupboard.authorizedPlayers select id.userid).ToList();


            return ids.Any(authUserID => authUserID == player.userID);
        }
        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            if (player.IsAdmin && adminIgnore) return;
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;

            var privilege = player.GetBuildingPrivilege();
            if ((privilege != null && privilege.authorizedPlayers.Count(p => p.userid == player.userID) == 0) || !CupboardPrivlidge(player,entity.transform.position, entity))
            {
                Refund(player, entity);
                player.ChatMessage(Messages["buildingBlocked"]);
                entity.Kill();
                return;
            }
            Vector3 pos = entity.GetNetworkPosition();

            if (entity is BuildingPrivlidge)
            {
                if (HasCupboard(pos, 25f, entity))
                {
                    Refund( player, entity );
                    player.ChatMessage(Messages[ "AlreadyBuildingBuilt" ] );
                    entity.Kill();
                    return;
                }
            }
            if (pos.y < -waterLevel)
                if (caveBuild && InCave(pos)) return;
                else
                {
                    player.ChatMessage(string.Format(Messages["waterLevel"],waterLevel));
                    entity.Kill();
                    return;
                }
            if (TerrainMeta.WaterMap.GetHeight(pos) - pos.y > waterLevel)
            {
                player.ChatMessage(string.Format(Messages["waterLevel"], waterLevel));
                entity.Kill();
                return;
            }
            if (pos.y - TerrainMeta.HeightMap.GetHeight(pos) > maxHeight)
            {
                SendReply(player, string.Format(Messages["heightLevel"], maxHeight));
                entity.Kill();
                return;
            }
            if (roadRestrict)
            {
                RaycastHit hit;
                if (Physics.Raycast(pos + new Vector3(0, 2, 0), Vector3.down, out hit, 10, terrainMask))
                {
                    if (hit.transform.name == "Road Mesh")
                    {
                        SendReply(player, Messages["roadBlock"]);
                        entity.Kill();
                        return;
                    }
                }
            }

            if (treeRadius > 0 && entity.ShortPrefabName.Contains("foundation"))
            {
                List<TreeEntity> treeList = new List<TreeEntity>();
                Vis.Entities(pos, treeRadius,treeList, LayerMask.GetMask("Tree"),QueryTriggerInteraction.Ignore);
                if (treeList.Count > 0)
                {
                    SendReply(player,string.Format(Messages["treeBlock"], (int)treeRadius));
                    entity.Kill();
                    return;
                }
            }
            if (entity.ShortPrefabName.Contains("foundation"))
            {
                List<BuildingBlock> blockList = new List<BuildingBlock>();
                Vis.Entities(pos,4,blockList,constructionMask,QueryTriggerInteraction.Ignore);
                if (
                    blockList.Count(
                        block =>
                            block.ShortPrefabName.Contains("foundation") &&
                            CompareFoundationStacking(block.CenterPoint(), entity.CenterPoint())) > 1)
                {
                    entity.KillMessage();
                    SendReply(player, Messages["StackFoundation"], this);
                }
            }
            //Puts($"{player.userID}/{player.displayName}:{entity.ShortPrefabName}");
        }

        bool CompareFoundationStacking(Vector3 vec1, Vector3 vec2)
        {
            vec1 = vec1.WithY(0);
            vec2 = vec2.WithY(0);
            return vec1.ToString("F4") == vec2.ToString("F4");
        }

        void SendReply(BasePlayer player, string msg)
        {
            base.SendReply(player, $"<size=16><color=#ff5400>{msg}</color></size>");
        }

        bool InCave(Vector3 vec) => collision.GetIgnore(vec);

        [PluginReference] private Plugin Remove;

        void Refund(BasePlayer player, BaseEntity entity, float percent = -1)
        {
            RefundHelper.Refund(player, entity, percent);
        }


        #region MESSAGES

        Dictionary<string,string> Messages = new Dictionary<string, string>()
        {
            { "buildingBlocked", "Строительство в BuildingBlocked запрещено!"},
            { "waterLevel", "Строительство глубже {0} метров под водой запрещено!"},
            { "heightLevel", "Строительство выше {0} метров запрещено!" },
            { "roadBlock", "Строительство на дорогах запрещено!" },
            { "treeBlock", "Строительство рядом с деревьями в радиусе {0}м. запрещено" },
            { "StackFoundation","Стакать фундаменты запрещено!"},
            { "AlreadyBuildingBuilt", "Шкаф уже стоит!" }
        };

        #endregion
    }
}
