using Rust;
using System;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{

    [Info("DamageDisplayGUI", "cogu", "1.6")]
    [Description("Displays the given damage to a player in a GUI")]
    class DamageDisplay : RustPlugin
    {
        /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        ///////////////////////////////////////				Configs			\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

        HashSet<ulong> users = new HashSet<ulong>();
        System.Collections.Generic.List<ulong> DisabledFor = new System.Collections.Generic.List<ulong>();
        public float DisplayAttackerNameRange => Config.Get<float>("DisplayAttackerNameRange");
        public float DisplayVictimNameRange => Config.Get<float>("DisplayVictimNameRange");
        public bool DisplayDistance => Config.Get<bool>("DisplayDistance");
        public bool DisplayBodyPart => Config.Get<bool>("DisplayBodyPart");
        public bool DamageForAttacker => Config.Get<bool>("DamageForAttacker");
        public bool DamageForVictim => Config.Get<bool>("DamageForVictim");
        public float X_MinVictim => Config.Get<float>("X_MinVictim");
        public float X_MaxVictim => Config.Get<float>("X_MaxVictim");
        public float Y_MinVictim => Config.Get<float>("Y_MinVictim");
        public float Y_MaxVictim => Config.Get<float>("Y_MaxVictim");
        public float X_MinAttacker => Config.Get<float>("X_MinAttacker");
        public float X_MaxAttacker => Config.Get<float>("X_MaxAttacker");
        public float Y_MinAttacker => Config.Get<float>("Y_MinAttacker");
        public float Y_MaxAttacker => Config.Get<float>("Y_MaxAttacker");
        public float DisplayTime => Config.Get<float>("DisplayTime");
        void Unload() => SaveData();
        void OnServerSave() => SaveData();

        private Dictionary<string, string> boneParts = new Dictionary<string, string>()
        {
            {"body", "в тело"},
            {"chest", "в грудь"},
            {"groin", "в пах"},
            {"head", "в голову"},
            {"hip", "в таз"},
            {"jaw", "в челюсть"},
            {"left arm", "в руку"},
            {"left eye", "в глаз"},
            {"left foot", "в ногу"},
            {"left forearm", "в предплечье"},
            {"left hand", "в руку"},
            {"left knee", "в колено"},
            {"left ring finger", "в палец"},
            {"left shoulder", "в плечо"},
            {"left thumb", "в палец"},
            {"left toe", "в палец"},
            {"left wrist", "в запястье"},
            {"lower spine", "в хребет"},
            {"neck", "в шею"},
            {"pelvis", "в таз"},
            {"right arm", "в руку"},
            {"right eye", "в глаз"},
            {"right foot", "в ступню"},
            {"right forearm", "в предплечье"},
            {"right hand", "в руку"},
            {"right knee", "в колено"},
            {"right ring finger", "в палец"},
            {"right shoulder", "в плечо"},
            {"right thumb", "в палец"},
            {"right toe", "в палец"},
            {"right wrist", "в запястье"},
            {"stomach", "в живот"}
        };

        protected override void LoadDefaultConfig()
        {
            Config["DisplayAttackerNameRange"] = 50;
            Config["DisplayVictimNameRange"] = 50;
            Config["DisplayDistance"] = true;
            Config["DisplayBodyPart"] = true;
            Config["DamageForVictim"] = true;
            Config["DamageForAttacker"] = true;
            Config["X_MinVictim"] = 0.355;
            Config["X_MaxVictim"] = 0.475;
            Config["Y_MinVictim"] = 0.91;
            Config["Y_MaxVictim"] = 0.99;
            Config["X_MinAttacker"] = 0.555;
            Config["X_MaxAttacker"] = 0.675;
            Config["Y_MinAttacker"] = 0.91;
            Config["Y_MaxAttacker"] = 0.99;
            Config["DisplayTime"] = 2.5f;
            SaveConfig();
        }

        /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

        private void OnEntityTakeDamage(BaseCombatEntity victim, HitInfo hitInfo)
        {
            if (victim == null || hitInfo == null) return;
            DamageType type = hitInfo.damageTypes.GetMajorityDamageType();
            if (type == null) return;

            if (hitInfo?.Initiator != null && hitInfo?.Initiator?.ToPlayer() != null && users.Contains(hitInfo.Initiator.ToPlayer().userID) && victim.ToPlayer() != null)
            {
                /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
                ///////////////////////////////////////				Configs			\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
                /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

                string vName = "";
                string aName = "";
                string bodypart = "";
                string distance = "";
                float displaytime = DisplayTime;
                //double damage = System.Convert.ToDouble(Math.Round(hitInfo.damageTypes.Total(), 0, MidpointRounding.AwayFromZero));
                float xminvictim = X_MinVictim;
                float xmaxvictim = X_MaxVictim;
                float yminvictim = Y_MinVictim;
                float ymaxvictim = Y_MaxVictim;
                float xminattacker = X_MinAttacker;
                float xmaxattacker = X_MaxAttacker;
                float yminattacker = Y_MinAttacker;
                float ymaxattacker = Y_MaxAttacker;
                float distanceBetween = Vector3.Distance(victim.transform.position, hitInfo.Initiator.ToPlayer().transform.position);
                float displayattackerrange = DisplayAttackerNameRange;
                float displayvictimrange = DisplayVictimNameRange;
                /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

                /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
                ///////////////////////////////////////				Handling		\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
                /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

                NextTick(() =>
                {
                    double damage = System.Convert.ToDouble(Math.Round(hitInfo.damageTypes.Total(), 0, MidpointRounding.AwayFromZero));
                    if (DisplayAttackerNameRange == -1)
                    {
                        displayattackerrange = 65535;
                    }
                    if (DisplayVictimNameRange == -1)
                    {
                        displayvictimrange = 65535;
                    }
                    if (DisplayBodyPart && hitInfo?.Initiator?.ToPlayer() != victim.ToPlayer())
                    {
                        var bone = GetBoneName(victim, ((uint)hitInfo?.HitBone));
                        if (boneParts.ContainsKey(bone))
                            bodypart = boneParts[bone];
                        else
                        {
                            PrintError($"DAMAGE DISPLAY: bond[{bone}] not found in dictionary");
                        }
                    }
                    if (hitInfo?.Initiator?.ToPlayer() != victim.ToPlayer() && distanceBetween <= displayattackerrange)
                    {
                        aName = RemoveSpecialCharacters(hitInfo?.Initiator?.ToPlayer().displayName);
                    }
                    if (hitInfo?.Initiator?.ToPlayer() != victim.ToPlayer() && distanceBetween <= displayvictimrange)
                    {
                        vName = RemoveSpecialCharacters(victim.ToPlayer().displayName);
                    }
                    if (DisplayDistance && hitInfo?.Initiator?.ToPlayer() != victim.ToPlayer())
                    {
                        distance = GetDistance(victim, hitInfo);
                    }
                    NextTick(() =>
                    {
                        if (DamageForAttacker && hitInfo?.Initiator?.ToPlayer() != victim.ToPlayer())
                        {
                            UseUI(hitInfo?.Initiator?.ToPlayer(), "-" + damage.ToString() + " HP", distance, vName, bodypart, xminattacker, xmaxattacker, yminattacker, ymaxattacker);
                        }
                        if (DamageForVictim && hitInfo?.Initiator?.ToPlayer() != victim.ToPlayer())
                        {
                            UseUI(victim.ToPlayer(), "<color=#fee3b4>-" + damage.ToString() + " HP" + "</color>", "<color=#fee3b4>" + distance + "</color>", "<color=#fee3b4>" + aName + "</color>", "<color=#fee3b4>" + bodypart + "</color>", xminvictim, xmaxvictim, yminvictim, ymaxvictim);
                        }
                    });
                    timer.Once(displaytime, () =>
                    {
                        DestroyNotification(hitInfo?.Initiator?.ToPlayer());
                        DestroyNotification(victim.ToPlayer());
                    });
                });
                /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

            }
        }

        /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
        /////////////////////////////////					  Extra					 \\\\\\\\\\\\\\\\\\\\\\\\\\
        /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

        public static string RemoveSpecialCharacters(string str)
        {
            char[] buffer = new char[str.Length];
            int index = 0;
            foreach (char c in str)
            {
                if (Char.IsLetter(c))
                {
                    buffer[index] = c;
                    index++;
                }
            }
            return new string(buffer, 0, index);
        }


        void Loaded()
        {
            LoadSavedData();
            //Puts("DamageDisplay by cogu is now LIVE!");
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                users.Add(player.userID);
            }
        }

        void OnPlayerInit()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                users.Add(player.userID);
            }
        }

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("DamageDisplay", users);
        void LoadSavedData()
        {
            HashSet<ulong> users = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>("DamageDisplay");
            this.users = users;
        }

        private void UseUI(BasePlayer player, string dmg, string dst, string name, string bpart, float xmin, float xmax, float ymin, float ymax)
        {
            CuiHelper.DestroyUi(player, "DamageDisplay");
            float dtime = DisplayTime;
            float xmin_1 = xmin + 0.08f;
            float xmax_1 = xmax + 0.08f;
            float xmin_2 = xmin + 0.30f;
            float xmax_2 = xmax + 0.30f;
            var elements = new CuiElementContainer();
            CuiElement damage = new CuiElement
            {
                Name = "DamageDisplay",
                //Parent = "HUD/Overlay",
                //FadeOut = dtime,
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"<color=#ff5400>Урон:</color> <color=#fee3b4>{dmg}</color>",
                            FontSize = 16,
                            Align = TextAnchor.MiddleLeft,
                            //FadeIn = dtime
                        },
                        new CuiOutlineComponent
                        {
                            Distance = "1.0 1.0",
                            Color = "0.0 0.0 0.0 1.0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = xmin + " " + ymin,
                            AnchorMax = xmax + " " + ymax
                        }
                    }
            };
            elements.Add(damage);

            CuiElement nameof = new CuiElement
            {
                Name = "DamageDisplay",
                //Parent = "HUD/Overlay",
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "<color=#ff5400>Игрок:</color> <color=#fee3b4>"+name + $"</color> <color=#ff5400>{bpart}</color>",
                            FontSize = 16,
                            Align = TextAnchor.MiddleLeft,
                            //FadeIn = dtime
                        },
                        new CuiOutlineComponent
                        {
                            Distance = "1.0 1.0",
                            Color = "0.0 0.0 0.0 1.0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = xmin_1 + " " + ymin,
                            AnchorMax = xmax_1 + " " + ymax
                        }
                    }
            };
            elements.Add(nameof);

            CuiElement range = new CuiElement
            {
                Name = "DamageDisplay",
                //Parent = "HUD/Overlay",
                //FadeOut = dtime,
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"<color=#ff5400>Дист:</color> <color=#fee3b4>{dst}</color>",
                            FontSize = 16,
                            Align = TextAnchor.MiddleLeft,
                            //FadeIn = dtime
                        },
                        new CuiOutlineComponent
                        {
                            Distance = "1.0 1.0",
                            Color = "0.0 0.0 0.0 1.0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = xmin_2 + " " + ymin,
                            AnchorMax = xmax_2 + " " + ymax
                        }
                    }
            };
            elements.Add(range);

            CuiHelper.AddUi(player, elements);
        }

        private void DestroyNotification(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "DamageDisplay");
        }

        string GetBoneName(BaseCombatEntity entity, uint boneId) => entity?.skeletonProperties?.FindBone(boneId)?.name?.english ?? "body";
        string FirstUpper(string original)
        {
            if (original == string.Empty)
                return string.Empty;
            List<string> output = new List<string>();
            foreach (string word in original.Split(' '))
                output.Add(word.Substring(0, 1).ToUpper() + word.Substring(1, word.Length - 1));
            return ListToString(output, 0, " ");
        }
        string ListToString(List<string> list, int first, string seperator) => string.Join(seperator, list.Skip(first).ToArray());
        string GetDistance(BaseCombatEntity entity, HitInfo info)
        {
            float distance = 0.0f;
            if (entity != null && info.Initiator != null)
            {
                distance = Vector3.Distance(info.Initiator.transform.position, entity.transform.position);
            }
            return distance.ToString("0.0").Equals("0.0") ? "" : distance.ToString("0.0") + "m";
        }

        /////////////////////////////////////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
    }
}
