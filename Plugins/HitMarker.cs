// Reference: Oxide.Core.RustyCore

using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Facepunch;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HitMarker", "bazuka5801", "1.0.0")]
    class HitMarker : RustPlugin
    {
        #region CONFIGURATION

        private bool Changed;
        private bool enablesound;
        private string soundeffect;
        private string headshotsoundeffect;
        private float damageTimeout;
        private int historyCapacity;
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        

        protected override void LoadDefaultConfig()
        {
            enablesound = Convert.ToBoolean( GetConfig( "Sound", "EnableSoundEffect", true ) );
            soundeffect =
                Convert.ToString( GetConfig( "Sound", "Sound Effect", "assets/bundled/prefabs/fx/takedamage_hit.prefab" ) );
            headshotsoundeffect =
                Convert.ToString( GetConfig( "Sound", "HeadshotSoundEffect", "assets/bundled/prefabs/fx/headshot.prefab" ) );
            Config.GetVariable( "Через сколько будет пропадать урон", out damageTimeout, 1f );
            Config.GetVariable( "Вместимость истории урона", out historyCapacity, 5 );
            SaveConfig();
        }

        #endregion
        

        #region FIELDS

        [PluginReference] private Plugin Clans;
        
        RCore core = Interface.Oxide.GetLibrary<RCore>();
        List<BasePlayer> hitmarkeron = new List<BasePlayer>();


        Dictionary<BasePlayer, List<KeyValuePair<float, int>>> damageHistory = new Dictionary<BasePlayer, List<KeyValuePair<float, int>>>();

        Dictionary<BasePlayer, Oxide.Plugins.Timer> destTimers = new Dictionary<BasePlayer, Oxide.Plugins.Timer>();
        #endregion

        #region COMMANDS

        [ChatCommand("hitmarker")]
        void cmdHitMarker(BasePlayer player, string cmd, string[] args)
        {
            if (!hitmarkeron.Contains(player))
            {
                hitmarkeron.Add(player);
                SendReply(player,
                    "<color=cyan>HitMarker</color>:" + " " + "<color=orange>You have enabled your hitmarker.</color>");
            }
            else
            {
                hitmarkeron.Remove(player);
                SendReply(player,
                    "<color=cyan>HitMarker</color>:" + " " + "<color=orange>You have disabled your hitmarker.</color>");
            }
        }

        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                hitmarkeron.Add(current);
            }
            CommunityEntity.ServerInstance.StartCoroutine(LoadImages());
            timer.Every(0.1f, OnDamageTimer);
        }

        IEnumerator LoadImages()
        {
            yield return CommunityEntity.ServerInstance.StartCoroutine(core.StoreImages(Images));
        }

        void OnPlayerInit(BasePlayer player)
        {
            hitmarkeron.Add(player);
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            hitmarkeron.Remove(player);
            damageHistory.Remove(player);
        }
        void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            var victim = hitinfo.HitEntity as BasePlayer;
            if (victim && hitmarkeron.Contains(attacker))
            {
                bool isFriend = (Clans?.Call("HasFriend", attacker.userID, victim.userID) as bool?) ?? false;
                if (hitinfo.isHeadshot)
                {
                    if (enablesound == true)
                    {
                        Effect.server.Run(headshotsoundeffect, attacker.transform.position, Vector3.zero,
                            attacker.net.connection);
                    }
                    DestroyLastCui(attacker);
                    core.DrawUI(attacker, "HitMarkerGUI", "menu", Images["hitmarker.hit.head"]);
                    destTimers[attacker] = timer.Once(0.5f, () => core.DestroyUI(attacker, "HitMarkerGUI", "menu"));
                }
                else
                {
                    if (enablesound)
                    {
                        Effect.server.Run(soundeffect, attacker.transform.position, Vector3.zero,
                            attacker.net.connection);
                    }
                    DestroyLastCui(attacker);
                    core.DrawUI(attacker, "HitMarkerGUI", "menu",
                        Images["hitmarker.hit." + (isFriend ? "friend" : "normal")]);
                    destTimers[attacker] = timer.Once(0.5f, () => core.DestroyUI(attacker, "HitMarkerGUI", "menu"));
                }
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var victim = entity as BasePlayer;
            if (victim == null || hitInfo == null) return;
            DamageType type = hitInfo.damageTypes.GetMajorityDamageType();
            if (type == null) return;
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null) return;
            NextTick(() =>
            {
                var damage =
                    System.Convert.ToInt32(Math.Round(hitInfo.damageTypes.Total(), 0, MidpointRounding.AwayFromZero));
                DamageNotifier(attacker, damage);
            });
        }


        void OnPlayerWound( BasePlayer player )
        {
            var attacker = player?.lastAttacker as BasePlayer;
            if (attacker == null) return;

            DestroyLastCui( attacker );
            core.DrawUI( attacker, "HitMarker", "bigicon", Images[ "hitmarker.hit.wound" ] );
            destTimers[ attacker ] = timer.Once( 0.5f, () => core.DestroyUI( attacker, "HitMarker", "bigicon" ) );
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            if (player == null) return;
            var attacker = info?.Initiator as BasePlayer;
            if (attacker == null) return;

            DestroyLastCui(attacker);
            core.DrawUI(attacker, "HitMarker", "bigicon", Images[ "hitmarker.kill"] );
            destTimers[ attacker ] = timer.Once(0.5f, () => core.DestroyUI(attacker, "HitMarker", "bigicon" ) );
        }
        #endregion

        #region Core

        void OnDamageTimer()
        {
            float time = Time.time;
            var toRemove = Pool.GetList<BasePlayer>(); 
            foreach (var dmgHistoryKVP in damageHistory)
            {
                dmgHistoryKVP.Value.RemoveAll(p => p.Key < time);

                DrawDamageNotifier( dmgHistoryKVP.Key );

                if (dmgHistoryKVP.Value.Count == 0)
                    toRemove.Add(dmgHistoryKVP.Key);
            }
            toRemove.ForEach(p=>damageHistory.Remove(p));
            Pool.FreeList(ref toRemove);
        }

        void DamageNotifier(BasePlayer player, int damage)
        {
            List<KeyValuePair<float, int>> damages;
            if (!damageHistory.TryGetValue(player, out damages))
                damageHistory[player] = damages = new List<KeyValuePair<float,int>>();
            damages.Insert(0,new KeyValuePair<float, int>(Time.time+ damageTimeout, damage) );
            if (damages.Count > historyCapacity) damages.RemoveAt(damages.Count - 1);
            DrawDamageNotifier(player);
        }

        string GetDamageArg(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            List<KeyValuePair<float, int>> damages;
            if (!damageHistory.TryGetValue(player, out damages))
                return string.Empty;
            for (var i = 0; i < damages.Count; i++)
            {
                var item = damages[i];
                sb.Append(new string(' ', i * 2) + $"<color=#{GetDamageColor(item.Value)}>-{item.Value}</color>" + Environment.NewLine);
            }
            return sb.ToString();
        }

        void DestroyLastCui(BasePlayer player)
        {
            Oxide.Plugins.Timer tmr;
            if (destTimers.TryGetValue(player, out tmr))
            {
                tmr?.Callback?.Invoke();
                if (tmr != null && !tmr.Destroyed)
                    timer.Destroy(ref tmr);
            }
        }

        private Color minColor = ColorEx.Parse( "1 1 1 1" );
        private Color maxColor = ColorEx.Parse( "1 0 0 1" );
        string GetDamageColor( int damage )
        {
            return ColorToHex(Color.Lerp( minColor, maxColor, (float) damage / 100 ));
        }

        string ColorToHex( Color32 color )
        {
            string hex = color.r.ToString( "X2" ) + color.g.ToString( "X2" ) + color.b.ToString( "X2" );
            return hex;
        }
        #endregion

        #region UI


        void DrawDamageNotifier(BasePlayer player)
        {
            core.DrawUI( player, "HitMarker", "damage", GetDamageArg( player ) );
        }

        Dictionary<string,string> Images = new Dictionary<string, string>()
        {
            { "hitmarker.kill", "http://i.imgur.com/R0NeHWp.png" },
            { "hitmarker.hit.normal", "http://i.imgur.com/CmlQUR0.png" },
            { "hitmarker.hit.head", "http://i.imgur.com/RbXBvH2.png" },
            { "hitmarker.hit.friend", "http://i.imgur.com/5M2rAek.png" },
            { "hitmarker.hit.wound", "http://i.imgur.com/bFCHTxL.png" },
        };

        #endregion
    }
}



