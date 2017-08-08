using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using RustyCore.Items;

namespace RustyCore.Utils
{
    public static class InventoryUtils
    {
        private static readonly Dictionary<ulong, RustInventory> storage;

        static InventoryUtils()
        {
            storage = new Dictionary<ulong, RustInventory>();
        }

        public static void SaveInventory(BasePlayer player)
        {
            storage[player.userID] = RustInventory.fromInventory(player.inventory);
        }

        public static void RestoreInventory(BasePlayer player)
        {
            var steamid = player.userID;
            RustInventory inventory;
            if (!storage.TryGetValue(steamid,out inventory))
            {
                Logger.Error($"[{nameof(InventoryUtils)}]: can't restore player inventory, key was not present in {nameof(storage)}");
                return;
            }
            inventory.toInventory(player.inventory);
            storage.Remove(steamid);
        }

        public static Item CloneItem(Item item, ulong skin = ulong.MaxValue)
        {
            return RustItem.fromItem(item, string.Empty).toItem(skin);
        }
    }
}
