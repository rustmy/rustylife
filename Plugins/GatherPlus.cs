// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries;
using Oxide.Core;
using RustyCore;
using RustyCore.Utils;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GatherPlus", "bazuka5801", "1.0.0")]
    class GatherPlus : RustPlugin
    {


        #region CONFIGURATION

        Dictionary<string, Dictionary<string, float>> privileges;

        const string DISPENSER_GATHER = "Рейт добываемых ресурсов";
        const string COLLECTIBLE_PICKUP = "Рейт поднимаемых ресурсов";
        const string QUARRY_GATHER = "Рейт добываемых ресурсов в карьере";

        protected override void LoadDefaultConfig()
        {
            Config["Привилегии"] = privileges = GetConfig("Привилегии", new Dictionary<string, object>()
                {
                    {
                        "gather.vip", new Dictionary<string, object>()
                        {
                            {DISPENSER_GATHER, 1f},
                            {COLLECTIBLE_PICKUP, 1f},
                            {QUARRY_GATHER, 1f}
                        }
                    }
                })
                .ToDictionary(p => p.Key,
                    p =>
                        ((Dictionary<string, object>) p.Value).ToDictionary(k => k.Key,
                            k => (float) Convert.ChangeType(k.Value, typeof(float))));

            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue)
            => Config[name] == null ? defaultValue : (T) Convert.ChangeType(Config[name], typeof(T));

        #endregion

        #region VARIABLES

        private RCore core = Interface.Oxide.GetLibrary<RCore>();

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            PermissionService.RegisterPermissions(this, privileges.Keys.ToList());
            core.AddHook(this, nameof(OnDispenserGather), 1000);
            if (!permission.GroupHasPermission("default", "gatherplus.default"))
                permission.GrantGroupPermission("default", "gatherplus.default", this);
        }

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenser == null) return;
            if (entity == null) return;
            BasePlayer player = entity as BasePlayer;
            if (player == null) return;
            DoGather(player.userID, DISPENSER_GATHER, item);
        }

        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            DoGather(player.userID, COLLECTIBLE_PICKUP, item);
        }

        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            var uid = quarry.OwnerID;
            if (!uid.IsSteamId()) return;
            DoGather(uid, QUARRY_GATHER, item);
        }

        void OnAssignFinishBonus(BasePlayer player, Item item)
        {
            var uid = player.userID;
            if (!uid.IsSteamId()) return;
            DoGather( uid, DISPENSER_GATHER, item );
        }

        private void OnSurveyGather(SurveyCharge surveyCharge, Item item)
        {
            item.amount = (int) (item.amount * privileges["gatherplus.defualt"][QUARRY_GATHER]);
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is OreResourceEntity)
            {
                if (UnityEngine.Random.Range(0, 5) == 3)
                {
                    entity.Kill();
                }
            }
        }

        #endregion

            #region CORE

            void DoGather(ulong uid, string type, Item item)
        {
            if (item.info.shortname == "cloth")
                item.amount = item.amount > 1 ? (int)(item.amount / 1.5f) : 1;
            float multiplier = GetMultiplier(uid, type);
            item.amount = (int) (item.amount * multiplier);
        }

        float GetMultiplier(ulong uid, string type)
        {
            float multiplier = 1;
            foreach (var privilege in privileges)
                if (PermissionService.HasPermission(uid, privilege.Key))
                    multiplier = Mathf.Max(multiplier, privilege.Value[type]);
            return multiplier;
        }

        #endregion

    }
}
