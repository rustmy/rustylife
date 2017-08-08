using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RustyCore.Items
{
    internal class RustInventory
    {

        public RustItemContainer containerMain;

        public RustItemContainer containerBelt;

        public RustItemContainer containerWear;

        

        public static RustInventory fromInventory(PlayerInventory invent)
        {
            RustInventory rustyCoreInventory = new RustInventory()
            {
                containerBelt = RustItemContainer.fromContainer(invent.containerBelt, "belt"),
                containerMain = RustItemContainer.fromContainer(invent.containerMain, "main"),
                containerWear = RustItemContainer.fromContainer(invent.containerWear, "wear")
            };
            invent.Strip();
            return rustyCoreInventory;
        }

        public void toInventory(PlayerInventory invent)
        {
            invent.Strip();
            containerBelt.toContainer(invent.containerBelt);
            containerMain.toContainer(invent.containerMain);
            containerWear.toContainer(invent.containerWear);
        }
    }
    internal class RustItem
    {
        public string shortname;
        public int itemid;
        public string container;
        public float condition;
        public float maxCondition;
        public int amount;
        public int ammoamount;
        public string ammotype;
        public int flamefuel;
        public ulong skinid;
        public bool weapon;
        public List<RustItem> mods;

        public RustItem()
        {
        }

        Item BuildItem(RustItem sItem, ulong skin = ulong.MaxValue)
        {
            if (sItem.amount < 1) sItem.amount = 1;
            Item item = ItemManager.CreateByItemID(sItem.itemid, sItem.amount, skin != ulong.MaxValue ? skin : sItem.skinid);
            if (item.hasCondition)
            {
                item.condition = sItem.condition;
                item.maxCondition = sItem.maxCondition;
            }
            FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();
            if (flameThrower)
                flameThrower.ammo = sItem.flamefuel;
            return item;
        }

        Item BuildWeapon(RustItem sItem, ulong skin = ulong.MaxValue)
        {
            Item item = ItemManager.CreateByItemID(sItem.itemid, 1, skin != ulong.MaxValue ? skin : sItem.skinid);
            if (item.hasCondition)
                item.condition = sItem.condition;
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                var def = ItemManager.FindItemDefinition(sItem.ammotype);
                weapon.primaryMagazine.ammoType = def;
                weapon.primaryMagazine.contents = sItem.ammoamount;
            }

            if (sItem.mods != null)
                foreach (var mod in sItem.mods)
                    item.contents.AddItem(BuildItem(mod).info, 1);
            return item;
        }

        public static RustItem fromItem(Item item, string container)
        {
            RustItem iItem = new RustItem
            {
                shortname = item.info?.shortname,
                amount = item.amount,
                mods = new List<RustItem>(),
                container = container,
                skinid = item.skin
            };
            
            if (item.info == null) return iItem;
            iItem.itemid = item.info.itemid;
            iItem.weapon = false;
            if (item.hasCondition)
            {
                iItem.condition = item.condition;
                iItem.maxCondition = item.maxCondition;
            }
            FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();
            if (flameThrower != null)
                iItem.flamefuel = flameThrower.ammo;
            BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon == null) return iItem;
            if (weapon.primaryMagazine == null) return iItem;
            iItem.ammoamount = weapon.primaryMagazine.contents;
            iItem.ammotype = weapon.primaryMagazine.ammoType.shortname;
            iItem.weapon = true;
            if (item.contents != null)
                foreach (var mod in item.contents.itemList)
                    if (mod.info.itemid != 0)
                        iItem.mods.Add(fromItem(mod, "noun"));
            return iItem;
        }

        public Item toItem(ulong skin = ulong.MaxValue)
        {
            if (!this.weapon)
            {
                return this.BuildItem(this, skin);
            }
            return this.BuildWeapon(this, skin);
        }
    }

    internal class RustItemContainer
    {
        private static MethodInfo containerInsert = typeof(ItemContainer).GetMethod("Insert",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public RustItem[] container;
        public string type;

        public RustItemContainer(int size)
        {
            this.container = new RustItem[size];
        }

        public static RustItemContainer fromContainer(ItemContainer cont, string pType)
        {
            RustItemContainer itemContainer = new RustItemContainer(cont.itemList.Count)
            {
                type = pType
            };
            for (int i = 0; i < itemContainer.container.Count<RustItem>(); i++)
            {
                itemContainer.container[i] = RustItem.fromItem(cont.itemList[i], pType);
            }
            return itemContainer;
        }

        public void toContainer(ItemContainer container)
        {
            foreach (var item in getItems())
            {
                item.MoveToContainer(container);
            }
        }

        public Item[] getItems()
        {
            Item[] itemArray = new Item[this.container.Count<RustItem>()];
            for (int i = 0; i < this.container.Count<RustItem>(); i++)
            {
                itemArray[i] = this.container[i].toItem();
            }
            return itemArray;
        }

        public string getStatus()
        {
            return container?.Length.ToString() ?? "null";
        }
    }
}
