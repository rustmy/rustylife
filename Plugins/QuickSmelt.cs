// Reference: Oxide.Core.RustyCore
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using RustyCore.Utils;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("QuickSmelt", "bazuka5801","1.0.0")]
    public class QuickSmelt : RustPlugin
    {
        ItemDefinition charcoalDef = ItemManager.FindItemDefinition("charcoal");
        Dictionary<ItemDefinition, ItemModCookable> cookableItems;

        #region CONFIGURATION

        int multiplier;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Множитель",out multiplier, 1);
            SaveConfig();
        }

        #endregion


        #region OXIDE HOOKS

        bool init = false;
        void OnServerInitialized()
        {
            LoadDefaultConfig();
            InitCookable();
            init = true;
            PermissionService.RegisterPermissions(this, permissions);
        }
        
        void InitCookable()
        {
            cookableItems =
                ItemManager.itemList.ToDictionary(i => i, i => i.GetComponent<ItemModCookable>())
                    .Where(i => i.Value != null && !i.Value.becomeOnCooked.shortname.Contains("burned")).ToDictionary(p=>p.Key,p=>p.Value);
        }

        void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (!init) return;
            bool charcoal = false;
            int mul = GetMultiplier(oven.OwnerID);



            foreach (var item in oven.inventory.itemList.ToList())
            {
                if (!charcoal && item.info == charcoalDef)
                {
                    charcoal = true;
                    item.amount += mul;
                }
                if (!cookableItems.ContainsKey(item.info)) continue;
                int amountToCreate;
                if (item.amount > mul)
                {
                    item.MarkDirty();
                    item.amount -= mul;
                    amountToCreate = mul;
                }
                else
                {
                    amountToCreate = item.amount;
                    item.RemoveFromContainer();
                    item.Remove();
                }

                var cookedDef = cookableItems[item.info].becomeOnCooked;
                var cookedAmount = amountToCreate*cookableItems[item.info].amountOfBecome;
                var cookedOvenItem =
                    oven.inventory.itemList.Where(p => p.info == cookedDef).OrderBy(p => p.amount).FirstOrDefault();
                if (cookedOvenItem != null)
                {
                    var amount = Math.Min(cookedOvenItem.MaxStackable() - cookedOvenItem.amount, cookedAmount);
                    cookedOvenItem.amount += amount;
                    if (cookedAmount - amount <= 0)
                        continue;
                }
                var cookedItem = ItemManager.Create(cookedDef, cookedAmount);
                if (!cookedItem.MoveToContainer(oven.inventory))
                    cookedItem.Drop(oven.GetDropPosition(), oven.GetDropVelocity());
            }
        }

        int GetMultiplier(ulong uid)
        {
            if (PermissionService.HasPermission(uid, PERM_X5)) return 5;
            if (PermissionService.HasPermission(uid, PERM_X4)) return 4;
            return multiplier;
        }


        #endregion


        #region PERMISSIONS

        const string PERM_X4 = "quicksmelt.x4";
        const string PERM_X5 = "quicksmelt.x5";

        List<string> permissions = new List<string>()
        {
            PERM_X4,
            PERM_X5
        };

        #endregion


        #region Helper Methods

        bool HasPermission(string userId, string perm) => permission.UserHasPermission(userId, perm);


        #endregion

    }
}
