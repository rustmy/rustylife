// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using System.Text;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("KillFeed", "bazuka5801", "1.0.0")]
    class KillFeed : RustPlugin
    {
        #region CONFIGURATION

        private string colorKiller;
        private string colorVictim;
        private string colorText;
        private string colorBone;
        private string colorWeapon;
        private string colorDistance;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Цвет ника убийцы",out colorKiller, "#ff5400");
            Config.GetVariable("Цвет ника жертвы", out colorVictim, "#ff5400");
            Config.GetVariable("Цвет кости", out colorBone, "#ff5400");
            Config.GetVariable("Цвет дистанции", out colorDistance, "#ff5400");
            Config.GetVariable("Цвет оружия", out colorWeapon, "#ff5400");
            Config.GetVariable("Цвет текста", out colorText, "#ff5400");
            Puts(colorVictim);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        RCore core = Interface.Oxide.GetLibrary<RCore>();


        List<StringBuilder> rows = new List<StringBuilder>();
        HashSet<ulong> cache = new HashSet<ulong>();
        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            SendKillFeed();
            foreach (var VARIABLE in (Dictionary<uint,string>)typeof(StringPool).GetField("toString", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null))
            {
                
            }
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (cache.Contains(player.userID))
                {
                    CuiHelper.DestroyUi( player, "killfeedtext" );
                }
            }
        }
        void OnEntityDeath(BaseCombatEntity ent, HitInfo info)
        {
            if (info == null) return;
            var victim = ent as BasePlayer;
            var killer = info.InitiatorPlayer;
            if (victim == killer) return;
            if (!victim || !killer) return;
            AddKill(killer, victim, info);
        }

        #endregion

        #region CORE

        void AddKill(BasePlayer killer, BasePlayer victim, HitInfo info)
        {
            //if (killer == victim) return;
            rows.Add(new StringBuilder($"<color={colorText}><color={colorKiller}>{killer.displayName.ToLower().RemoveBadSymbols()}</color> убил в <color={colorBone}>{GetHitBone(info)}</color> <color={colorWeapon}>({GetWeapon(info)})</color> <color={colorDistance}>{GetDistance(victim, info)}</color><color={colorVictim}> ☠ {victim.displayName.ToLower().RemoveBadSymbols()}</color></color>{Environment.NewLine}"));

            if (rows.Count > 5)
            {
                rows.RemoveAt(0);
            }
            SendKillFeed();
        }

        void SendKillFeed()
        {
            var tb = new StringBuilder();
            foreach (var row in rows)
            {
                tb.Append(row);
            }
            string text = tb.ToString();
            foreach (var player in BasePlayer.activePlayerList)
            {
                core.DrawUI( player, "KillFeed", "menu", text );
            }
        }

        

        string GetHitBone(HitInfo info)
        {
            string hitBone = "";

            BaseCombatEntity hitEntity = info?.HitEntity as BaseCombatEntity;

            if (hitEntity != null)
            {
                SkeletonProperties.BoneProperty boneProperty = hitEntity.skeletonProperties?.FindBone(info.HitBone);

                string bone = boneProperty?.name?.english ?? "";

                if (!boneNames.TryGetValue(bone, out hitBone))
                {
                    hitBone = bone;
                }
            }
            return hitBone;
        }

        string GetDistance(BaseCombatEntity entity, HitInfo info)
        {
            float distance = 0.0f;

            if (entity != null && info.Initiator != null)
            {
                distance = Vector3.Distance(info.Initiator.transform.position, entity.transform.position);
            }
            return distance.ToString("0.0").Equals("0.0") ? "" : distance.ToString("0.0") + "m";
        }

        string GetWeapon(HitInfo info)
        {
            var selfInflicted = false;
            string weaponID;

            string weapon = info.Weapon?.GetItem()?.info?.shortname ?? "";
            // special case handling if the traditional way of getting a weapons shortname doesn't return results.
            if (string.IsNullOrEmpty( weapon ))
            {
                if (info.WeaponPrefab != null)
                {
                    if (info.WeaponPrefab.ShortPrefabName.Equals( "axe_salvaged.entity" )) weapon = "axe.salvaged";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "bone_club.entity" )) weapon = "bone.club";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "explosive.satchel.deployed" )) weapon = "explosive.satchel";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "explosive.timed.deployed" )) weapon = "explosive.timed";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "flamethrower.entity" )) weapon = "flamethrower";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "flameturret.deployed" ))
                    {
                        weapon = "flameturret";
                        selfInflicted = true;
                    }
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "grenade.beancan.deployed" )) weapon = "grenade.beancan";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "grenade.f1.deployed" )) weapon = "grenade.f1";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "hammer_salvaged.entity" )) weapon = "hammer.salvaged";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "hatchet.entity" )) weapon = "hatchet";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "hatchet_pickaxe.entity" )) weapon = "stone.pickaxe";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "icepick_salvaged.entity" )) weapon = "icepick.salvaged";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "knife_bone.entity" )) weapon = "knife.bone";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "landmine" ))
                    {
                        weapon = "landmine";
                        selfInflicted = true;
                    }
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "longsword.entity" )) weapon = "longsword";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "mace.entity" )) weapon = "mace";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "machete.weapon" )) weapon = "machete";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "pickaxe.entity" )) weapon = "pickaxe";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "rock.entity" )) weapon = "rock";
                    else if (info.WeaponPrefab.ShortPrefabName.Contains( "rocket" )) weapon = "rocket.launcher";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "salvaged_cleaver.entity" )) weapon = "salvaged.cleaver";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "salvaged_sword.entity" )) weapon = "salvaged.sword";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "spear_stone.entity" )) weapon = "spear.stone";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "spear_wooden.entity" )) weapon = "spear.wooden";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "stonehatchet.entity" )) weapon = "stonehatchet";
                    else if (info.WeaponPrefab.ShortPrefabName.Equals( "survey_charge.deployed" )) weapon = "surveycharge";
                }
                else if (info.Initiator != null)
                {
                    if (info.Initiator.ShortPrefabName.Equals( "autoturret_deployed" ))
                    {
                        weapon = "autoturret";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "beartrap" ))
                    {
                        weapon = "trap.bear";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "barricade.metal" ))
                    {
                        weapon = "barricade.metal";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "barricade.wood" ))
                    {
                        weapon = "barricade.wood";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "barricade.woodwire" ))
                    {
                        weapon = "barricade.woodwire";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "gates.external.high.stone" ))
                    {
                        weapon = "gates.external.high.stone";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "gates.external.high.wood" ))
                    {
                        weapon = "gates.external.high.wood";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "guntrap.deployed" ))
                    {
                        weapon = "guntrap";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "lock.code" )) weapon = "lock.code";
                    else if (info.Initiator.ShortPrefabName.Equals( "spikes.floor" ))
                    {
                        weapon = "spikes.floor";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "wall.external.high.stone" ))
                    {
                        weapon = "wall.external.high.stone";
                        selfInflicted = true;
                    }
                    else if (info.Initiator.ShortPrefabName.Equals( "wall.external.high.wood" ))
                    {
                        weapon = "wall.external.high";
                        selfInflicted = true;
                    }
                }
            }

            // mainly used for determining whether the killing or wounding hit was self inflicted or not.
            if (!selfInflicted && ( string.IsNullOrEmpty( weapon ) || !weapon.Equals( "flamethrower" ) ))
            {
                switch (info.damageTypes.GetMajorityDamageType())
                {
                    case DamageType.Bite:
                        if (string.IsNullOrEmpty( weapon )) weapon = "bite";
                        break;

                    case DamageType.Bleeding:
                        if (string.IsNullOrEmpty( weapon )) weapon = "bleeding";
                        selfInflicted = true;
                        break;

                    case DamageType.Blunt:
                        if (string.IsNullOrEmpty( weapon )) weapon = "blunt";
                        break;

                    case DamageType.Bullet:
                        if (string.IsNullOrEmpty( weapon )) weapon = "bullet";
                        break;

                    case DamageType.Cold:
                    case DamageType.ColdExposure:
                        if (string.IsNullOrEmpty( weapon )) weapon = "cold";
                        selfInflicted = true;
                        break;

                    case DamageType.Drowned:
                        if (string.IsNullOrEmpty( weapon )) weapon = "drowned";
                        selfInflicted = true;
                        break;

                    case DamageType.ElectricShock:
                        if (string.IsNullOrEmpty( weapon )) weapon = "electricShock";
                        selfInflicted = true;
                        break;

                    case DamageType.Explosion:
                        if (string.IsNullOrEmpty( weapon )) weapon = "explosion";
                        break;

                    case DamageType.Fall:
                        if (string.IsNullOrEmpty( weapon )) weapon = "fall";
                        selfInflicted = true;
                        break;

                    case DamageType.Generic:
                        if (string.IsNullOrEmpty( weapon )) weapon = "generic";
                        break;

                    case DamageType.Heat:
                        if (string.IsNullOrEmpty( weapon )) weapon = "heat";
                        selfInflicted = true;
                        break;

                    case DamageType.Hunger:
                        if (string.IsNullOrEmpty( weapon )) weapon = "hunger";
                        selfInflicted = true;
                        break;

                    case DamageType.Poison:
                        if (string.IsNullOrEmpty( weapon )) weapon = "poison";
                        selfInflicted = true;
                        break;

                    case DamageType.Radiation:
                    case DamageType.RadiationExposure:
                        if (string.IsNullOrEmpty( weapon )) weapon = "radiation";
                        selfInflicted = true;
                        break;

                    case DamageType.Slash:
                        if (string.IsNullOrEmpty( weapon )) weapon = "slash";
                        break;

                    case DamageType.Stab:
                        if (string.IsNullOrEmpty( weapon )) weapon = "stab";
                        break;

                    case DamageType.Suicide:
                        if (string.IsNullOrEmpty( weapon )) weapon = "suicide";
                        selfInflicted = true;
                        break;

                    case DamageType.Thirst:
                        if (string.IsNullOrEmpty( weapon )) weapon = "thirst";
                        selfInflicted = true;
                        break;
                }
            }
            string weaponloc;

            return localization.TryGetValue(weapon, out weaponloc) ? weaponloc : "Неизв. оружие";
        }

        #endregion

        #region DATA

        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {

        };

        private Dictionary<string, string> localization = new Dictionary<string, string>()
            {
                {"autoturret", "Туррель"},
                {"axe.salvaged", "Самодельный топор"},
                {"barricade.metal", "Металлическая баррикада"},
                {"barricade.wood", "Деревянная барикада"},
                {"barricade.woodwire", "Каменная барикада"},
                {"bone.club", "Костяная дубина"},
                {"bow.hunting", "Охотничий лук"},
                {"crossbow", "Арбалет"},
                {"explosive.satchel", "도시락"},
                {"explosive.timed", "С4"},
                {"flamethrower", "Огнемет"},
                {"gates.external.high.stone", "Каменные ворота"},
                {"gates.external.high.wood", "Деревянные ворота"},
                {"grenade.beancan", "Бобовая граната"},
                {"grenade.f1", "Граната F1"},
                {"hammer.salvaged", "Самодельный молот"},
                {"hatchet", "Топор"},
                {"icepick.salvaged", "Ледоруб"},
                {"knife.bone", "Костяной нож"},
                {"landmine", "Мина"},
                {"lmg.m249", "Пулемет М249"},
                {"lock.code", "Кодовый замок"},
                {"longsword", "Меч"},
                {"mace", "Палица"},
                {"machete", "Мачете"},
                {"pickaxe", "Кирка"},
                {"pistol.eoka", "Самодельный пистолет"},
                {"pistol.m92", "М92"},
                {"pistol.revolver", "Револьвер"},
                {"pistol.semiauto", "Пистолет"},
                {"rifle.ak", "АК - 47"},
                {"rifle.bolt", "Винтовка"},
                {"rifle.lr300", "LR - 300"},
                {"rifle.semiauto", "Берданка"},
                {"rock", "Камень"},
                {"rocket.launcher", "Ракетница"},
                {"salvaged.cleaver", "Тесак"},
                {"salvaged.sword", "Меч"},
                {"shotgun.pump", "Помповый дробовик"},
                {"shotgun.waterpipe", "Самодельный дробовик"},
                {"shotgun.double", "Двустволка"},
                {"smg.2", "СМГ"},
                {"smg.mp5", "MP5"},
                {"smg.thompson", "Томпсон"},
                {"spear.stone", "Копьё"},
                {"spear.wooden", "Копьё"},
                {"spikes.floor", "Шипы"},
                {"stone.pickaxe", "Каменная кирка"},
                {"stonehatchet", "Каменный топор"},
                {"surveycharge", "Геозаряд"},
                {"torch", "Факел"},
                {"trap.bear", "Ловушка"},
                {"wall.external.high", "Стена"},
                {"wall.external.high.stone", "Стена"},
                {"bite", "Укус животного"},
                {"bleeding", "Кровотечение"},
                {"blunt", "Оглушение"},
                {"bullet", "Пуля - Дура"},
                {"cold", "Холод"},
                {"drowned", "Утонул"},
                {"electricShock", "Замок"},
                {"explosion", "Взрывчатка"},
                {"fall", "Воткнулся головой в землю"},
                {"generic", "Смерть забрала с собой"},
                {"heat", "Сгорел"},
                {"hunger", "Голод"},
                {"poison", "Яд"},
                {"radiation", "Радиация"},
                {"slash", ""},
                {"stab", ""},
                {"suicide", "Суицид"},
                {"thirst", "Жажда"}
            };
        
        private Dictionary<string, string> boneNames = new Dictionary<string, string>()
        {
            {"body", "тело"},
            {"chest", "грудь"},
            {"groin", "пах"},
            {"head", "голову"},
            {"hip", "таз"},
            {"jaw", "челюсть"},
            {"left arm", "в руку"},
            {"left eye", "в глаз"},
            {"left foot", "в ногу"},
            {"left forearm", "в предплечье"},
            {"left hand", "в руку"},
            {"left knee", "в колено"},
            {"left ring finge", "в палец"},
            {"left shoulder", "в плечо"},
            {"left thumb", "в палец"},
            {"left toe", "в палец"},
            {"left wrist", "в запястье"},
            {"lower spine", "в хребет"},
            {"neck", "шею"},
            {"pelvis", "таз"},
            {"right arm", "в руку"},
            {"right eye", "в глаз"},
            {"right foot", "в ступню"},
            {"right forearm", "в предплечье"},
            {"right hand", "в руку"},
            {"right knee", "в колено"},
            {"right ring finge", "в палец"},
            {"right shoulder", "в плечо"},
            {"right thumb", "в палец"},
            {"right toe", "в палец"},
            {"right wrist", "в запястье"},
            {"stomach", "живот"}
        };

        #endregion
    }
}
