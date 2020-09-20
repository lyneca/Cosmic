using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace CosmicSpell {
    class SunControllerModule : ItemModule {
        public override void OnItemLoaded(Item item) {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<SunController>();
        }
    }

    class SunController : MonoBehaviour {
        protected Item item;
        public CosmicFireMerge mergeSpell;
        public int numFireballs = 10;
        private float lastFireballFired = 0;
        private float fireballDelay = 1.0f;
        public bool awake = false;
        public bool active = false;

        protected void Awake() {
            awake = true;
            item = GetComponent<Item>();
        }

        protected void Start() => item.definition.collisionHandlers[0].OnCollisionStartEvent += new CollisionHandler.CollisionEvent(this.OnCollision);

        protected void OnCollision(ref CollisionStruct collisionInstance) {
            if (active) {
                var colliderId = collisionInstance.targetColliderGroup?.collisionHandler?.item?.definition?.itemId;
                var colliderCreature = collisionInstance.targetColliderGroup?.collisionHandler?.ragdollPart?.ragdoll?.creature;
                if (colliderId != null && colliderId.Equals("DynamicProjectile")
                    || colliderCreature != null && colliderCreature.Equals(Creature.player))
                    return;
                if (mergeSpell == null)
                    return;
                for (int i = 0; i < numFireballs; i++) {
                    Item fireball = mergeSpell.fireballItem.Spawn();
                    fireball.transform.position = collisionInstance.contactPoint + collisionInstance.contactNormal / 2.0f + new Vector3(
                        UnityEngine.Random.Range(-0.5f, 0.5f),
                        UnityEngine.Random.Range(-0.5f, 0.5f),
                        UnityEngine.Random.Range(-0.5f, 0.5f));
                    fireball.rb.useGravity = true;
                    mergeSpell.ThrowFireball(fireball, Quaternion.Euler(
                        UnityEngine.Random.Range(-20.0f, 20.0f),
                        UnityEngine.Random.Range(-20.0f, 20.0f),
                        UnityEngine.Random.Range(-20.0f, 20.0f)) * collisionInstance.contactNormal * 3.0f);
                }
                Despawn();
            }
        }

        protected void Update() {
            if (awake && item != null && Time.time - lastFireballFired > fireballDelay) {
                lastFireballFired = Time.time;
                Creature.player.StartCoroutine(mergeSpell.SpawnFireball(transform, item.GetComponentsInChildren<Collider>()));
            }
        }

        public void Despawn() {
            this.item.rb.isKinematic = false;
            foreach (ColliderGroup colliderGroup in this.item.definition.colliderGroups) {
                foreach (Collider collider in colliderGroup.colliders)
                    collider.enabled = true;
            }
            this.CancelInvoke();
            this.item.Despawn();
        }
    }
}
