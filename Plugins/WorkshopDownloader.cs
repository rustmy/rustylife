// Reference: Oxide.Core.RustyCore
using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using System.Collections;
using Rust;

namespace Oxide.Plugins
{
    [Info("WorkshopDownloader", "bazuka5801", "1.0.0")]
    class WorkshopDownloader : RustPlugin
    {
        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            InitializeItemList();
            CommunityEntity.ServerInstance.StartCoroutine(GetWorkshopSkins());
        }

        #endregion

        #region CORE
        public class ImageData
        {
            public Dictionary<string, Dictionary<ulong, string>> ImageURLs = new Dictionary<string, Dictionary<ulong, string>>();
        }
        
        ImageData data;
        public IEnumerator GetWorkshopSkins()
        {
            var workshopQuery = Global.SteamServer.Workshop.CreateQuery();
            workshopQuery.Page = 1;
            workshopQuery.PerPage = 50000;
            workshopQuery.RequireTags.Add("Version3");
            workshopQuery.Run();

            yield return new WaitWhile(new System.Func<bool>(() => workshopQuery.IsRunning));
            bool flag = false;
            data = new ImageData();
            foreach (var item in workshopQuery.Items)
            {
                string itemshortname = null;
                flag = false;
                foreach (var tag in item.Tags)
                {
                    string removeskin = tag.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "");
                    if (ItemLists.ContainsKey(removeskin))
                    {
                        itemshortname = ItemLists[removeskin];
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    continue;
                }

                if (!data.ImageURLs.ContainsKey(itemshortname))
                    data.ImageURLs[itemshortname] = new Dictionary<ulong, string>();
                data.ImageURLs[itemshortname].Add(item.Id, item.PreviewImageUrl);
            }
            workshopQuery.Dispose();
            Interface.Oxide.DataFileSystem.GetFile("WorkshopSkins").WriteObject(data);
            Puts("Success download!");
        }
        Dictionary<string, string> ItemLists = new Dictionary<string, string>();
        void InitializeItemList()
        {
            ItemLists.Clear();
            foreach (var item in ItemManager.itemList)
            {
                ItemLists.Add(item.displayName.english.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", ""), item.shortname);
                //item.skins2 = new Facepunch.Steamworks.Inventory.Definition[0];
            }
            ItemLists.Add("longtshirt", ItemManager.FindItemDefinition("tshirt.long").shortname);
            ItemLists.Add("cap", ItemManager.FindItemDefinition("hat.cap").shortname);
            ItemLists.Add("beenie", ItemManager.FindItemDefinition("hat.beenie").shortname);
            ItemLists.Add("boonie", ItemManager.FindItemDefinition("hat.boonie").shortname);
            ItemLists.Add("balaclava", ItemManager.FindItemDefinition("mask.balaclava").shortname);
            ItemLists.Add("pipeshotgun", ItemManager.FindItemDefinition("shotgun.waterpipe").shortname);
            ItemLists.Add("woodstorage", ItemManager.FindItemDefinition("box.wooden").shortname);
            ItemLists.Add("ak47", ItemManager.FindItemDefinition("rifle.ak").shortname);
            ItemLists.Add("boltrifle", ItemManager.FindItemDefinition("rifle.bolt").shortname);
            ItemLists.Add("bandana", ItemManager.FindItemDefinition("mask.bandana").shortname);
            ItemLists.Add("snowjacket", ItemManager.FindItemDefinition("jacket.snow").shortname);
            ItemLists.Add("buckethat", ItemManager.FindItemDefinition("bucket.helmet").shortname);
            ItemLists.Add("semiautopistol", ItemManager.FindItemDefinition("pistol.semiauto").shortname);
            ItemLists.Add("burlapgloves", ItemManager.FindItemDefinition("burlap.gloves").shortname);
            ItemLists.Add("roadsignvest", ItemManager.FindItemDefinition("roadsign.jacket").shortname);
            ItemLists.Add("roadsignpants", ItemManager.FindItemDefinition("roadsign.kilt").shortname);
            ItemLists.Add("burlappants", ItemManager.FindItemDefinition("burlap.trousers").shortname);
            ItemLists.Add("collaredshirt", ItemManager.FindItemDefinition("shirt.collared").shortname);
            ItemLists.Add("mp5", ItemManager.FindItemDefinition("smg.mp5").shortname);
            ItemLists.Add("sword", ItemManager.FindItemDefinition("longsword").shortname);
            ItemLists.Add("workboots", ItemManager.FindItemDefinition("shoes.boots").shortname);
            ItemLists.Add("vagabondjacket", ItemManager.FindItemDefinition("jacket").shortname);
            ItemLists.Add("hideshoes", ItemManager.FindItemDefinition("shoes.boots").shortname);
            ItemLists.Add("deerskullmask", ItemManager.FindItemDefinition("deer.skull.mask").shortname);
            ItemLists.Add("minerhat", ItemManager.FindItemDefinition("hat.miner").shortname);
            ItemLists.Add("hideshirt", ItemManager.FindItemDefinition("shirt.tanktop").shortname);
        }
        #endregion
    }
}
