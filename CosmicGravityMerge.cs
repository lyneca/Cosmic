using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;

namespace CosmicSpell {
    class CosmicGravityMerge : SpellMergeData {
        private bool effectShown;
        private GameObject textObject;
        private TextMesh text;
        private EffectData ringEffectData;
        private EffectInstance ringEffect;

        public float maxGravityForce = 2.0f;

        public override void OnCatalogRefresh() {
            base.OnCatalogRefresh();
            textObject = new GameObject();
            text = textObject.AddComponent<TextMesh>();
            ringEffectData = Catalog.GetData<EffectData>("SpellCosmicGravityPoint");
            textObject.SetActive(false);
            textObject.transform.localScale = Vector3.one * 0.01f;
            text.fontSize = 100;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            EventManager.onPlayerSpawned += (Player player) => Physics.gravity = new Vector3(0, -9.81f, 0);
        }

        private static Quaternion GetHandsPointingQuaternion() {
            var lookDirection = Quaternion.Slerp(
                Quaternion.LookRotation(Creature.player.animator.GetBoneTransform(HumanBodyBones.RightHand).transform.right * -1.0f),
                Quaternion.LookRotation(Creature.player.animator.GetBoneTransform(HumanBodyBones.LeftHand).transform.right * -1.0f),
                0.5f
            );
            if (PlayerControl.handLeft.gripPressed && PlayerControl.handRight.gripPressed) {
                var angleDown = Quaternion.Angle(lookDirection, Quaternion.LookRotation(Vector3.down));
                var angleUp = Quaternion.Angle(lookDirection, Quaternion.LookRotation(Vector3.up));
                if (angleUp < angleDown) {
                    return Quaternion.LookRotation(Vector3.up);
                } else {
                    return Quaternion.LookRotation(Vector3.down);
                }
            } else {
                return lookDirection;
            }
        }

        public static Quaternion FlattenQuaternion(Quaternion quat) {
            var transformed = quat * Vector3.forward;
            return Quaternion.LookRotation(transformed - Vector3.Dot(transformed, Vector3.up) * Vector3.up).normalized;

        }

        private float GetHandDistanceInGs() {
            if (PlayerControl.handLeft.gripPressed && PlayerControl.handRight.gripPressed) {
                return 1.0f;
            } else {
                return Mathf.Clamp(
                    Vector3.Distance(
                        Creature.player.body.handLeft.transform.position,
                        Creature.player.body.handRight.transform.position) - 0.3f,
                    0f, maxGravityForce / 2.0f) * 2.0f;
            }
        }

        public override void Merge(bool active) {
            base.Merge(active);
            if (active) {
                effectShown = true;
                textObject.transform.position = Creature.player.mana.mergePoint.transform.position + new Vector3(0, 0.2f, 0);
                ringEffect = ringEffectData.Spawn(Creature.player.mana.mergePoint.transform);
                ringEffect.Play();
            } else {
                effectShown = false;
                ringEffect.Stop();
                ringEffect.Despawn();
                textObject.SetActive(false);
                if (currentCharge == 1) {
                    Physics.gravity = GetHandsPointingQuaternion() * Vector3.forward * 9.8f * GetHandDistanceInGs();
                }
            }
        }

        public override void Update() {
            base.Update();
            if (effectShown) {
                textObject.SetActive(true);
                ringEffect.SetSpeed(GetHandDistanceInGs());
                ringEffect.SetIntensity(GetHandDistanceInGs());
                Creature.player.mana.mergePoint.transform.rotation = GetHandsPointingQuaternion();
                text.text = $"{Math.Round(GetHandDistanceInGs(), 1)}G";
                textObject.transform.position = Vector3.Lerp(
                    textObject.transform.position,
                    Creature.player.mana.mergePoint.transform.position + new Vector3(0, 0.2f, 0),
                    Time.deltaTime * 50.0f);
                textObject.transform.rotation = Quaternion.LookRotation(textObject.transform.position - Creature.player.animator.GetBoneTransform(HumanBodyBones.Head).position);
            } else {
                textObject.SetActive(false);
            }
        }
    }
}


