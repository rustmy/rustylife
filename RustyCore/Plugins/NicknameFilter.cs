using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Oxide.Plugins;
using RustyCore.Utils;
using UnityEngine;

namespace RustyCore.Plugins
{
    [Info("NicknameFilter","bazuka5801","1.0.0")]
    internal class NicknameFilter : RustPlugin
    {

        void OnServerInitialized()
        {
            timer.Every(90, () => CommunityEntity.ServerInstance.StartCoroutine(VerifyPlayers()));
        }

        IEnumerator VerifyPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                player.displayName =
                    player.displayName.Replace("$", "")
                        .Replace("<", "")
                        .Replace(">", "")
                        .Replace("#", "")
                        .Replace("@", "");
                yield return new WaitForEndOfFrame();
            }
            /*foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                object res = IsValidNickname(player.displayName);
                if ((res as string) != null)
                    player.Kick((string) res);
                yield return new WaitForEndOfFrame();
            }*/
        }

/*
                                object CanClientLogin(Network.Connection connection)
                                {
                                    return IsValidNickname(connection.username);
                                }
                        
                                object IsValidNickname(string name)
                                {
                                    name = Regex.Replace(name.ToLower(), "^\\[(.*)\\]", "");
                                    if (name.Length < 4) return "Ник должен содержать более 4 символов!";
                                    if (name.IsLink()) return "В нике запрещена реклама сторонних сайтов!";
                                    if (name.IsBadSymbols()) return $"В нике '{name}' должны быть только Буквы или цифры!";
                        
                                    if (name.IsBadWord())
                                        return $"В нике '{name}' содержатся нецензурные выражения";
                                    return null;
                                }
                                */
        [ConsoleCommand("swearwords.reload")]
        void cmdSwearWordsReload(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            FilterWords.LoadConfig();
        }


    }
}
