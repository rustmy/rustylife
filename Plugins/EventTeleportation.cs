
using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EventTeleportation", "Unknown", 0.1)]
    [Description("Makes epic stuff happen")]
    public class EventTeleportation : RustPlugin
    {
        public Vector2 pivot = Vector2.zero;
        public float radius = 100;
        private readonly int groundLayer = LayerMask.GetMask("Terrain", "World");

        [PluginReference]
        Plugin Kits;

        List<ulong> blockList = new List<ulong>();

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            Puts(pivot.ToString());
        }

        protected override void LoadDefaultConfig()
        {
            string[] point = GetConfig("Pivot", "0 0").Split();
            pivot = new Vector2(float.Parse(point[0]), float.Parse(point[1]));
            radius = GetConfig("Radius", radius);
            SaveConfig();
        }

        private float GetGround(Vector3 sourcePos)
        {
            var oldPos = sourcePos;
            sourcePos.y = TerrainMeta.HeightMap.GetHeight(sourcePos);
            RaycastHit hitinfo;
            if(Physics.SphereCast(oldPos, .1f, Vector3.down, out hitinfo, groundLayer))
                sourcePos.y = hitinfo.point.y;
            return sourcePos.y;
        }

        public Vector3 GetSpawnPosition()
        {
            float angle = UnityEngine.Random.Range(0f, 360f);
            float x = pivot.x + radius * Mathf.Cos(angle);
            float z = pivot.y + radius * Mathf.Sin(angle);
            float y = GetGround(new Vector3(x, 0, z));
            if(y == -1) return Vector3.zero;
            return new Vector3(x, y, z);
        }

        [ChatCommand("tpevent")]
        void TpEvent(BasePlayer player, string command, string[] args)
        {
            if (blockList.Contains(player.userID))
            {
                player.ChatMessage("Умрите, чтобы попасть на ивент");
                return;
            }
            blockList.Add(player.userID);
            player.ChatMessage("Вы телепортируетесь через 10 секунд");
            timer.Once(10, () =>
            {
                var position = GetSpawnPosition();
                if (position == Vector3.zero)
                {
                    player.ChatMessage("Failed teleport!");
                    return;
                }
                Teleport(player, position);
            });
        }

        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if ((entity as BasePlayer) == null) return;
            blockList.Remove(((BasePlayer) entity).userID);
        }

        #region Teleport Helper

        void Teleport(BasePlayer player, Vector3 position)
        {
            if(player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            StartSleeping(player);
            player.MovePosition(position);
            if(player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if(player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if(player.net?.connection == null) return;
            //TODO temporary for potential rust bug
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }

        void StartSleeping(BasePlayer player)
        {
            if(player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if(!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }

        #endregion

        #region Config Helper

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));

        #endregion
    }
}