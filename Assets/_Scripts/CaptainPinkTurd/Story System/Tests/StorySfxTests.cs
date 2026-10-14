#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Game.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CaptainPinkTurd.Story.Tests
{
    public class StorySfxTests
    {
        [Test]
        public void StoryAndBossCuesHaveResolvedClipsAndUseTheSettingsMixer()
        {
            var catalog = Resources.Load<StorySfxCatalog>("Story SFX");
            Assert.IsNotNull(catalog, "The resource catalog must also ship in Android builds.");
            Assert.IsNotNull(catalog.mixerGroup);
            Assert.AreEqual("SoundFX", catalog.mixerGroup.name);
            var names = new HashSet<string>();
            foreach (var cue in catalog.cues)
            {
                Assert.IsTrue(names.Add(cue.id), "duplicate sound cue " + cue.id);
                Assert.That(cue.volume, Is.InRange(.01f, 1f));
                Assert.IsNotEmpty(cue.clips);
                foreach (var clip in cue.clips)
                {
                    Assert.IsNotNull(clip, cue.id + " has a missing audio GUID");
                    Assert.Greater(clip.length, .01f, cue.id);
                }
            }
            foreach (string id in new[] { "door_bang", "door_knock", "door_break", "cage_struggle", "lever", "trapdoor",
                "stab", "box_pickup", "footsteps_metal", "footsteps_soft", "appear", "merge", "splash", "dust",
                "summon", "volley", "lightning", "ice_freeze", "ice_crack", "ice_break", "reveal", "charge", "slash", "dash", "cleave" })
                Assert.IsTrue(names.Contains(id), "unresolved stage/boss action " + id);
            Assert.Less(catalog.Find("volley").clips[0].length, .4f,
                "frequent boss volleys need a short cue to avoid stacking long sound tails");
        }

        [Test]
        public void AudioPlaybackPreservesGameplayRandomAndRespectsSfxSettings()
        {
            var owner = new GameObject("SFX lifecycle test");
            var originalState = Random.state;
            try
            {
                var source = StorySfx.CreateSource(owner, "Story cue");
                Assert.AreEqual(owner.transform, source.transform.parent);
                Assert.IsFalse(source.playOnAwake);
                Assert.IsFalse(source.ignoreListenerPause, "gameplay SFX must obey the pause listener");
                Assert.AreEqual(0f, source.spatialBlend);
                Random.InitState(1303);
                var before = Random.state;
                Assert.IsTrue(StorySfx.Play(source, "volley"));
                float afterPlayback = Random.value;
                Random.state = before;
                Assert.AreEqual(Random.value, afterPlayback, "new SFX changed the boss/enemy RNG sequence");
                Assert.AreSame(StorySfx.Catalog.mixerGroup, source.outputAudioMixerGroup);
                owner.SetActive(false);
                Assert.IsFalse(StorySfx.Play(source, "volley"), "a hidden/destroyed scene may not emit new sounds");
            }
            finally
            {
                Random.state = originalState;
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ReplacingTheLegacyBossAttackSoundPreservesItsPitchAndCombatRandomSequence()
        {
            var owner = new GameObject("Attack audio RNG comparison");
            var inactiveBossParent = new GameObject("Unstarted boss fixture");
            inactiveBossParent.SetActive(false);
            var originalState = Random.state;
            try
            {
                // Keep the real prefab's legacy SoundData, but never start its combat/animation lifecycle.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab");
                var bossObject = Object.Instantiate(prefab, inactiveBossParent.transform);
                var combat = bossObject.GetComponent<BossPhaseTwoCombat>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var data = (SoundData)typeof(BossPhaseTwoCombat).GetField("attackSfx", flags).GetValue(combat);
                Assert.IsNotNull(data.clip, "the old attack path must have a valid clip to consume its pitch draw");
                var replacement = StorySfx.CreateSource(owner, "Replacement attack");
                typeof(BossPhaseTwoCombat).GetField("attackSfxSource", flags).SetValue(combat, replacement);

                var legacyObject = new GameObject("Legacy attack emitter");
                legacyObject.transform.SetParent(owner.transform, false);
                var legacy = legacyObject.AddComponent<SoundEmitter>();
                legacy.Initialize(data, StorySfx.Catalog.mixerGroup);

                Random.InitState(947);
                // Execute the actual pitch method used by SoundBuilder.WithRandomPitch().Play(data).
                legacy.WithRandomPitch();
                var legacyState = Random.state;
                float legacyPitch = legacy.GetComponent<AudioSource>().pitch;

                Random.InitState(947);
                typeof(BossPhaseTwoCombat).GetMethod("PlayAttackSound", flags).Invoke(combat, new object[] { "slash" });
                Assert.AreEqual(legacyState, Random.state, "changing the attack clip changed future combat choices");
                Assert.AreEqual(legacyPitch, replacement.pitch, "the replacement lost the previous pitch variation");
            }
            finally
            {
                Random.state = originalState;
                Object.DestroyImmediate(inactiveBossParent);
                Object.DestroyImmediate(owner);
            }
        }
    }
}
#endif
