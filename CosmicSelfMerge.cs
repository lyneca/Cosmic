using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using HarmonyLib;


namespace CosmicSpell {
    using Utils.ExtensionMethods;

    using static CosmicSpell.HoleType;

    class CosmicSelfMerge : SpellMergeData {
        public float itemSpamDelay = 1.0f;

        public float friction = 0.95f;
        public float throwTrackRange = 10.0f;
        public float throwTrackForce = 60.0f;
        public float throwForce = 30.0f;

        public float pushForce = 30.0f;
        public float pullForce = 40.0f;

        ItemPhysic blackHoleData;
        ItemPhysic whiteHoleData;

        Item blackHoleInstance;
        Item whiteHoleInstance;

        bool isActive;
        bool whiteGrabbed;
        bool blackGrabbed;

        public static void PointItemFlyRefAtTarget(Item item, Vector3 target, float lerpFactor) {
            if (item == null || item?.flyDirRef == null)
                return;
            item.transform.rotation = Quaternion.Slerp(
                item.transform.rotation * item.flyDirRef.localRotation,
                Quaternion.LookRotation(target),
                lerpFactor) * Quaternion.Inverse(item.flyDirRef.localRotation);
        }


        public static bool ShouldAffectRigidbody(Rigidbody rb) {
            if (rb == null)
                return false;
            try {
                bool rigidbodyConditions = (rb.gameObject.layer != GameManager.GetLayer(LayerName.NPC))
                    && !rb.isKinematic;
                Item item = rb.gameObject.GetComponent<Item>();
                bool itemConditions = item != null && (!item.isTelekinesisGrabbed
                    && item.handlers.Count == 0
                    && !item.isGripped
                    && item.itemId != "BlackHoleSphere"
                    && item.itemId != "WhiteHoleSphere"
                );
                RagdollPart ragdoll = rb.gameObject.GetComponent<RagdollPart>();
                bool ragdollConditions = ragdoll == null;
                return rigidbodyConditions && itemConditions && ragdollConditions;
            } catch (NullReferenceException e) {
                Debug.LogError(e.Data);
                return false;
            }
        }


        public override void Load(Mana mana) {
            base.Load(mana);
            blackHoleData = Catalog.GetData<ItemPhysic>("CosmicBlackHole");
            whiteHoleData = Catalog.GetData<ItemPhysic>("CosmicWhiteHole");
        }

        public override void Unload() {
            base.Unload();
        }

        public Item GetOrFindHole(HoleType type) {
            if (type == Black) {
                return blackHoleInstance ?? Item.list.Find(item => item.itemId == "BlackHoleSphere");
            } else {
                return whiteHoleInstance ?? Item.list.Find(item => item.itemId == "WhiteHoleSphere");
            }
        }
        //instance.gameObject.GetOrAddComponent<HoleSphereBehaviour>().Detach();

        public override void Merge(bool active) {
            base.Merge(active);
            if (active) {
                if (!isActive) {
                    isActive = true;

                    blackGrabbed = false;
                    whiteGrabbed = false;

                    GetOrFindHole(Black)?.GetComponent<HoleSphereBehaviour>()?.Despawn();
                    GetOrFindHole(White)?.GetComponent<HoleSphereBehaviour>()?.Despawn();

                    whiteHoleData.SpawnAsync(instance => {
                        whiteHoleInstance = instance;
                        whiteHoleInstance.disallowDespawn = true;
                        whiteHoleInstance.transform.localScale = Vector3.one * 0.3f;
                        whiteHoleInstance.transform.position = Player.currentCreature.mana.mergePoint.position;
                        whiteHoleInstance.gameObject.GetOrAddComponent<HoleSphereBehaviour>().Begin(White, ref blackHoleInstance, this);
                    });

                    blackHoleData.SpawnAsync(instance => {
                        blackHoleInstance = instance;
                        blackHoleInstance.disallowDespawn = true;
                        blackHoleInstance.transform.localScale = Vector3.one * 0.3f;
                        blackHoleInstance.transform.position = Player.currentCreature.mana.mergePoint.position;
                        blackHoleInstance.gameObject.GetOrAddComponent<HoleSphereBehaviour>().Begin(Black, ref whiteHoleInstance, this);
                    });
                }
            } else {
                isActive = false;
                blackGrabbed = false;
                whiteGrabbed = false;
                if (currentCharge < 1) {
                    blackHoleInstance.GetComponent<HoleSphereBehaviour>().Despawn();
                    blackHoleInstance = null;
                    whiteHoleInstance.GetComponent<HoleSphereBehaviour>().Despawn();
                    whiteHoleInstance = null;
                } else {
                    whiteHoleInstance.GetComponent<HoleSphereBehaviour>().active = true;
                    blackHoleInstance.GetComponent<HoleSphereBehaviour>().active = true;
                }
            }
        }

