using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;

namespace CosmicSpell {
    class CosmicLightningMerge : SpellMergeData {
        bool active;
        float lastZapTime;
        float zapDelay = 1.0f;
        float pushRadius = 10.0f;
        EffectData imbueRagdollEffect;

        public override void OnCatalogRefresh() {
            base.OnCatalogRefresh();
            imbueRagdollEffect = Catalog.GetData<EffectData>("ImbueLightningRagdoll");
        }


        public override void Merge(bool active) {
            base.Merge(active);
            if (active) {
                if (!this.active)
                    this.active = true;
            } else {
                this.active = false;
            }
        }
        public override void Update() {
            base.Update();
            if (active) {
                if (Time.time - lastZapTime < zapDelay)
                    return;
                lastZapTime = Time.time;

                SpellCastData lightningData = Creature.player.mana.casterLeft.spellInstance is SpellCastLightning
                    ? Creature.player.mana.casterLeft.spellInstance
                    : Creature.player.mana.casterRight.spellInstance;
                if (lightningData is SpellCastLightning cast) {
                    // modified from ThunderRoad source code for Gravity crystal slam
                    List<Rigidbody> pushedBodies = new List<Rigidbody>();
                    List<Creature> pushedCreatures = new List<Creature>();
                    foreach (Collider collider in Physics.OverlapSphere(Creature.player.mana.mergePoint.position, pushRadius)
                        .Where(c =>
                            c.attachedRigidbody
                            && c.attachedRigidbody
                            && !c.attachedRigidbody.isKinematic)) {
                        if (collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.NPC) && collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.BodyLocomotion) && !pushedBodies.Contains(collider.attachedRigidbody)) {
                            RagdollPart component = collider.attachedRigidbody.gameObject.GetComponent<RagdollPart>();
                            if (component?.ragdoll.creature == Creature.player)
                                continue;
                            collider.attachedRigidbody.AddForce((collider.transform.position - Creature.player.mana.mergePoint.position).normalized * 80.0f, ForceMode.Impulse);
                            pushedBodies.Add(collider.attachedRigidbody);
                        }
                        if (collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.NPC) || collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.Ragdoll)) {
                            RagdollPart component = collider.attachedRigidbody.gameObject.GetComponent<RagdollPart>();
                            if (component.ragdoll.creature == Creature.player)
                                continue;
                            if (component && !pushedCreatures.Contains(component.ragdoll.creature)) {
                                component.ragdoll.creature.brain.TryPush((component.ragdoll.hipsPart.transform.position - Creature.player.mana.mergePoint.position).normalized, component.ragdoll.creature.brain.gravityPushBehaviorPerLevel[1]);
                                collider.attachedRigidbody.AddForce((collider.transform.position - Creature.player.mana.mergePoint.position).normalized * 40.0f, ForceMode.Impulse);
                                pushedCreatures.Add(component.ragdoll.creature);
                                ActionShock action = component.ragdoll.creature.GetAction<ActionShock>();
                                if (action != null) {
                                    action.Refresh(0.5f, cast.boltShockDuration);
                                } else {
                                    ActionShock actionShock = new ActionShock(0.5f, cast.boltShockDuration, imbueRagdollEffect);
                                    //creature.TryAction(actionShock, true);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
