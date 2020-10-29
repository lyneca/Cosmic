using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using System.Collections;
using System.Reflection;

namespace CosmicSpell {
    class CosmicFireMerge : SpellMergeData {
        private ItemPhysic sunData;
        private Item sunInstance;
        private bool isActive;
        public ItemPhysic fireballItem;
        private EffectData fireballEffect;
        private EffectData fireballDeflectEffect;
        private DamagerData fireballDamager;

        public int sunNumFireballs = 10;
        public float sunFireballDelay = 1.0f;
        public float throwVelocity = 5.0f;
        public float fireballSpeed = 50.0f;
        public float fireballChargeSpeed = 2.0f;

        public override void OnCatalogRefresh() {
            base.OnCatalogRefresh();
            AssetBundle assetBundle = AssetBundle.GetAllLoadedAssetBundles().Where(bundle => bundle.name.Contains("blackholespell")).First();
            sunData = Catalog.GetData<ItemPhysic>("CosmicSun");
            fireballItem = Catalog.GetData<ItemPhysic>("DynamicProjectile");
            fireballEffect = Catalog.GetData<EffectData>("SpellFireball");
            fireballDeflectEffect = Catalog.GetData<EffectData>("SpellFireBallHitBlade");
            fireballDamager = Catalog.GetData<DamagerData>("Fireball");
        }

        public override void Merge(bool active) {
            base.Merge(active);
            if (active) {
                if (!isActive) {
                    isActive = true;
                    sunInstance = sunData.Spawn();
                    sunInstance.transform.position = Creature.player.mana.mergePoint.transform.position;
                    sunInstance.transform.rotation = Creature.player.mana.mergePoint.transform.rotation;
                    sunInstance.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    sunInstance.rb.isKinematic = true;
                    SunController control = sunInstance.GetComponent<SunController>();
                    control.mergeSpell = this;
                }
            } else {
                isActive = false;
                Vector3 velocity = Player.local.transform.rotation * PlayerControl.GetHand(GameManager.options.twoHandedDominantHand).GetHandVelocity();
                if (currentCharge == 1) {
                    Throw(velocity);
                } else if (sunInstance != null) {
                    sunInstance.GetComponent<SunController>().Despawn();
                    sunInstance = null;
                }
            }
        }

        public void Throw(Vector3 velocity) {
            SunController control = sunInstance.GetComponent<SunController>();
            if (control != null) {
                control.active = true;
                control.numFireballs = sunNumFireballs;
                control.fireballDelay = sunFireballDelay;
            }
            sunInstance.rb.isKinematic = false;
            sunInstance.rb.useGravity = true;
            sunInstance.rb.AddForce(velocity * throwVelocity, ForceMode.Impulse);
            sunInstance.Throw();
        }

        public static Vector3 GetClosestCreatureHead(Item item) {
            var nearestCreatures = Creature.list
                    .Where(x => !x.faction.name.Equals("Player") && x.state != Creature.State.Dead);
            if (nearestCreatures.Count() == 0) {
                return item.transform.position + item.transform.position - Creature.player.animator.GetBoneTransform(HumanBodyBones.Chest).transform.position;
            } else {
                return nearestCreatures
                    .Aggregate((a, x) => Vector3.Distance(a.transform.position, item.transform.position) < Vector3.Distance(x.transform.position, item.transform.position) ? a : x)
                    .animator.GetBoneTransform(HumanBodyBones.Head).position;
            }
        }

        public override void Update() {
            base.Update();
            Transform mergePoint = Creature.player.mana.mergePoint.transform;
            if (isActive) {
                sunInstance.transform.position = Vector3.Lerp(
                    sunInstance.transform.position,
                    mergePoint.position,
                    Time.deltaTime * 10.0f);
                sunInstance.transform.rotation *= Quaternion.Euler(0, 1.0f, 0);
                sunInstance.transform.localScale = Vector3.one * currentCharge / 3.0f;
            }
        }

        public void ThrowFireball(Item fireball, Vector3 velocity) {
            fireball.rb.isKinematic = false;

            // The following is lightly modified from the game's own fireball spawn functionality. All credit to KospY.
            foreach (CollisionHandler collisionHandler in fireball.definition.collisionHandlers) {
                collisionHandler.SetPhysicModifier(this, 0, 0.0f);
                foreach (Damager damager in collisionHandler.damagers)
                    damager.Load(fireballDamager);
            }
            ItemMagicProjectile component = fireball.GetComponent<ItemMagicProjectile>();
            if ((bool)(UnityEngine.Object)component) {
                component.guided = false;
                component.speed = fireballSpeed;
                component.item.lastHandler = Creature.player.body.handRight.interactor;
                component.allowDeflect = true;
                component.deflectEffectData = fireballDeflectEffect;
                component.imbueBladeEnergyTransfered = 50.0f;
                component.imbueSpellCastCharge = (SpellCastCharge)Creature.player.mana.spells.Find(spell => spell.id == "Fire");
                component.Fire(velocity, fireballEffect, Creature.player.body.handRight.playerHand?.itemHand?.item, Creature.player.ragdoll);
            } else {
                fireball.rb.AddForce(velocity, ForceMode.Impulse);
                fireball.Throw(flyDetection: Item.FlyDetection.Forced);
            }
        }

        public void ThrowFireballAtClosestEnemy(Item fireball) {
            ThrowFireball(fireball, (GetClosestCreatureHead(fireball) - fireball.transform.position).normalized * 30.0f);
        }

        public IEnumerator SpawnFireball(Transform sun, Vector3 velocity) {
            var offset = Quaternion.Euler(
                UnityEngine.Random.value * 360.0f,
                UnityEngine.Random.value * 360.0f,
                UnityEngine.Random.value * 360.0f) * Vector3.forward * 0.2f;
            Item fireball = fireballItem.Spawn();
            fireball.transform.position = sun.position + offset;
            fireball.transform.localScale = Vector3.zero;
            fireball.rb.isKinematic = true;
            ThrowFireball(fireball, velocity);
            yield break;
        }

        public IEnumerator SpawnFireball(Transform sun, Collider[] ignoredColliders) {
            // does it show that I didn't google how to do C# default args...
            float chargeTime = 0;
            var offset = Quaternion.Euler(
                UnityEngine.Random.value * 360.0f,
                UnityEngine.Random.value * 360.0f,
                UnityEngine.Random.value * 360.0f) * Vector3.forward * 0.2f;
            Item fireball = fireballItem.Spawn();
            fireball.transform.position = sun.position + offset;
            fireball.transform.localScale = Vector3.zero;
            fireball.rb.isKinematic = true;
            foreach (Collider collider in ignoredColliders) {
                foreach (Collider fireballCollider in fireball.GetComponentsInChildren<Collider>()) {
                    Physics.IgnoreCollision(collider, fireballCollider);
                }
            }
            while (true) {
                if (chargeTime < 1 && sun != null) {
                    chargeTime += Time.deltaTime / fireballChargeSpeed;
                    fireball.transform.localScale = Vector3.one * chargeTime;
                    fireball.transform.position = Vector3.Lerp(
                        fireball.transform.position,
                        sun.position + offset,
                        Time.deltaTime * 10.0f);
                    yield return new WaitForFixedUpdate();
                } else {
                    ThrowFireballAtClosestEnemy(fireball);
                    yield break;
                }
            }
        }
    }
}
