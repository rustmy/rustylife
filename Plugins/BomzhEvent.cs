// Reference: Oxide.Core.RustyCore
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using RustyCore;
using RustyCore.Utils;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BomzhEvent", "bazuka5801","1.0.0")]
    public class BomzhEvent : RustPlugin
    {
        private bool isStart = false;
        private Vector3 eventPos;
        private BasePlayer admin;
        List<ulong> inEvent = new List<ulong>();
        bool free = true;
        int max = 0;
        RCore core = Interface.Oxide.GetLibrary<RCore>();

        void Loaded()
        {
            
        }

        [ChatCommand("bomzh")]
        void cmdChatEvent(BasePlayer player,string command,string[] args)
        {
            if (!player.IsAdmin)return;
            if (args.Length == 1)
            {
                isStart = !isStart;
                if (isStart)
                {
                    admin = player;
                    eventPos = player.transform.position;
                    max = int.Parse(args[0]);
                    var msg =
$@"<size=16><color=green>Запущен Event, долбёжка стенок на <color=red>{max}</color> игроков
За стенками находятся ящики с рандомным лутом(ценным)
<color=red>НА ИВЕНТ ИДТИ ГОЛЫМ - ВСЕ ВАШИ ВЕЩИ ПРОПАДУТ!</color>
Чтобы попасть на ивент пишите в чат 'event10'</color></size>";
                    BroadcastHandler(msg);
                    SendReply(player, "Запущен ивент с " + max + " игроками");
                }
                else
                {
                    foreach (var uid in inEvent)
                        rust.RunServerCommand($"revoke user {uid} noescape.ignore");
                    eventPos = Vector3.zero;
                    timer.Once(60f, () => {
                        inEvent.Clear();
                    });
                }
            }
        }

        void BroadcastHandler(string msg)
        {
            foreach (var p in BasePlayer.activePlayerList)
                p.ChatMessage(msg);
            if (inEvent.Count < max)
            {
                timer.Once(60f, () => BroadcastHandler(msg));
            }
        }

        List<string> lockItems = new List<string>()
        {
            "bleach",
            "glue",
            "blueprintbase",
            "ducttape",
            "mining.pumpjack"
        };

        bool InEvent(BasePlayer player)
        {
            return inEvent.Contains(player.userID);
        }

        [ChatCommand("putsunduk")]
        void cmdPutSunduk(BasePlayer player)
        {
            if (!player.IsAdmin) return;

            if (player.inventory.loot != null && player.inventory.loot.containers.Count > 0)
            {
                var cont = player.inventory.loot.containers[0];
                RandomContainer(cont);
            }
            player.ChatMessage("Ящик заполнен");
        }

        void RandomContainer(ItemContainer cont)
        {
            for (int i = cont.itemList.Count - 1; i >= 0; i--)
            {
                var item = cont.itemList[i];
                item.Remove(0.0f);
            }
            var items = ItemManager.itemList.Where(i => !lockItems.Contains(i.shortname)).ToList();
            items.Shuffle((uint)UnityEngine.Random.Range(10, 25369));
            while (!cont.IsFull())
            {
                var itemDef = items.GetRandom();
                var item = ItemManager.CreateByItemID(itemDef.itemid, UnityEngine.Random.Range(1, itemDef.stackable));
                item.MoveToContainer(cont);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            if (!player.IsAdmin) return;
            if (go.name == "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab" || go.name == "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab")
            {
                var storage = go.GetComponent<StorageContainer>();
                if (storage != null)
                    RandomContainer(storage.inventory);
            }
        }

        

        void OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (!isStart) return;
            var player = arg.Player();
            if (!inEvent.Contains(player.userID) && arg.Args[0].Replace("\"","") == "event10" && free)
            {
                if (inEvent.Count >= max)
                {
                    player.ChatMessage("<size=16><color=red>Всё, все слоты заняты, ожидайте след. ивент</color></size>");
                    return;
                }
                free = false;
                timer.Once(2f,()=> { free = true; });
                admin.ChatMessage(player.displayName + " Присоединился к Event-у");
                inEvent.Add(player.userID);
                rust.RunServerCommand($"grant user {player.userID} noescape.ignore");
                player.inventory.Strip();
                rust.RunServerCommand("inv.giveplayer "+player.UserIDString+" pickaxe 24");
                rust.RunServerCommand("inv.giveplayer " + player.UserIDString + " hat.miner 150");
                core.Teleport(player, eventPos);
            }
        }
        object CanTeleport(BasePlayer player)
        {
            if (isStart && inEvent.Contains(player.userID)) { return "Телепорт заблокирован, вы не можете \"убежать\" с ивента!"; }
            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info?.Initiator == null) return null;
            var p1 = entity as BasePlayer;
            var p2 = info.Initiator as BasePlayer;
            if (p1 == null || p2 == null) return null;
            if (inEvent.Contains(p1.userID) || inEvent.Contains(p2.userID))
            {
                Puts("Пропущен урон: "+p1.displayName+", "+p2.displayName);
                info.damageTypes.ScaleAll(0);
                return (object) true;
            }
            return null;
        }
    }
}