        public void GrabItem(SpellCaster caster, Item item) {
            caster?.telekinesis?.StartTargeting(item.GetMainHandle(caster.ragdollHand.side));
            caster?.telekinesis?.TryCatch();
        }

        public override void Update() {
            base.Update();
            if (isActive && whiteHoleInstance && blackHoleInstance) {
                if (PlayerControl.handLeft.gripPressed && !whiteGrabbed && currentCharge == 1) {
                    whiteGrabbed = true;
                    GrabItem(Player.currentCreature.mana.casterLeft, whiteHoleInstance);
                    whiteHoleInstance.GetComponent<HoleSphereBehaviour>().active = true;
                }
                if (PlayerControl.handRight.gripPressed && !blackGrabbed && currentCharge == 1) {
                    blackGrabbed = true;
                    GrabItem(Player.currentCreature.mana.casterRight, blackHoleInstance);
                    blackHoleInstance.GetComponent<HoleSphereBehaviour>().active = true;
                }
                if (!blackGrabbed)
                    blackHoleInstance.transform.position = Vector3.Lerp(
                        blackHoleInstance.transform.position,
                        Player.currentCreature.mana.mergePoint.position + (whiteGrabbed ? new Vector3(0, 0, 0) : new Vector3(0, 0.3f, 0)),
                        Time.deltaTime * 10.0f);
                if (!whiteGrabbed)
                    whiteHoleInstance.transform.position = Vector3.Lerp(
                        whiteHoleInstance.transform.position,
                        Player.currentCreature.mana.mergePoint.position + (blackGrabbed ? new Vector3(0, 0, 0) : new Vector3(0, -0.3f, 0)),
                        Time.deltaTime * 10.0f);
                whiteHoleInstance.transform.localScale = Vector3.one * currentCharge * 0.3f;
                blackHoleInstance.transform.localScale = Vector3.one * currentCharge * 0.3f;
            }
        }
    }

    enum HoleType {
        White,
        Black
    }

    class HoleSphereBehaviour : MonoBehaviour {
        private Item item;
        public bool active;
        private bool isHeld;
        private bool isStored;
        private bool hasFired;

        private bool wasHeld;
        private float lastHeldTime;
        private float itemSpamDelay = 1.0f;
        private float lastThrownItem = 0.0f;
        private float itemThrowDelay = 0.1f;
        private float friction = 0.95f;
        private float throwTrackRange = 10.0f;
        private float throwTrackForce = 100.0f;
        // private float throwTrackForce = 60.0f;
        private float throwForce = 30.0f;

        private float pushForce = 30.0f;
        private float pullForce = 40.0f;

        EffectData nomEffect;
        EffectData pewEffect;
        EffectData ambientEffect;
        EffectInstance effect;
        HoleType type;
        Item otherHole;
        Queue<GameObject> storedItems = new Queue<GameObject>();

