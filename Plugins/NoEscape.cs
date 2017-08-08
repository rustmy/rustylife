// Reference: Oxide.Core.RustyCore

using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoEscape", "bazuka5801", "1.0.0")]
    class NoEscape : RustPlugin
    {
        #region CLASSES

        class Raid
        {
            public HashSet<ulong> owners;
            public HashSet<ulong> raiders;
            public Vector3 pos;

            public Raid(List<ulong> owners, List<ulong> raiders)
            {
                this.owners = new HashSet<ulong>(owners);
                this.raiders = new HashSet<ulong>(raiders);
            }
        }

        class DamageTimer
        {
            public List<ulong> owners;
            public int seconds;

            public DamageTimer(List<ulong> owners, int seconds)
            {
                this.owners = owners;
                this.seconds = seconds;
            }
        }

        #endregion

        #region CONFIGURATION

        float radius;
        int blockTime;
        int ownerBlockTime;
        bool clansSupport;
        bool useDamageScale;
        bool useVK;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Радиус блокировки", out radius, 50f);
            Config.GetVariable("Время блокировки", out blockTime, 10);
            Config.GetVariable("Поддержка кланов", out clansSupport, true);
            Config.GetVariable("Время блокировки хозяина", out ownerBlockTime, 20);
            Config.GetVariable("Скейлинг дамага если хозяина нет в сети (damage = damage*SCALE)", out offlineScale, 0.5f);
            Config.GetVariable("Использовать скейлинг дамага", out useDamageScale, false);
            Config.GetVariable("Использовать оповещения с помощью плагина VK", out useVK, true);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        [PluginReference] Plugin GUIAnnouncements;

        [PluginReference] Plugin Clans;

        RCore core = Interface.Oxide.GetLibrary<RCore>();

        Dictionary<ulong, double> timers = new Dictionary<ulong, double>();
        List<Raid> raids = new List<Raid>();
        List<DamageTimer> damageTimers = new List<DamageTimer>();
        float offlineScale;

        private string PERM_IGNORE = "noescape.ignore";
        private string PERM_VK_NOTIFICATION = "noescape.vknotification";

        #endregion

        #region OXIDE HOOKS


        void OnServerInitialized()
        {
            PermissionService.RegisterPermissions(this, new List<string>() {PERM_IGNORE, PERM_VK_NOTIFICATION});
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            timer.Every(1f, NoEscapeTimerHandle);
            timer.Every(10f, RaidZoneTimerHandle);
            if (!useDamageScale)
            {
                Unsubscribe(nameof(OnEntityTakeDamage));
            }
            if (Map != null)
                InitImages();
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BuildingBlock) && !(entity is Door) && !entity.ShortPrefabName.Contains("external.high.")) return;

            var player = info?.InitiatorPlayer;
            if (player == null) return;

            if (PermissionService.HasPermission(player.userID, PERM_IGNORE)) return;

            if (player.userID == entity.OwnerID) return;

            var block = entity as BuildingBlock;
            if (block && block.grade <= 0) return;

            if (clansSupport && Clans.Call<bool>("HasFriend", player.userID, entity.OwnerID)) return;
            GetAroundPlayers(entity.GetNetworkPosition()).ForEach(p => BlockPlayer(p));

            var raidteam = (List<BasePlayer>) Clans.Call("ApiGetOnlineTeam", player);
            raidteam.ForEach(p => BlockPlayer(p));

            bool justCreated;
            var raid = GetRaid(entity.GetNetworkPosition(), out justCreated);
            raid.raiders.Add(player.userID);
            if (Clans.Call("GetClanOf", entity.OwnerID) != null)
            {
                bool sendRemoveOwnerMessage = false;
                var team = (List<ulong>) Clans.Call("ApiGetMembers", entity.OwnerID);
                foreach (var uid in team.ToList())
                {
                    if (!raid.owners.Contains(uid))
                    {
                        var p = BasePlayer.FindByID(uid);
                        if (p)
                            GUIAnnouncements?.Call("CreateMsgGUI", Messages["yourbuildingdestroy"], "0.1 0.1 0.1 0.7",
                                "1 1 1", p);
                        raid.owners.Add(uid);
                        if (useDamageScale)
                        {
                            if (justCreated && p && !sendRemoveOwnerMessage)
                            {
                                sendRemoveOwnerMessage = true;
                                damageTimers.Add(new DamageTimer(raid.owners.ToList(), 3600));
                                SendReply(player, Messages["DamageOnlineOwner"]);
                            }
                            else if ((justCreated && !p && !sendRemoveOwnerMessage) ||
                                     (!sendRemoveOwnerMessage && team.Last() == uid))
                            {
                                sendRemoveOwnerMessage = true;
                                SendReply(player, Messages["DamageNotOnlineOwner"]);
                            }
                        }
                    }
                }
            }
            else
            {
                if (!raid.owners.Contains(entity.OwnerID))
                {
                    var p = BasePlayer.FindByID(entity.OwnerID);
                    if (p != null)
                        GUIAnnouncements?.Call("CreateMsgGUI", Messages["yourbuildingdestroy"], "0.1 0.1 0.1 0.7",
                            "1 1 1", p);
                    raid.owners.Add(entity.OwnerID);
                    if (useDamageScale)
                    {
                        if (justCreated && p)
                        {
                            damageTimers.Add(new DamageTimer(raid.owners.ToList(), 3600));
                            SendReply(player, Messages["removeowner"]);
                        }
                    }
                }
            }
            SendVKNotification(raid, player);
            foreach (var owner in GetOwnersByOwner(entity.OwnerID, entity.GetNetworkPosition()))
                raid.owners.Add(owner);
        }



        Raid GetRaid(Vector3 pos, out bool justCreated)
        {
            justCreated = false;
            foreach (var raid in raids)
                if (Vector3.Distance(raid.pos, pos) < 50) return raid;
            justCreated = true;
            var ownerraid = new Raid(new List<ulong>(), new List<ulong>()) {pos = pos};
            raids.Add(ownerraid);
            return ownerraid;
        }

        void BlockPlayer(BasePlayer player, bool owner = false)
        {
            if (player.IsSleeping()) return;
            if (!timers.ContainsKey(player.userID))
                player.ChatMessage(string.Format(Messages[owner ? "ownerhome" : "blockactive"],
                    core.TimeToString(owner ? ownerBlockTime : blockTime)));
            if (!owner || !timers.ContainsKey(player.userID))
            {
                var secs = owner ? ownerBlockTime : blockTime;
                timers[player.userID] = secs;
                CooldownRaid(player, secs);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (useDamageScale)
            {
                foreach (var raid in raids)
                    if (raid.owners.Contains(player.userID) &&
                        raid.owners.Count(p => BasePlayer.FindByID(p) != null) <= 1 &&
                        raid.owners.Any(o => damageTimers.All(t => !t.owners.Contains(o))))
                    {
                        foreach (var raider in raid.raiders)
                            BasePlayer.FindByID(raider)?.ChatMessage(Messages["OwnerEnterOnline"]);
                        break;
                    }
            }
        }

        /*void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            foreach (var raid in raids)
                if (raid.owners.Contains(player.userID) && raid.owners.Count(p => BasePlayer.FindByID(p) != null) <= 1 && raid.raiders.Any(o => removeTimers.Any(t => t.owners.Contains(o))))
                {
                    foreach (var raider in raid.raiders)
                        BasePlayer.FindByID(raider)?.ChatMessage(Messages["OwnerLeaveGame"]);
                    break;
                }
        }*/

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BuildingBlock) && !(entity is Door)) return;

            var initiator = info?.InitiatorPlayer;
            if (initiator == null) return;
            if (initiator.userID == entity.OwnerID) return;
            if (damageTimers.Any(p => p.owners.Contains(entity.OwnerID))) return;
            if (Clans.Call("GetClanOf", entity.OwnerID) != null)
            {
                var team = (List<ulong>) Clans.Call("ApiGetMembers", entity.OwnerID);
                if (team.Contains(initiator.userID))
                    return;
                if (!team.Select(BasePlayer.FindByID).Any(p => p))
                {
                    info.damageTypes.ScaleAll(offlineScale);
                    return;
                }
            }
            else
            {
                if (!BasePlayer.FindByID(entity.OwnerID))
                    info.damageTypes.ScaleAll(offlineScale);
            }
        }

        object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            return ApiGetTime(player.userID) > 0 ? (object) false : null;
        }

        object CanUpgrade(BasePlayer player)
        {
            var seconds = ApiGetTime(player.userID);
            return seconds > 0 ? string.Format(Messages["blockupgrade"], seconds) : null;
        }

        object CanTeleport(BasePlayer player)
        {
            var seconds = ApiGetTime(player.userID);
            return seconds > 0 ? string.Format(Messages["blocktp"], seconds) : null;
        }

        object CanTrade(BasePlayer player)
        {
            var seconds = ApiGetTime(player.userID);
            return seconds > 0 ? string.Format(Messages["blocktrade"], seconds) : null;
        }

        object CanRemove(BasePlayer player, BaseEntity entity)
        {
            var seconds = ApiGetTime(player.userID);
            if (seconds > 0)
                return string.Format(Messages["raidremove"], seconds);

            /*if (removeTimers.Any(rm => rm.owners.Contains(entity.OwnerID)))
            {
                return null;
            }

            if (GetOwnersByOwner(player.userID, entity.GetNetworkPosition()).Select(BasePlayer.FindByID).Any(p => p))
                return null;

            if (Clans.Call("GetClanOf", entity.OwnerID) != null)
            {
                var team = (List<ulong>)Clans.Call("ApiGetMembers", entity.OwnerID);
                if (team.Select(BasePlayer.FindByID).Any(p => p))
                {
                    return null;
                }
            }
            else
            {
                if (BasePlayer.FindByID(entity.OwnerID))
                    return null;
            }*/
            return null; //Messages["ownernotonline"];
        }

        #endregion


        #region CORE

        void NoEscapeTimerHandle()
        {
            foreach (var uid in timers.Keys.ToList())
            {
                if (--timers[uid] <= 0)
                {
                    bool cont = false;
                    foreach (var raid in raids)
                        if (raid.owners.Contains(uid))
                            cont = true;
                    if (cont) continue;
                    timers.Remove(uid);
                    BasePlayer.activePlayerList.Find(p => p.userID == uid)?.ChatMessage(Messages["blocksuccess"]);
                }
            }
            for (int i = damageTimers.Count - 1; i >= 0; i--)
            {
                var rem = damageTimers[i];
                if (--rem.seconds <= 0)
                {
                    damageTimers.RemoveAt(i);
                    continue;
                }
            }
        }

        void RaidZoneTimerHandle()
        {
            List<Raid> toRemove = new List<Raid>();
            foreach (var raid in raids)
            {
                foreach (var player in GetAroundPlayers(raid.pos))
                {
                    if (raid.owners.Contains(player.userID))
                    {
                        BlockPlayer(player, true);
                    }
                }
                raid.raiders.RemoveWhere(raider => !timers.ContainsKey(raider));
                if (raid.raiders.Count <= 0)
                {
                    foreach (var owner in raid.owners)
                    {
                        timers[owner] = ownerBlockTime;
                        var p = BasePlayer.FindByID(owner);
                        if (p) CooldownRaid(p, ownerBlockTime);
                    }
                    toRemove.Add(raid);
                }
            }
            toRemove.ForEach(raid => raids.Remove(raid));
        }

        List<BasePlayer> GetAroundPlayers(Vector3 position)
        {
            var coliders = new List<BaseEntity>();
            Vis.Entities(position, radius, coliders, Rust.Layers.Server.Players);
            return coliders.OfType<BasePlayer>().ToList();
        }

        List<ulong> GetOwnersByOwner(ulong owner, Vector3 position)
        {
            var coliders = new List<BaseEntity>();
            Vis.Entities(position, radius, coliders, Rust.Layers.Server.Deployed);
            var codelocks =
                coliders.OfType<BoxStorage>()
                    .Select(s => s.GetSlot(BaseEntity.Slot.Lock))
                    .OfType<CodeLock>()
                    .ToList();
            var owners = new HashSet<ulong>();
            foreach (var codelock in codelocks)
            {
                var whitelist = codelock.whitelistPlayers;
                if (whitelist == null) continue;
                if (!whitelist.Contains(owner)) continue;
                foreach (var uid in whitelist)
                    if (uid != owner)
                        owners.Add(uid);
            }
            return owners.ToList();
        }

        #endregion

        #region COOLDOWN SYSTEM

        [PluginReference] private Plugin CooldownSystem;

        void CooldownRaid(BasePlayer player, int seconds)
        {
            CooldownSystem?.Call("SetCooldown", player, "raid", seconds);
        }

        #endregion

        #region VK

        [PluginReference] private Plugin Map;

        [PluginReference] private Plugin VK;

        private Image mapImage;
        private Image raidhomeImage;

        void OnMapInitialized()
        {
            InitImages();
        }

        void InitImages()
        {
            try
            {
                uint mapCRC = uint.Parse((string)Map?.Call("MapPng"));
                uint raidhomeCRC = uint.Parse((string)Map?.Call("RaidHomePng"));
                mapImage = (Bitmap)(new ImageConverter().ConvertFrom(FileStorage.server.Get(mapCRC, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID)));
                raidhomeImage = (Bitmap)(new ImageConverter().ConvertFrom(FileStorage.server.Get(raidhomeCRC, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID)));
            }
            catch {}
        }

        void SendVKNotification(Raid raid, BasePlayer raider)
        {
            var vkSubs = raid.owners.Where(p => PermissionService.HasPermission(p, PERM_VK_NOTIFICATION)).ToList();
            if (vkSubs.Count == 0) return;
            var raidhomeViewportPos = (Vector2)Map.Call("ToScreenCoords", raid.pos);
            var raidhomeSize = new Vector2i(raidhomeImage.Width, raidhomeImage.Height);
            var raidhomePos = new Vector2i((int)(raidhomeViewportPos.x*mapImage.Width), mapImage.Height-(int)(raidhomeViewportPos.y*mapImage.Height));
            var raidhomeMin = raidhomePos - raidhomeSize/2;
            foreach (var owner in vkSubs)
            {
                VK.Call("HasUserAuthVK", owner, (Action<bool>)(isAuth =>
                {
                    if (!isAuth) return;
                    Bitmap cutPiece = new Bitmap(mapImage.Width, mapImage.Height);
                    System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(cutPiece);
                    graphic.DrawImage(mapImage, new Rectangle(0, 0, mapImage.Width, mapImage.Height), 0, 0, mapImage.Width, mapImage.Height, GraphicsUnit.Pixel);
                    graphic.DrawImage(raidhomeImage, new Rectangle(raidhomeMin.x, raidhomeMin.y, raidhomeSize.x, raidhomeSize.y), 0, 0, raidhomeImage.Width, raidhomeImage.Height, GraphicsUnit.Pixel);
                    graphic.Dispose();

                    MemoryStream ms = new MemoryStream();
                    cutPiece.Save(ms, ImageFormat.Png);
                    VK.Call("SendMessage", owner, string.Format(Messages["VKNotification"], raider.displayName),
                        ms.ToArray().ToList());

                }));
            }
        }

        #endregion

        #region PLUGIN API

        double ApiGetTime(ulong player)
        {
            double time;
            return timers.TryGetValue(player, out time) ? time : 0;
        }

        List<Vector3> ApiGetOwnerRaidZones(ulong uid)
        {
            return  new List<Vector3>(raids.Where(p => p.owners.Contains(uid)).Select(r => r.pos));
        }

        #endregion

        #region DATA

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"blocksuccess", "<color=#ffcc00><size=16>Рейдблок прошёл!</size></color>"},
            {"blockactive", "<color=#ffcc00><size=16>Строение разрушено, активирован рейдблок на {0}!</size></color>"},
            {"blocktp", "<color=#ffcc00><size=16>Вы не можете телепортироватся во бремя блокировки.\nОсталось {0}</size></color>" },
            { "blockupgrade", "Авто-улучшение построек во время рейд блока запрещено.\n Осталось {0}" },
            { "yourbuildingdestroy", "Вас рейдят! Добавлена отметка на карту" },
            { "removerestrict", "Хозяев нет в сети! Ремув запрещён!" },
            { "ownerhome", "Вы рядом со своим домом, который рейдят!\nРейдблок активирован на {0}!" },
            {"raidremove", "Ремув во время рейда запрещён!\nОсталось {0}" },
            {"ownernotonline", "Владельцов нет в сети, ремув недоступен!" },
            { "DamageOnlineOwner", "Один их хозяев постройки сейчас в сети.\nУрон по объектам владельца стандартный" },
            { "DamageNotOnlineOwner", "Не одного владельца постройки нет в сети. \nУрон по объектам владельца уменьшен в 2 раза!" },
            { "OwnerEnterOnline", "Хозяин постройки зашел в игру.\nУрон по объектам владельца стандартный!" },
            { "blocktrade", "Во время рейда трейдиться запрещено!" },
            { "VKNotification", "Внимание! Вас рейдит игрок {0}" }
        };

        #endregion
    }
}
