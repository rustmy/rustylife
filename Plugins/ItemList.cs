// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Configuration;
using UnityEngine;
using System.IO;

namespace Oxide.Plugins
{
    [Info("ItemList", "bazuka5801", "1.0.0")]
    class ItemList : RustPlugin
    {
        [ConsoleCommand("itemlist")]
        void cmdItemList(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            Dictionary<string, List<string>> itemlist = new Dictionary<string, List<string>>();
            for (int i = 0; i < 15; i++)
                itemlist[$"{(ItemCategory)i}"] = new List<string>();
            foreach (var item in ItemManager.itemList)
            {
                itemlist[$"{item.category}"].Add($"{item.shortname}");
            }
            foreach (var itemcategory in itemlist)
            {
                var file = Interface.Oxide.DataFileSystem.GetFile($"itemlist/{itemcategory.Key}.txt");
                file.WriteObject(itemcategory.Value);
            }
        }
    }
}