        public void Begin(HoleType type, ref Item otherHole, CosmicSelfMerge spellData) {
            nomEffect = nomEffect ?? Catalog.GetData<EffectData>("HoleAbsorb");
            pewEffect = pewEffect ?? Catalog.GetData<EffectData>("HoleFire");
            ambientEffect = ambientEffect ?? Catalog.GetData<EffectData>((type == Black) ? "BlackHoleAmbient" : "WhiteHoleAmbient");

            itemSpamDelay = spellData.itemSpamDelay;
            friction = spellData.friction;
            throwTrackRange = spellData.throwTrackRange;
            throwTrackForce = spellData.throwTrackForce;
            throwForce = spellData.throwForce;
            pushForce = spellData.pushForce;
            pullForce = spellData.pullForce;

            if (active)
                return;
            this.otherHole = otherHole;
            item = GetComponent<Item>();
            effect = ambientEffect.Spawn(transform);
            effect.Play();
            this.type = type;
        }

        public void Detach() {
            item?.holder?.UnSnap(item, true);
            item?.GetMainHandle(Side.Left).handlers.ForEach(handler => handler.TryRelease());
            item?.GetMainHandle(Side.Right).handlers.ForEach(handler => handler.TryRelease());
            item?.handlers.ForEach(handler => handler.TryRelease());
        }

        public void DestroyOldHoles() {
            foreach (Item otherItem in new List<Item>(Item.list).Where(
                i => i.itemId == "BlackHoleSphere"
                  || i.itemId == "WhiteHoleSphere").ToList()) {
                if (otherItem && otherHole && !ReferenceEquals(otherItem.gameObject, otherHole.gameObject) && !ReferenceEquals(otherItem.gameObject, gameObject)) {
                    otherItem.holder?.UnSnap(otherItem, true);
                    otherItem.GetMainHandle(Side.Left).handlers.ToList().ForEach(handler => handler.TryRelease());
                    otherItem.GetMainHandle(Side.Right).handlers.ToList().ForEach(handler => handler.TryRelease());
                    otherItem.handlers.ToList().ForEach(handler => handler.TryRelease());
                    otherItem.Despawn();
                }
            }
        }

        public void Update() {
            isHeld = item.mainHandler != null;
            isStored = item.holder != null;

            item.rb.useGravity = false;
            item.rb.velocity *= friction;

            if (Item.list.Where(
                i => i.itemId == "BlackHoleSphere"
                  || i.itemId == "WhiteHoleSphere").Count() > 2)
                DestroyOldHoles();

            if (!active)
                return;

            if (isHeld) {
                wasHeld = true;
                float axis = PlayerControl.GetHand(item.mainHandler.side).useAxis;
                effect.SetIntensity(axis);
                effect.SetSpeed(axis);
                if (type == White) {
                    if (axis > 0) {
                        PushOrSucc(axis);
                        if (!hasFired) {
                            hasFired = true;
                            FireObject();
                        }
                    } else {
                        hasFired = false;
                    }
                } else {
                    if (axis > 0) {
                        PushOrSucc(axis);
                    }
                }
            } else if (isStored) {
                wasHeld = true;
                effect.SetIntensity(0);
                effect.SetSpeed(0);
                hasFired = false;
            } else {
                if (wasHeld) {
                    wasHeld = false;
                    lastHeldTime = Time.time;
                }
                effect.SetIntensity(1);
                effect.SetSpeed(1);
                PushOrSucc();
                hasFired = false;
            }
            if (!isStored && !isHeld && storedItems.Count > 0 && type == White && Time.time - lastHeldTime > itemSpamDelay && Time.time - lastThrownItem > itemThrowDelay) {
                lastThrownItem = Time.time;
                GameObject storedObject = storedItems.Dequeue();
                storedObject.SetActive(true);
                TriggerHeadThrow(storedObject.GetComponent<Rigidbody>());
            }
        }

        public void PushOrSucc() { PushOrSucc(1); }

