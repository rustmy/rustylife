using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    [Info( "FindMaxItem", "bazuka5801", "1.0.0" )]
    class FindMaxItem : RustPlugin
    {
        void OnServerInitialized()
        {
            var objs = UnityEngine.Object.FindObjectsOfType<StorageContainer>();
            Dictionary<string,int> itemCounts = new Dictionary<string, int>();
            foreach (var box in objs)
            {
                foreach (var item in box.inventory.itemList.Where( p => p != null ))
                {
                    if (!itemCounts.ContainsKey(item.info.shortname)) itemCounts[item.info.shortname] = 0;
                    itemCounts[item.info.shortname] += item.amount;
                }
            }
            var sb = new StringBuilder("\n");
            foreach (var item in itemCounts.OrderBy(p=>p.Value))
            {
                sb.Append($"{item.Key}: {item.Value}\n");
            }
            Puts(sb.ToString());
        }
    }
}
