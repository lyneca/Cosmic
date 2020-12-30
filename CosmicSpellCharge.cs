using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using ThunderRoad;
using System.Collections;
using System;
using Utils.ExtensionMethods;

namespace CosmicSpell {
    static class HeldItemExtension {
        public static HeldItem HeldItem(this Item item) {
            return item?.gameObject.GetOrAddComponent<HeldItem>();
        }
    }
    public enum CastSource {
        Left, Right, Staff, Imbue
    }
    public class CosmicSpellCharge : SpellCastCharge {
        private bool isActive;

        public float attractRadius = 10.0f;
        public float attractStrength = 70.0f;
        public float imbueAttractRadius = 5.0f;
        public float imbueAttractForce = 30.0f;
        public float imbueRaiseForce = 0.5f;
        public float imbueAttractDelay = 1f;
        public float throwVelocity = 5.0f;
        public float collectObjectPushForce = 1.0f;
        public float suspendVelocityMultiplier = 0.85f;
        public float suspendAngularVelocityMultiplier = 0.95f;
        public float outerRimBlasterBoltVelocityMultiplier = 0.6f;
        public EffectVfx particleGrid;
        float lastImplosion;
        public float implosionDelay = 2;
        Quaternion startRotation;

        bool staffActive;

        public EffectInstance staffEffect;
        public List<Item> staffItems;

        private Vector3 target;
        private GameObject attractionObj;

        private List<Item> items = new List<Item>();
        private List<Item> previousItems = new List<Item>();

        public override void Load(SpellCaster caster) {
            base.Load(caster);
            previousItems = new List<Item>();
            attractionObj = new GameObject();
        }

        public override IEnumerator OnCatalogRefresh() {
            //Catalog.gameData.GetPooling().audioCount = 200;
            EventManager.onLevelLoad += (LevelData data, EventTime time) => Utils.Utils.RefreshEffectPools();
            return base.OnCatalogRefresh();
        }

        public override void Unload() {
            base.Unload();
            items?.Clear();
            previousItems?.Clear();
            particleGrid = null;
            UnityEngine.Object.Destroy(attractionObj);
        }

        public RagdollHand GetHand() {
            return spellCaster.ragdollHand;
        }

        public Side GetSide() {
            return GetHand().side;
        }

        public IEnumerator StaffCoroutine() {
            while (staffActive) {
                items = GetItemListInRadius(imbue.colliderGroup.imbueShoot.position, attractRadius * 2f).ToList();
                items.Where(item => !previousItems.Contains(item)).ToList().ForEach(item => item?.HeldItem()?.Set(CastSource.Staff));
                previousItems.Where(item => !items.Contains(item)).ToList().ForEach(item => item?.HeldItem()?.Reset(CastSource.Staff));
                previousItems = new List<Item>(items);
                target = Vector3.Lerp(target, imbue.colliderGroup.imbueShoot.position + imbue.colliderGroup.imbueShoot.forward * 2.5f, Time.deltaTime * 10.0f);
                PullItemsToPoint(items, target, 2f);
                yield return new WaitForEndOfFrame();
            }
        }

        public override void OnCrystalUse(Side side, bool active) {
            staffActive = active;
            if (active) {
                staffEffect = chargeEffectData.Spawn(imbue.colliderGroup.imbueShoot.position, Quaternion.identity);
                staffEffect.SetSource(imbue.colliderGroup.imbueShoot);
                staffEffect.SetParent(imbue.colliderGroup.collisionHandler.item.transform);
                ((EffectVfx)staffEffect.effects.Find(effect => effect is EffectVfx)).vfx.SetInt("State", 1);
                staffEffect.Play();
                Player.currentCreature.mana.StartCoroutine(StaffCoroutine());
            } else {
                if (previousItems != null)
                    previousItems.ForEach(item => item?.HeldItem().Reset(CastSource.Staff));
                if (items != null)
                    items.ForEach(item => item?.HeldItem()?.Reset(CastSource.Staff));
                staffEffect?.Stop();
                Throw(imbue.colliderGroup.collisionHandler.item.rb.GetPointVelocity(imbue.colliderGroup.imbueShoot.position), CastSource.Staff);
            }
            base.OnCrystalUse(side, active);
        }

