using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using System.Reflection;
using System.Collections;

namespace BlackHoleSpell {
    public class BlackHoleCharge : SpellCastCharge {
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
            foreach (Item item in Item.list.Where(i => i != sourceItem && Vector3.Distance(contactPoint, i.transform.position) < imbueAttractRadius)) {
                collisionInstance.sourceColliderGroup.imbue.energy = 0.0f;
                Creature.player.mana.StartCoroutine(ImbueDaggerAttractCoroutine(item, contactPoint));
            }
        }

        IEnumerator ImbueDaggerAttractCoroutine(Item item, Vector3 target) {
            bool wasUsingGravity = item.rb.useGravity;
            item.rb.useGravity = false;
            item.rb.AddForce(Vector3.up * 0.5f, ForceMode.Impulse);
            yield return new WaitForSeconds(1f);
            item.rb.useGravity = wasUsingGravity;
            item.rb.AddForce((target - item.transform.position).normalized * 10.0f, ForceMode.Impulse);
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
                item => item?.imbues?
                    .Find(imbue => imbue?.spellCastBase?.id != "Gravity" || imbue?.energy == 0))?
                .Where(item => item.rb.useGravity != gravity);
            if (itemsToChange != null)
                //Debug.Log($"usegravity = {gravity} for {itemsToChange.Count()} items");
                foreach (var item in itemsToChange) {
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
                            && !item.isTelekinesisGrabbed).ToList();
                //Debug.Log($"{previousItems.Where(item => !items.Contains(item)).Count()} items in prev list");
                ToggleGravity(previousItems.Where(item => !items.Contains(item)), true);
                previousItems = new List<Item>(items);
                foreach (Item item in items) {
                    item.Throw();
                    if (isGripping) {
                        target = Vector3.Lerp(target, spellCaster.magicSource.position + spellCaster.magicSource.up, Time.deltaTime * 10.0f);
                        Vector3 forceDirection = target - item.transform.position;
                        item.rb.AddForce(forceDirection.normalized * 70.0f);
                        item.rb.AddForce(-forceDirection.normalized * (float)(2.0f / Math.Pow(forceDirection.magnitude, 2.0f)));
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
                item.rb.AddForce(velocity * 5.0f, ForceMode.Impulse);
                item.Throw();
            }
        }
    }
}
