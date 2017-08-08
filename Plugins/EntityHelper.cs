using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Entity Helper","bazuka5801","1.1.0")]
    public class EntityHelper: RustPlugin
    {
        Dictionary<string,int> entityTypes = new Dictionary<string, int>();
        void ExecuteEntityTypes()
        {
            entityTypes.Clear();
            foreach (var entity in BaseEntity.saveList)
            {
                var prefab = entity.ShortPrefabName;
                if (!entityTypes.ContainsKey(prefab))
                    entityTypes[prefab] = 1;
                else entityTypes[prefab]++;
            }
            foreach (var type in entityTypes.OrderByDescending(e=>e.Value))
            {
                Puts($"{type.Key}:{type.Value}");
            }
        }
        [ConsoleCommand("worlditems")]
        void cmdworlditems(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            Puts(UnityEngine.Object.FindObjectsOfType<WorldItem>().Length.ToString());
        }
        [ConsoleCommand("entitytypes")]
        void cmdEntityTypes(ConsoleSystem.Arg arg)
        {
            if (arg.Connection!= null)return;
            ExecuteEntityTypes();
        }

        [ConsoleCommand("planners")]
        void cmdPlanners(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;

            var poses = new Dictionary<Vector3, int>();
            foreach (var entity in BaseEntity.saveList)
            {
                var pos = entity.transform.position;
                if (!poses.ContainsKey(Vector3Ex.Parse(pos.ToString("F0"))))
                    poses[pos] = 1;
                else poses[pos]++;
            }
            foreach (var type in entityTypes.OrderByDescending(e => e.Value).Where(p=>p.Value > 1))
            {
                Puts($"{type.Key}:{type.Value}");
            }
        }
        [ConsoleCommand("objectscount")]
        void cmdObjectsCount(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            GameObject[] allGameobjects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
            Puts(allGameobjects.Length.ToString());
        }

        [ConsoleCommand("buildingstypes")]
        void cmdbuildingstypes(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            Dictionary<string, string> dic = new Dictionary<string, string>();
            foreach (var entity in BaseEntity.saveList)
            {
                if ((entity as DecayEntity) == null)continue;
                var name = entity.ShortPrefabName;
                dic[entity.PrefabName] = name;
            }
            var s = new StringBuilder();
            foreach (var d in dic)
            {
                s.Append($"\"{d.Value}\",\n");
            }
            Puts($"{s}");
        }
        [ConsoleCommand("stockings")]
        void cmdStockings(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            Puts($"{Stocking.stockings.Count} STOCKINGS");
        }

        [ConsoleCommand("killbuildingblocks")]
        void cmdKillBuildingBlocks(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            var buildings = UnityEngine.Object.FindObjectsOfType<BuildingBlock>();
            foreach (var building in buildings)
                building.Kill();
        }
    }
}