        public void PushOrSucc(float amount) {
            if (!otherHole)
                return;
            var otherComponent = otherHole.GetComponent<HoleSphereBehaviour>();
            lock (otherComponent.storedItems) {
                foreach (Collider collider in Physics.OverlapSphere(item.transform.position, 0.1f, 218119169)) {
                    if (type == Black && otherHole != null && collider && CosmicSelfMerge.ShouldAffectRigidbody(collider.attachedRigidbody)) {
                        collider.attachedRigidbody.gameObject.GetComponent<Item>()?.handlers.ForEach(handler => handler.TryRelease());
                        otherComponent.storedItems.Enqueue(collider.attachedRigidbody.gameObject);
                        nomEffect.Spawn(item.transform).Play();
                        collider.attachedRigidbody.gameObject.SetActive(false);
                    }
                }
            }
            foreach (Collider collider in Physics.OverlapSphere(item.transform.position, 10.0f, 218119169)) {
                if (CosmicSelfMerge.ShouldAffectRigidbody(collider.attachedRigidbody))
                    AddForce(collider.attachedRigidbody, amount);
            }
        }

        public void TriggerHeadThrow(Rigidbody rb) {
            rb.gameObject.transform.position = item.transform.position;
            List<Creature> creatures = Creature.list.Where(
                creature => creature != Player.currentCreature
                         && creature.state != Creature.State.Dead
                         && (creature.ragdoll.headPart.transform.position - item.transform.position).magnitude < throwTrackRange).ToList();
            if (creatures.Count > 0) {
                Creature target = creatures[new System.Random().Next(creatures.Count)];
                Debug.Log($"Throwing at {target.name}");
                rb.position = (target.ragdoll.headPart.transform.position - item.transform.position).normalized * 0.2f;
                rb.velocity = Vector3.zero;
                float modifier = 1;
                if (rb.mass < 1) {
                    modifier *= rb.mass * 2;
                } else {
                    modifier *= rb.mass;
                }
                rb.AddForce((target.ragdoll.headPart.transform.position - item.transform.position).normalized * modifier * throwTrackForce, ForceMode.Impulse);
                if (rb.gameObject.GetComponent<Item>().data.type != ItemPhysic.Type.Prop)
                    CosmicSelfMerge.PointItemFlyRefAtTarget(rb.gameObject.GetComponent<Item>(), (target.ragdoll.headPart.transform.position - item.transform.position).normalized, 1);
            }
            pewEffect.Spawn(item.transform).Play();
            rb.gameObject.GetComponent<Item>()?.Throw();
        }

        public void FireObject() {
            if (!isHeld)
                return;
            if (storedItems.Count > 0) {
                GameObject storedObject = storedItems.Dequeue();
                try {
                    storedObject.transform.position = item.transform.position
                        + item.mainHandler.transform.right * -0.3f;
                    storedObject.SetActive(true);
                    if (storedObject.GetComponent<Rigidbody>()) {
                        var rigidbody = storedObject.GetComponent<Rigidbody>();
                        rigidbody.velocity = Vector3.zero;
                        rigidbody.AddForce(
                            (storedObject.transform.position - item.mainHandler.transform.position).normalized
                            * rigidbody.mass
                            * throwForce, ForceMode.Impulse);
                    }
                    pewEffect.Spawn(item.transform).Play();
                    storedObject.GetComponent<Item>()?.Throw();
                } catch { }
            }
        }

        public void AddForce(Rigidbody rb) { AddForce(rb, 1); }

        public void AddForce(Rigidbody rb, float amount) {
            switch (type) {
                case White:
                    AddPushForce(rb, amount);
                    break;
                case Black:
                    AddPullForce(rb, amount);
                    break;
            }
        }

        public void AddPushForce(Rigidbody rb, float amount) {
            var diff = rb.gameObject.transform.position - item.transform.position;
            rb.AddForce(diff.normalized * rb.mass * pushForce / Math.Max(diff.magnitude, 1.0f) * amount);
        }

        public void AddPullForce(Rigidbody rb, float amount) {
            var diff = item.transform.position - rb.gameObject.transform.position;
            rb.AddForce(diff.normalized * rb.mass * pullForce / Math.Max(diff.magnitude, 1.0f) * amount);
        }

        public void Despawn() {
            item.Despawn();
        }
    }
}


