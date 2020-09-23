using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using System.Reflection;
using System.Collections;

namespace CosmicSpell {
    public class CosmicSpellCharge : SpellCastCharge {
        private bool isActive;

        private float attractRadius = 10.0f;
        private float attractStrength = 10.0f;
        private float imbueAttractRadius = 5.0f;
        private GameObject blackHolePrefab;
        private Vector3 target;

        private List<Item> items;
        private List<Item> previousItems;

        public override void OnCatalogRefresh() {
            base.OnCatalogRefresh();
            blackHolePrefab = AssetBundle.GetAllLoadedAssetBundles()
                .Where(bundle => bundle.name.Contains("blackholespell"))
                .First()
                .LoadAsset<GameObject>("BlackHole.prefab");
            previousItems = new List<Item>();
        }

        public override void OnImbueCollisionStart(ref CollisionStruct collisionInstance) {
            base.OnImbueCollisionStart(ref collisionInstance);
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
                     && !i.isTelekinesisGrabbed)) {
                if (collisionInstance.sourceColliderGroup.imbue)
                    collisionInstance.sourceColliderGroup.imbue.energy = 0.0f;
                if (collisionInstance.targetColliderGroup?.collisionHandler != null && collisionInstance.targetColliderGroup.collisionHandler.isRagdollPart) {
                    Creature.player.mana.StartCoroutine(ImbueDaggerAttractCoroutine(item, collisionInstance.targetCollider.transform));
                } else {
                    Creature.player.mana.StartCoroutine(ImbueDaggerAttractCoroutine(item, contactPoint));
                }
            }
        }
        private void PointItemFlyRefAtTarget(Item item, Vector3 target, float lerpFactor) {
            item.transform.rotation = Quaternion.Slerp(
                item.transform.rotation * item.definition.flyDirRef.localRotation,
                Quaternion.LookRotation(target),
                lerpFactor) * Quaternion.Inverse(item.definition.flyDirRef.localRotation);
        }

        IEnumerator ImbueDaggerAttractCoroutine(Item item, Transform target) {
            bool wasUsingGravity = item.rb.useGravity;
            item.rb.useGravity = false;
            item.rb.AddForce(Vector3.up * 0.5f, ForceMode.Impulse);
            yield return new WaitForSeconds(1f);
            item.rb.useGravity = wasUsingGravity;
            item.rb.AddForce((target.transform.position - item.transform.position).normalized * 30.0f * item.rb.mass / 2.0f, ForceMode.Impulse);
            item.Throw();
        }

        IEnumerator ImbueDaggerAttractCoroutine(Item item, Vector3 target) {
            bool wasUsingGravity = item.rb.useGravity;
            item.rb.useGravity = false;
            item.rb.AddForce(Vector3.up * 0.5f, ForceMode.Impulse);
            yield return new WaitForSeconds(1f);
            item.rb.useGravity = wasUsingGravity;
            item.rb.AddForce((target - item.transform.position).normalized * 30.0f * item.rb.mass / 2.0f, ForceMode.Impulse);
            item.Throw();
        }

        public override void UpdateImbue() {
            base.UpdateImbue();
        }

        public override void Fire(bool active) {
            base.Fire(active);
            target = spellCaster.magicSource.position + spellCaster.magicSource.up;
            if (active) {
                if (!isActive) {
                    isActive = true;
                }
            } else {
                isActive = false;
                if (items.Count() > 0) {
                    ToggleGravity(items, true);
                    items.Clear();
                    previousItems.Clear();
                }
            }
        }

        private void ToggleGravity(IEnumerable<Item> list, bool gravity) {
            if (list == null || list.Count() == 0)
                return;
            var itemsToChange = list?.Where(
                item => item.imbues == null || item.imbues.Count() == 0 || item.imbues?
                    .Find(imbue => imbue?.spellCastBase?.id != "Gravity" || imbue?.energy == 0))?
                .Where(item => item.rb.useGravity != gravity);
            if (itemsToChange != null)
                foreach (var item in itemsToChange) {
                    if (!(item.data.id.Equals("CosmicSun") && gravity == true))
                        item.rb.useGravity = gravity;
                }
        }

        public override void UpdateCaster() {
            base.UpdateCaster();
            if (isActive) {
                bool isGripping = (spellCaster.bodyHand.side == Side.Left)
                                ? PlayerControl.handLeft.gripPressed
                                : PlayerControl.handRight.gripPressed;
                items = Item.list.Where(
                    item => Vector3.Distance(item.transform.position, spellCaster.magicSource.position) < attractRadius
                            && item.data.type != ItemPhysic.Type.Body
                            && !item.isGripped
                            && item.handlers.Count() == 0
                            && (item.lastHandler == null || !item.lastHandler.isHandlingSameObject)
                            && item.holder == null
                            && !item.rb.isKinematic
                            && !item.isTelekinesisGrabbed).ToList();
                ToggleGravity(previousItems.Where(item => !items.Contains(item)), true);
                previousItems = new List<Item>(items);
                foreach (Item item in items) {
                    item.Throw();
                    if (isGripping) {
                        target = Vector3.Lerp(target, spellCaster.magicSource.position + spellCaster.magicSource.up, Time.deltaTime * 10.0f);
                        Vector3 forceDirection = target - item.transform.position;
                        if (item.data.type == ItemPhysic.Type.Weapon
                            || item.data.type == ItemPhysic.Type.Shield) {
                            item.rb.AddForce(forceDirection.normalized * 70.0f);
                            item.rb.AddForce(-forceDirection.normalized * (float)(2.0f / Math.Pow(forceDirection.magnitude, 2.0f)));
                        } else {
                            item.rb.AddForce(forceDirection.normalized * item.rb.mass / 2.0f * 70.0f);
                            item.rb.AddForce(-forceDirection.normalized * item.rb.mass / 2.0f * (float)(2.0f / Math.Pow(forceDirection.magnitude, 2.0f)));
                        }
                        if (forceDirection.magnitude < 1.0f) {
                            item.rb.velocity *= 0.9f;
                        }
                        foreach (Item other in items) {
                            if (item == other)
                                continue;
                            var direction = item.transform.position - other.transform.position;
                            item.rb.AddForce(direction.normalized * (1 / direction.magnitude));
                        }
                    } else {
                        item.rb.useGravity = false;
                        item.rb.velocity *= 0.85f;
                    }
                }
            }
        }

        public override void Throw(Vector3 velocity) {
            base.Throw(velocity);
            isActive = false;
            ToggleGravity(items, true);
            foreach (Item item in items) {
                item.rb.velocity = Vector3.zero;
                if (item.data.type == ItemPhysic.Type.Weapon
                    || item.data.type == ItemPhysic.Type.Shield) {
                    item.rb.AddForce(velocity * 5.0f, ForceMode.Impulse);
                } else {
                    item.rb.AddForce(velocity * 5.0f * item.rb.mass / 2.0f, ForceMode.Impulse);
                }
                item.Throw();
            }
        }
    }
}
