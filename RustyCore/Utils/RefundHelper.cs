using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RustyCore.Utils
{
    public static class RefundHelper
    {
        private static Dictionary<uint, Dictionary<ItemDefinition, int>> refundItems =
            new Dictionary<uint, Dictionary<ItemDefinition, int>>();

        public static void Refund( BasePlayer player, BaseEntity entity, float percent = -1 )
        {
            StorageContainer storage = entity as StorageContainer;
            if (storage)
            {
                for (int i = storage.inventory.itemList.Count - 1; i >= 0; i--)
                {
                    var item = storage.inventory.itemList[ i ];
                    if (item == null) continue;
                    item.amount = (int) ( item.amount * percent );
                    float single = 20f;
                    Vector3 vector32 = Quaternion.Euler( UnityEngine.Random.Range( -single * 0.5f, single * 0.5f ), UnityEngine.Random.Range( -single * 0.5f, single * 0.5f ), UnityEngine.Random.Range( -single * 0.5f, single * 0.5f ) ) * Vector3.up;
                    BaseEntity baseEntity = item.Drop( storage.transform.position + ( Vector3.up * 1f ), vector32 * UnityEngine.Random.Range( 5f, 10f ), UnityEngine.Random.rotation );
                    baseEntity.SetAngularVelocity( UnityEngine.Random.rotation.eulerAngles * 5f );
                }
            }

            BuildingBlock block = entity as BuildingBlock;
            if (block != null)
            {
                try
                {
                    if (block.currentGrade == null) return;
                    foreach (var item in block.currentGrade.costToBuild)
                    {
                        var amount = (int) ( item.amount * ( Mathf.Approximately( percent, -1 ) ? 0.5f : percent ) );
                        if (amount < 1) amount = 1;
                        player.GiveItem( ItemManager.Create( item.itemDef, amount, 1 ) );
                    }

                }
                catch
                {

                }
                return;
            }

            Dictionary<ItemDefinition, int> items;
            if (refundItems.TryGetValue( entity.prefabID, out items ))
            {
                foreach (var item in items)
                    if (item.Value > 0)
                        player.GiveItem( ItemManager.Create( item.Key, (int) ( item.Value ) ) );
            }
        }

        private static void InitRefundItems()
        {
            foreach (var item in ItemManager.itemList)
            {
                var deployable = item.GetComponent<ItemModDeployable>();
                if (deployable != null)
                {
                    if (item.Blueprint == null || deployable.entityPrefab == null) continue;
                    refundItems.Add( deployable.entityPrefab.resourceID, item.Blueprint.ingredients.ToDictionary( p => p.itemDef, p => ( (int)p.amount  ) ) );
                }
            }
        }
    }
}
