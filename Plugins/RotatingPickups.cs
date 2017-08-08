// Reference: Oxide.Core.RustyCore

using Oxide.Core;
using RustyCore.Utils;
using RustyCore;
using System.Collections.Generic;
using System.Linq;
using System;
using TAA;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RotatingPickups", "bazuka5801", "1.0.0")]
    class RotatingPickups : RustPlugin
    {
        #region CLASSES

        class RotatePickupComponent : MonoBehaviour
        {
            Transform transform;
            //Rigidbody rigidbody;
            Vector3 position;
            BaseEntity entity;
            public bool Error = false;
            private bool lerp = false;
            private bool work = false;
            private Transform parentTransform;
            private Vector3 lastParentPosition;
            void Awake()
            {
                transform = GetComponent<Transform>();
                entity = GetComponent<BaseEntity>();
            }

            void OnCollisionEnter(Collision collision)
            {
                CancelInvoke("StartRotate");
                Invoke("StartRotate", 0.5f);
            }

            void StartRotate()
            {
                var rigidbody = GetComponent<Rigidbody>();
                if (rigidbody != null)
                    Destroy(rigidbody);

                transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
                work = true;

                UpdatePosition();
            }

            void UpdatePosition()
            {
                var ray = new Ray(transform.position + new Vector3(0, 0.05f, 0),
                    Vector3.down);
                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, 500, WorldBuildingsLayer, QueryTriggerInteraction.Ignore) ||
                    hit.point.y < -1000)
                {
                    Error = true;
                    return;
                }
                parentTransform = hit.transform.root.transform;
                lastParentPosition = parentTransform.position;
                position = hit.point + new Vector3(0, instance.offsetY, 0);
                lerp = Vector3.Distance(position, transform.position) > 0.1f;
                //transform.position = position;
                if (position.y < -1000)
                {
                    entity.Kill();
                    return;
                }
            }

            public void MyFixedUpdate()
            {
                if (work &&  (parentTransform == null || lastParentPosition != parentTransform.position))
                {
                    UpdatePosition();
                }
                if (!work) return;
                if (lerp)
                {
                    transform.position = Vector3.Lerp(transform.position, position, 0.05f);
                }
                transform.Rotate(0, 10, 0);
            }

            void OnDestroy()
            {
                // rigidbody.useGravity = true;
            }
        }

        #endregion

        #region CONFIGURATION

        float offsetY;
        int limit;

        protected override void LoadDefaultConfig()
        {
            Config.GetVariable("Высота над поверхностью", out offsetY, 1.5f);
            Config.GetVariable("Максимальное кол-во вращаемых объектов", out limit, 1000);
            SaveConfig();
        }

        #endregion

        #region FIELDS

        static RotatingPickups instance;

        static RCore core = Interface.Oxide.GetLibrary<RCore>();

        static int WorldBuildingsLayer = LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "Resource", "World", "Water", "Default", "Prevent Building");
        
        private Dictionary<BaseNetworkable, RotatePickupComponent> rotatingItems =
            new Dictionary<BaseNetworkable, RotatePickupComponent>();

        #endregion

        #region OXIDE HOOKS

        void Loaded()
        {
            instance = this;
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
        }

        void Unload()
        {
            foreach (var c in rotatingItems)
            {
                UnityEngine.Object.Destroy(c.Value);
                c.Key.SendNetworkUpdate();
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            // ignore loot box collision
            NextTick(() =>
            {
                if (entity == null) return;
                DroppedItemContainer droppedContainer = entity as DroppedItemContainer;
                if (droppedContainer != null)
                {
                    timer.Once(300f, () =>
                    {
                        if (droppedContainer != null && !droppedContainer.IsDestroyed) droppedContainer.Kill();
                    });
                }
                DroppedItem item = entity as DroppedItem;
                if (item != null)
                {
                    if (item?.item?.info?.worldModelPrefab == null) return;
                    if (!item.item.info.worldModelPrefab.isValid) return;
                    try
                    {
                        if (rotatingItems.Count < limit)
                        {
                            var component = item?.gameObject?.AddComponent<RotatePickupComponent>();
                            if (component == null || component.Error)
                            {
                                UnityEngine.Object.Destroy(component);
                                return;
                            }
                            rotatingItems.Add(entity, component);
                        }

                    }
                    catch (Exception ex)
                    {
                        Puts(ex.Message + Environment.NewLine + ex.StackTrace);
                        throw;
                    }
                }
            });
        }


        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            DroppedItem item = entity as DroppedItem;
            if (item?.item?.info?.worldModelPrefab == null) return;
            if (!item.item.info.worldModelPrefab.isValid) return;
            RotatePickupComponent component;
            if (rotatingItems.TryGetValue(entity, out component))
            {
                if (component)
                    UnityEngine.Object.Destroy(component);
                rotatingItems.Remove(entity);
            }
        }

        
        float lastupdate = -1;
        float timeout = 0.05f;
        private int i = 0;

        [ConsoleCommand("rot.debug")]
        void cmdD(ConsoleSystem.Arg arg)
        {
            if (arg?.Connection != null) return;
            debug = !debug;
        }

        private bool debug = false;

        void OnTick()
        {
            if (Time.time > lastupdate)
            {
                if (debug && ++i % 20 == 0)
                {
                    Puts("Rotating Items Count: "+rotatingItems.Count);
                }
                List<BaseNetworkable> toRemove = new List<BaseNetworkable>();
                lastupdate = Time.time;
                foreach (var item in rotatingItems)
                {
                    if (item.Value == null)
                    {
                        toRemove.Add(item.Key);
                        continue;
                    }
                    item.Value.MyFixedUpdate();
                }
                toRemove.ForEach(p=>rotatingItems.Remove(p));
            }
        }

        #endregion

        #region CORE

        #endregion
    }
}
