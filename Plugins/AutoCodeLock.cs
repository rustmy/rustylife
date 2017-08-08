// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using RustyCore;
using UnityEngine;
using Random = System.Random;


namespace Oxide.Plugins
{
    [Info("AutoCodeLock", "bazuka5801", "1.0.0")]
    class AutoCodeLock : RustPlugin
    {
        #region CLASSES

        public class CodeLockCfg
        {
            public bool AutoLock = true;
            public Dictionary<string,ulong> Shared = new Dictionary<string,ulong>();
        }

        #endregion

        #region VARIABLES

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        readonly List<string> supportPrefabs = new List<string>()
        {
            "assets/prefabs/building/door.hinged/door.hinged.wood.prefab",
            "assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab",
            "assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab",
            "assets/prefabs/building/wall.frame.cell/wall.frame.cell.gate.prefab",
            "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",

            "assets/prefabs/building/door.hinged/door.hinged.metal.prefab",
            "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab",
            "assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab",
            "assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab",
            "assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab",
            "assets/prefabs/building/floor.ladder.hatch/floor.ladder.hatch.prefab",
            "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"
        };

        PermissionService Permission;

        Dictionary<ulong, CodeLockCfg> playersCfg;
        List<CodeLock> codeLocks = new List<CodeLock>();

        int maxPlayers;
        bool buildCodelock;
        string openPerm;
        #endregion

        #region OXIDE HOOKS

        

        void Loaded()
        {
            Permission = new PermissionService(this);
            LoadPlayersCfg();
            LoadDefaultConfig();
            
            Permission.RegisterPermissions(new List<string>() { permissionVip });
            if (!string.IsNullOrEmpty(openPerm)) global::RustyCore.Utils.PermissionService.RegisterPermissions(this, new List<string>(){ openPerm });
        }

        protected override void LoadDefaultConfig()
        {
            Config["Максимум игроков"] = maxPlayers = GetConfig("Максимум игроков", 2);
            Config["Автоустановка замков"] = buildCodelock = GetConfig("Автоустановка замков", true);
            Config["Пермишен на открытие дверей(хозяин)"] = openPerm = GetConfig("Пермишен на открытие дверей(хозяин)", "");
            SaveConfig();
        }

        void OnServerInitialized()
        {
            codeLocks = BaseEntity.saveList.OfType<CodeLock>().ToList();
        }
        
        void Unload()
        {
            SavePlayersCfg();
        }
        
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (!buildCodelock) return;
            var player = plan.GetOwnerPlayer();
            if (!supportPrefabs.Contains(go.name)) return;
            if (!CanPlaceCodeLock(player.userID, go.name)) return;
            
            if (!TakeCodeLock(player)) return;
            var entity = go.ToBaseEntity();
            if (!entity) return;
            if (!go.name.Contains("box") || Permission.HasPermission(player.userID, permissionVip))
                BuildCodeLock(plan, entity);
        }

        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (!(entity is CodeLock)) return;
            var codelock = (CodeLock) entity;