        public class StaffBehaviour : MonoBehaviour {
            Transform target;
            Item item;
            bool useGravity;
            Vector3 offset;
            public void Start() {
                item = GetComponentInParent<Item>();
                useGravity = item.rb.useGravity;
                offset = new Vector3(
                    UnityEngine.Random.Range(0.2f, 1f) * UnityEngine.Random.Range(0, 1) * 2 - 1,
                    UnityEngine.Random.Range(0.2f, 1f) * UnityEngine.Random.Range(0, 1) * 2 - 1,
                    UnityEngine.Random.Range(-0.2f, 0.2f));
            }

            public void SetTarget(Transform transform) {
                target = transform;
            }

            public void OnDestroy() {
                item.rb.useGravity = useGravity;
            }

            public void Update() {
                if (target) {
                    item.rb.useGravity = false;
                    item.transform.position = Vector3.Lerp(item.transform.position, target.transform.TransformPoint(offset), Time.deltaTime * 10f);
                    CosmicSelfMerge.PointItemFlyRefAtTarget(item, item.transform.position + target.forward, Time.deltaTime * 10f);
                }
            }
        }

        public override void OnImbueCollisionStart(ref CollisionStruct collisionInstance) {
            base.OnImbueCollisionStart(ref collisionInstance);
            if (staffActive)
                return;
            if (Time.time - lastImplosion < implosionDelay)
                return;
            lastImplosion = Time.time;
            var contactPoint = collisionInstance.contactPoint;
            var sourceItem = collisionInstance.sourceColliderGroup.collisionHandler.item;
            foreach (Item item in Item.list.Where(
                i => i != sourceItem
                     && Vector3.Distance(contactPoint, i.transform.position) < imbueAttractRadius
                     && i.data.type != ItemPhysic.Type.Body
                     && !i.isGripped
                     && i.handlers.Count() == 0
                     && (i.lastHandler == null || !i.lastHandler.isHandlingSameObject)
                     && i.holder == null
                     && !i.rb.isKinematic
                     && i.itemId != "BlackHoleSphere"
                     && i.itemId != "WhiteHoleSphere"
                     && !i.isTelekinesisGrabbed)) {
                // if (collisionInstance.sourceColliderGroup.imbue)
                //     collisionInstance.sourceColliderGroup.imbue.energy = 0.0f;
                if (collisionInstance.targetColliderGroup?.collisionHandler != null && collisionInstance.targetColliderGroup.collisionHandler.isRagdollPart) {
                    Player.currentCreature.mana.StartCoroutine(ImbueDaggerAttractCoroutine(item, collisionInstance.targetCollider.transform));
                } else {
                    Player.currentCreature.mana.StartCoroutine(ImbueDaggerAttractCoroutine(item, contactPoint));
                }
            }
        }
        private void PointItemFlyRefAtTarget(Item item, Vector3 target, float lerpFactor) {
            item.transform.rotation = Quaternion.Slerp(
                item.transform.rotation * item.flyDirRef.localRotation,
                Quaternion.LookRotation(target),
                lerpFactor) * Quaternion.Inverse(item.flyDirRef.localRotation);
        }

        IEnumerator ImbueDaggerAttractCoroutine(Item item, Transform target) {
            item.HeldItem()?.Set(CastSource.Imbue);
            item.rb.AddForce(Vector3.up * imbueRaiseForce, ForceMode.Impulse);
            yield return new WaitForSeconds(imbueAttractDelay);
            item.HeldItem()?.Reset(CastSource.Imbue);
            item.rb.AddForce((target.transform.position - item.transform.position).normalized * imbueAttractForce * item.rb.mass / 2.0f, ForceMode.Impulse);
            item.Throw();
        }

