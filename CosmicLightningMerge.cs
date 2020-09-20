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
                    foreach (Creature creature in Creature.list.Where(c => c && !c.Equals(Creature.player))) {
                        ActionShock action = creature.GetAction<ActionShock>();
                        if (action != null) {
                            action.Refresh(0.5f, cast.boltShockDuration);
                        } else {
                            ActionShock actionShock = new ActionShock(0.5f, cast.boltShockDuration, this.imbueRagdollEffect);
                            creature.TryAction(actionShock, true);
                        }
                    }
                }
            }
        }
    }
}
