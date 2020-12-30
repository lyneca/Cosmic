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
    using Utils.ExtensionMethods;
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
        public float fireballDelay = 2.0f;

        public override void Load(Mana mana) {
            base.Load(mana);
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
                    sunData.SpawnAsync(sun => {
                        sunInstance = sun;
                        sunInstance.transform.position = Player.currentCreature.mana.mergePoint.transform.position;
                        sunInstance.transform.rotation = Player.currentCreature.mana.mergePoint.transform.rotation;
                        sunInstance.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        sunInstance.rb.isKinematic = true;
                        SunController control = sunInstance.GetComponent<SunController>();
                        control.mergeSpell = this;
                    });
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
                return item.transform.position + item.transform.position - Player.currentCreature.animator.GetBoneTransform(HumanBodyBones.Chest).transform.position;
            } else {
                return nearestCreatures
                    .Aggregate((a, x) => Vector3.Distance(a.transform.position, item.transform.position) < Vector3.Distance(x.transform.position, item.transform.position) ? a : x)
                    .animator.GetBoneTransform(HumanBodyBones.Head).position;
            }
        }

        public override void Update() {
            base.Update();
            Transform mergePoint = Player.currentCreature.mana.mergePoint.transform;
            if (isActive && sunInstance != null) {
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
            foreach (CollisionHandler collisionHandler in fireball.collisionHandlers) {
                collisionHandler.SetPhysicModifier(this, 0, 0.0f);
                foreach (Damager damager in collisionHandler.damagers)
                    damager.Load(fireballDamager, collisionHandler);
            }
            ItemMagicProjectile component = fireball.GetComponent<ItemMagicProjectile>();
            if ((bool)(UnityEngine.Object)component) {
                component.guided = false;
                component.speed = fireballSpeed;
                component.item.lastHandler = Player.currentCreature.handRight;
                component.allowDeflect = true;
                component.deflectEffectData = fireballDeflectEffect;
                component.imbueBladeEnergyTransfered = 50.0f;
                component.imbueSpellCastCharge = (SpellCastCharge)Player.currentCreature.mana.spells.Find(spell => spell.id == "Fire");
                component.Fire(velocity, fireballEffect, shooterRagdoll: Player.currentCreature.ragdoll);
            } else {
                fireball.rb.AddForce(velocity, ForceMode.Impulse);
                fireball.Throw(flyDetection: Item.FlyDetection.Forced);
            }
        }

        public void ThrowFireballAtClosestEnemy(Item fireball) {
            ThrowFireball(fireball, (GetClosestCreatureHead(fireball) - fireball.transform.position).normalized * 30.0f);
        }

        public void SpawnFireball(Transform sun, Vector3 velocity) {
            var offset = Quaternion.Euler(
                UnityEngine.Random.value * 360.0f,
                UnityEngine.Random.value * 360.0f,
                UnityEngine.Random.value * 360.0f) * Vector3.forward * 0.2f;
            fireballItem.SpawnAsync(fireball => {
                fireball.transform.position = sun.position + offset;
                fireball.transform.localScale = Vector3.zero;
                fireball.rb.isKinematic = true;
                ThrowFireball(fireball, velocity);
            });
        }

        public void SpawnFireball(Transform sun, Collider[] ignoredColliders) {
            // does it show that I didn't google how to do C# default args...
            float chargeTime = 0;
            var offset = Quaternion.Euler(
                UnityEngine.Random.value * 360.0f,
                UnityEngine.Random.value * 360.0f,
                UnityEngine.Random.value * 360.0f) * Vector3.forward * 0.2f;
            fireballItem.SpawnAsync(fireball => {
                fireball.transform.position = sun.position + offset;
                fireball.transform.localScale = Vector3.one;
                fireball.rb.isKinematic = true;
                foreach (Collider collider in ignoredColliders) {
                    foreach (Collider fireballCollider in fireball.GetComponentsInChildren<Collider>()) {
                        Physics.IgnoreCollision(collider, fireballCollider);
                    }
                }
                Task.Delay((int)fireballDelay).Wait();
                ThrowFireballAtClosestEnemy(fireball);
            });
        }
    }
}