        IEnumerator ImbueDaggerAttractCoroutine(Item item, Vector3 target) {
            item.HeldItem()?.Set(CastSource.Imbue);
            item.rb.AddForce(Vector3.up * imbueRaiseForce, ForceMode.Impulse);
            yield return new WaitForSeconds(imbueAttractDelay);
            item.HeldItem()?.Reset(CastSource.Imbue);
            item.rb.AddForce((target - item.transform.position).normalized * imbueAttractForce * item.rb.mass / 2.0f, ForceMode.Impulse);
            item.Throw();
        }

        public override void UpdateImbue() {
            base.UpdateImbue();
        }

        public override void Fire(bool active) {
            base.Fire(active);
            target = spellCaster.magicSource.position + spellCaster.magicSource.up;
            if (active) {
                startRotation = spellCaster.ragdollHand.transform.rotation;
                particleGrid = (EffectVfx)chargeEffectInstance.effects.Find(effect => effect is EffectVfx);
                if (!isActive) {
                    isActive = true;
                }
            } else {
                isActive = false;
                if (previousItems != null && previousItems.Count() > 0) {
                    previousItems.ForEach(item => item.HeldItem()?.Reset(GetSide()));
                    previousItems.Clear();
                }
                if (items != null && items.Count() > 0) {
                    items.ForEach(item => item.HeldItem()?.Reset(GetSide()));
                    items.Clear();
                    previousItems.Clear();
                }
            }
        }

        public void PullItemToPoint(Item item, Vector3 target, float forceMultiplier = 1) {
            if (item == null || target == null)
                return;
            Vector3 forceDirection = target - item.transform.position;
            if (item.data.type == ItemPhysic.Type.Weapon
                || item.data.type == ItemPhysic.Type.Shield) {
                item.rb.AddForce(forceDirection.normalized * attractStrength * forceMultiplier);
                item.rb.AddForce(-forceDirection.normalized * (float)(2.0f / Math.Pow(forceDirection.magnitude, 2.0f)));
            } else {
                item.rb.AddForce(forceDirection.normalized * forceMultiplier * item.rb.mass / 2.0f * attractStrength);
                item.rb.AddForce(-forceDirection.normalized * item.rb.mass / 2.0f * (float)(2.0f / Math.Pow(forceDirection.magnitude, 2.0f)));
            }
            if (forceDirection.magnitude < 0.7f) {
                if (item.itemId.StartsWith("BlasterBolt")) {
                    item.rb.velocity *= outerRimBlasterBoltVelocityMultiplier;
                } else {
                    item.rb.velocity *= suspendVelocityMultiplier;
                }
            }
        }

        public void PullItemsToPoint(List<Item> itemList, Vector3 target, float forceMultiplier = 1) {
            foreach (Item item in itemList) {
                if (item == null)
                    continue;
                item.Throw();
                PullItemToPoint(item, target, forceMultiplier);
                foreach (Item other in itemList) {
                    if (item == other)
                        continue;
                    var direction = item.transform.position - other.transform.position;
                    item.rb.AddForce(direction.normalized * (collectObjectPushForce / direction.magnitude));
                }
            }
        }

        public void SuspendItem(Item item) {
            item.rb.useGravity = false;
            item.rb.angularVelocity *= suspendAngularVelocityMultiplier;
            if (item.itemId.StartsWith("BlasterBolt")) {
                item.rb.velocity *= outerRimBlasterBoltVelocityMultiplier;
            } else {
                item.rb.velocity *= suspendVelocityMultiplier;
            }
        }

        public IEnumerable<Item> GetItemListInRadius(Vector3 origin, float radius) {
            return Item.list.Where(
                    item => Vector3.Distance(item.transform.position, origin) < radius
                            && item.data.type != ItemPhysic.Type.Body
                            && !item.isGripped
                            && item.handlers.Count() == 0
                            && (item.lastHandler == null || !item.lastHandler.isHandlingSameObject)
                            && item.holder == null
                            && !item.rb.isKinematic
                            && item.itemId != "BlackHoleSphere"
                            && item.itemId != "WhiteHoleSphere");
        }