            SetupCodelock(deployer.GetOwnerPlayer().userID, codelock);
        }

        void SetupCodelock(ulong uid, CodeLock codeLock)
        {
            codeLock.OwnerID = uid;
            codeLocks.Add(codeLock);
            var owner = GetCodeLockOwner(codeLock);
            
            var cfg = GetPlayerCfg(owner);
            if(!cfg.AutoLock) return;

            codeLock.code = RandomString(4);
            codeLock.SetFlag(BaseEntity.Flags.Locked, true);
            codeLock.whitelistPlayers = new List<ulong>() { owner };
        }

        void OnEntityKill(BaseNetworkable ent)
        {
            if (!(ent is CodeLock)) return;
            CodeLock codeLock = (CodeLock)ent;
            codeLocks.Remove(codeLock);
        }

        object CanUseLockedEntity(BasePlayer player, BaseLock @codelock)
        {
            if (!(@codelock is CodeLock)) return null;
            var ownerLock = GetCodeLockOwner((CodeLock)@codelock);
            if (ownerLock == 0) return null;
            if (!string.IsNullOrEmpty(openPerm) && !global::RustyCore.Utils.PermissionService.HasPermission(ownerLock, openPerm))
                return null;
            if (GetPlayerCfg(ownerLock).Shared.ContainsValue(player.userID))
                return true;
            return null;
        }

        #endregion


        #region CORE FUNCTIONS

        bool CanPlaceCodeLock(ulong uid, string prefab)
        {
            var cfg = GetPlayerCfg(uid);
            if (supportPrefabs.IndexOf(prefab) >= 0 && cfg.AutoLock) return true;
            return false;
        }

        void BuildCodeLock(Planner plan, BaseEntity entity)
        {
            if (!entity.HasSlot(BaseEntity.Slot.Lock))
            {
                PrintError( "BuildCodeLock" );
                return;
            }
            var codelock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab");
            codelock.OwnerID = plan.GetOwnerPlayer().userID;
            codelock.gameObject.Identity();
            codelock.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
            codelock.OnDeployed(entity);
            codelock.Spawn();
            entity.SetSlot(BaseEntity.Slot.Lock, (BaseLock) codelock);
            SetupCodelock(codelock.OwnerID,(CodeLock)codelock);
        }

        #endregion

        #region COMMANDS

        [ChatCommand("cl")]
        void cmdChatCodeLock(BasePlayer player, string cmd, string[] args)
        {
            if (player == null) return;
            var cfg = GetPlayerCfg(player.userID);
            var sb = new StringBuilder();
            if (args.Length == 0)
            {
                sb.Append("<size=14><color=#cccccc>");
                sb.Append(
                    "<color=orange><size=20>AutoCodeLock</size></color> <size=16><color=red>1.0</color> <color=orange>by</color></size> <color=red><size=20>BAZUKA5801</size></color>\n");
                sb.Append("<color=orange>/cl help</color>  - помощь по настройке\n");
                sb.Append($"<color=orange>Автоустановка замка {GetTextFromBool(cfg.AutoLock)}</color>\n");
                sb.Append($"<color=orange>Ваши замки могут открывать: <color=yellow>{string.Join(", ",GetPlayerCfg(player.userID).Shared.Keys.ToArray())}</color></color>\n");
                sb.Append("</color></size>");
                player.ChatMessage(sb.ToString());
                return;
            }
            if (args[0] == "help")
            {
                sb.Append("<size=14><color=#cccccc>");
                sb.Append(
                    "<color=orange><size=20>AutoCodeLock</size></color> <size=16><color=red>1.0</color> <color=orange>by</color></size> <color=red><size=20>BAZUKA5801</size></color>\n");
                sb.Append(
                    "<color=orange>/cl auto on/off</color> - включить/выключить автоустановку замка и ввода пароля\n");
                sb.Append("<color=orange>/cl add/remove NICKNAME</color> - разрешить/запретить игроку пользоваться вашими замками\n");
                sb.Append(
                    "<color=orange>Пароль при установке создаётся уникальный, его нельзя ввести в панеле ввода пароля</color>\n");
                sb.Append("</color></size>");
                player.ChatMessage(sb.ToString());
                return;
            }
            if (args.Length == 2)
            {
                var b = GetBoolFromText(args[1]);
                switch (args[0])
                {
                    case "auto":
                        if (b == -1) break;
                        cfg.AutoLock = Convert.ToBoolean(b);
                        player.ChatMessage($"Автоматический ввод пароля: {GetTextFromBool(cfg.AutoLock)}");
                        return;
                }
                ChatSharedHandle(player, cfg.Shared, args[0], args[1],
                    "{0} теперь может пользоваться вашими замками",
                    "{0} теперь не может пользоваться вашими замками");
                return;
            }
            player.ChatMessage("Введена неправильная команда");
        }

        #endregion


        #region FUNCTIONS

        ulong GetCodeLockOwner(CodeLock codelock)
        {
            var authorized = codelock.whitelistPlayers;
            if (authorized.Count > 0)
                return authorized[0];
            if (codelock.OwnerID > 0)
                return codelock.OwnerID;
            return 0;
        }

        CodeLockCfg GetPlayerCfg(ulong uid)
        {
            CodeLockCfg cfg;
            if (playersCfg.TryGetValue(uid, out cfg)) return cfg;
            cfg = new CodeLockCfg();
            playersCfg.Add(uid,cfg);
            return cfg;
        }

        bool TakeCodeLock(BasePlayer player)
        {
            List<Item> items = new List<Item>();
            player.inventory.Take(items, -975723312, 1);
            bool ret = items.Count > 0;
            if (!ret && player.inventory.GetAmount(688032252) >= 100)
            {
                player.inventory.Take(items, 688032252, 100);
                ret = true;
            }
            items.ForEach(i => i.Remove(0.1f));
            items.Clear();
            return ret;
        }

        string GetTextFromBool(bool isOn) => isOn ? "<color=green>вкл</color>" : "<color=red>выкл</color>";
        

        int GetBoolFromText(string text)
        {
            text = text.ToLower();
            if (text == "on") return 1;
            if (text == "off") return 0;
            return -1;
        }

        void ChatSharedHandle(BasePlayer player, Dictionary<string,ulong> shared, string mode, string partNameOrUID, string addMsg, string removeMsg)
        {
            if (mode == "add")
            {
                partNameOrUID = partNameOrUID.ToLower();
                var target = core.FindOnline(partNameOrUID);
                if (target == null)
                {
                    SendReply(player,"Игрок не найден");
                    return;
                }
                if (shared.Count >= maxPlayers)
                {
                    SendReply( player, "Список переполнент!\nПопробуйте удалить кого-нибудь с помощью команды /cl remove НИК" );
                    return;
                }
                if (shared.ContainsValue(target.userID))
                {
                    SendReply( player, "Такой игрок уже есть в списке!" );
                    return;
                }
                shared.Add(target.displayName, target.userID);
                addMsg = string.Format(addMsg, target);
                ChatMessage(player, addMsg);
            }
            else if (mode == "remove")
            {

                partNameOrUID = partNameOrUID.ToLower();
                var removeUser = shared.FirstOrDefault(p => p.Key.Contains(partNameOrUID)).Key;
                if (string.IsNullOrEmpty(removeUser))
                {
                    SendReply(player, "Игрок не найден");
                    return;
                }

                shared.Remove(removeUser);
                ChatMessage(player, removeMsg);
            }
        }

        void ChatMessage(BasePlayer player, string msg) => player.ChatMessage($"<size=14><color=orange>AutoCodeLock: {msg}</color></size>");
        #endregion

        #region DATA

        readonly DynamicConfigFile playersCfgFile = Interface.Oxide.DataFileSystem.GetFile("AutoCodeLock_PlayersCFG");

        void LoadPlayersCfg()
        {
            try
            {
                playersCfg = playersCfgFile.ReadObject<Dictionary<ulong, CodeLockCfg>>();
            }
            catch (Exception)
            {
                playersCfg = new Dictionary<ulong, CodeLockCfg>();
            }
        }

        void SavePlayersCfg() => playersCfgFile.WriteObject(playersCfg);

        #endregion

        #region PERMISSIONS

        private string permissionVip = "vip";
        public class PermissionService
        {
            private static readonly Permission permission = Interface.GetMod().GetLibrary<Permission>();

            private readonly Plugin owner;

            public PermissionService(Plugin owner)
            {
                this.owner = owner;
            }

            public bool HasPermission(ulong uid, string permissionName)
            {
                if(string.IsNullOrEmpty(permissionName))
                    return false;
                permissionName = $"{owner.Name.ToLower()}.{permissionName.ToLower()}";
                return permission.UserHasPermission(uid.ToString(), permissionName);
            }

            public void RegisterPermissions(List<string> permissions)
            {
                if(owner == null) throw new ArgumentNullException("owner");
                if(permissions == null) throw new ArgumentNullException("commands");
                permissions = permissions.Select(p => $"{owner.Name.ToLower()}.{p.ToLower()}").ToList();

                foreach(var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }

        #endregion

        #region CLANS

        [PluginReference]
        Plugin Clans;

        List<ulong> GetTeamMembers(ulong userId)
        {
            var members = Clans?.Call("ApiGetMembers", userId) as List<ulong> ?? new List<ulong>();
            if (members.Count == 0)
                members.Add(userId);
            return members;
        }
        #endregion

        #region RANDOM

        static Random rand = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        string RandomString(int length)=> new string(Enumerable.Repeat(chars, length).Select(s => s[rand.Next(s.Length)]).ToArray());

        #endregion
        
        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));

    }
}
