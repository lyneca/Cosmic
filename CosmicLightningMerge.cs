﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;

namespace CosmicSpell {
    class CosmicLightningMerge : SpellMergeData {
        public float radius = 10.0f;
        EffectData imbueRagdollEffect;

        public override void Load(Mana mana) {
            base.Load(mana);
            imbueRagdollEffect = Catalog.GetData<EffectData>("ImbueLightningRagdoll");
        }


        public override void Merge(bool active) {
            base.Merge(active);
            if (active) {
            } else {
                if (currentCharge == 1) {
                    DisarmEveryoneLmao();
                }
            }
        }

        public override void Update() {
            base.Update();
            if (currentCharge > 0) {
                chargeEffect?.SetSource(mana.mergePoint);
            }
        }

        public void DisarmEveryoneLmao() {
            SpellCastData lightningData = Player.currentCreature.mana.casterLeft.spellInstance is SpellCastLightning
                ? Player.currentCreature.mana.casterLeft.spellInstance
                : Player.currentCreature.mana.casterRight.spellInstance;
            if (lightningData is SpellCastLightning cast) {
                // modified from ThunderRoad source code for Gravity crystal slam
                List<Creature> pushedCreatures = new List<Creature>();
                foreach (Collider collider in Physics.OverlapSphere(Player.currentCreature.mana.mergePoint.position, radius)
                    .Where(c =>
                        c.attachedRigidbody
                        && c.attachedRigidbody
                        && !c.attachedRigidbody.isKinematic)) {
                    if (collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.NPC) || collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.Ragdoll)) {
                        RagdollPart component = collider.attachedRigidbody.gameObject.GetComponent<RagdollPart>();
                        if (component.ragdoll.creature == Player.currentCreature)
                            continue;
                        if (component && !pushedCreatures.Contains(component.ragdoll.creature)) {
                            // do it again, just in case lmao
                            pushedCreatures.Add(component.ragdoll.creature);
                            component.ragdoll.creature.handLeft.TryRelease();
                            component.ragdoll.creature.handRight.TryRelease();
                            ActionShock action = component.ragdoll.creature.brain.GetAction<ActionShock>();
                            if (action != null) {
                                action.Refresh(0.5f, cast.boltShockDuration);
                            } else {
                                ActionShock actionShock = new ActionShock(0.5f, cast.boltShockDuration, imbueRagdollEffect);
                                component.ragdoll.creature.brain.TryAction(actionShock, true);
                            }
                        }
                    }
                }
            }
        }
    }
}