        public override void UpdateCaster() {
            base.UpdateCaster();
            if (isActive) {
                chargeEffectInstance?.SetSource(spellCaster.magicSource);
                chargeEffectInstance?.SetTarget(attractionObj.transform);
                bool isGripping = (spellCaster.ragdollHand.side == Side.Left)
                                ? PlayerControl.handLeft.gripPressed
                                : PlayerControl.handRight.gripPressed;
                items = GetItemListInRadius(spellCaster.magicSource.position, attractRadius).ToList();
                items.Where(item => !previousItems.Contains(item)).ToList().ForEach(item => item.HeldItem()?.Set(GetSide()));
                previousItems.Where(item => !items.Contains(item)).ToList().ForEach(item => item.HeldItem()?.Reset(GetSide()));
                previousItems = new List<Item>(items);
                particleGrid?.vfx.SetInt("State", isGripping ? 1 : 0);
                target = Vector3.Lerp(target, spellCaster.magicSource.position + spellCaster.magicSource.up * 1.5f, Time.deltaTime * 10.0f);
                attractionObj.transform.position = target;
                if (isGripping) {
                    PullItemsToPoint(items, target, 0.5f + PlayerControl.GetHand(spellCaster.ragdollHand.side).gripAxis);
                    //items.ForEach(item => item.HeldItem().Rotate(startRotation * Quaternion.Inverse(spellCaster.ragdollHand.transform.rotation)));
                } else {
                    foreach (Item item in items) {
                        SuspendItem(item);
                    }
                }
            }
        }

        public override void Throw(Vector3 velocity) {
            Throw(velocity, SideToSource(GetSide()));
        }
        public void Throw(Vector3 velocity, CastSource source) {
            base.Throw(velocity);
            isActive = false;
            items.ForEach(item => item?.HeldItem()?.Reset(source));
            particleGrid?.vfx.SetInt("State", 2);
            particleGrid?.vfx.SetVector3("Force", Quaternion.Inverse(spellCaster.ragdollHand.transform.rotation) * velocity);
            foreach (Item item in items) {
                if (!item)
                    continue;
                item.rb.velocity = Vector3.zero;
                if (item.rb.mass < 1.0f) {
                    item.rb.AddForce(velocity * throwVelocity, ForceMode.Impulse);
                } else {
                    item.rb.AddForce(velocity * throwVelocity * item.rb.mass / 2.0f, ForceMode.Impulse);
                }
                item.Throw();
            }
        }
        public static CastSource SideToSource(Side side) {
            return (side == Side.Left) ? CastSource.Left : CastSource.Right;
        }
    }
    
    public class HeldItem : MonoBehaviour {
        public Vector3 startHeading;
        public Item item;
        bool useGravity;
        public PID headingPid;
        public PID slowingPid;
        List<CastSource> sources = new List<CastSource>();


        public void Start() {
            item = GetComponent<Item>();
            slowingPid = new PID(10, 0, 0.2553191f);
            headingPid = new PID(40, 0, 0.2382979f);
        }

        public void Set(Side side) {
            Set(CosmicSpellCharge.SideToSource(side));
        }

        public void Set(CastSource source) {
            item = item ?? GetComponent<Item>();
            if (!sources.Contains(source))
                sources.Add(source);
            if (sources.Count() > 1)
                return;
            startHeading = item.transform.forward;
            useGravity = item.rb.useGravity;
        }

        public void Reset(Side side) {
            Reset(CosmicSpellCharge.SideToSource(side));
        }

        public void Reset(CastSource source) {
            if (sources.Contains(source))
                sources.Remove(source);
            if (sources.Count() > 0)
                return;
            SetGravity(useGravity);
        }

        public void Rotate(Quaternion targetDiff) {
            item.rb.AddTorque(slowingPid.Update(-item.rb.angularVelocity, Time.deltaTime));
            var currentHeading = item.transform.forward;
            var targetHeading = targetDiff * startHeading;
            item.rb.AddTorque(-headingPid.Update(Vector3.Cross(currentHeading, targetHeading), Time.deltaTime));
        }

        public void SetGravity(bool gravity) {
            if (item.data.id.Equals("CosmicSun")) {
                item.rb.useGravity = false;
            } else {
                item.rb.useGravity = gravity;
            }
        }
    }
}
