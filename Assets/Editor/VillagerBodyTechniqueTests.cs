using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class VillagerBodyTechniqueTests
{
    private const string Generated = "Assets/RE4ChapterOneAnimationLoop/Generated/";
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void FirearmPoiseKeepsLeftAndRightLegIndependent()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "MainGanadoMale.prefab");
        var actor = Object.Instantiate(prefab);
        try
        {
            var combat = actor.GetComponent<EnemyCombatModule>();
            Assert.That(SelectReaction(combat, 0f, 8.4f, 0f), Is.EqualTo("None"));
            Assert.That(SelectReaction(combat, 0f, 0f, 8.4f), Is.EqualTo("None"));
            Assert.That(SelectReaction(combat, 0f, 8.4f, 0f), Is.EqualTo("LeftKneel"));
            Assert.That(SelectReaction(combat, 0f, 0f, 8.4f), Is.EqualTo("RightKneel"));
            Assert.That(SelectReaction(combat, 0f, 8.4f, 0f), Is.EqualTo("None"));
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void PistolHeadStaggerAccumulatesAcrossHits()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "MainGanadoMale.prefab");
        var actor = Object.Instantiate(prefab);
        try
        {
            var combat = actor.GetComponent<EnemyCombatModule>();
            Assert.That(SelectReaction(combat, 30f, 0f, 0f, true), Is.EqualTo("None"));
            Assert.That(SelectReaction(combat, 30f, 0f, 0f, true), Is.EqualTo("HeadStagger"));
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void MixedShotRanksNewContributionAndBreaksTiesByBodyPart()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "MainGanadoMale.prefab");
        var actor = Object.Instantiate(prefab);
        try
        {
            var combat = actor.GetComponent<EnemyCombatModule>();
            Assert.That(SelectReaction(combat, 30f, 16f, 16f), Is.EqualTo("HeadStagger"));
            Assert.That(SelectReaction(combat, 30f, 16f, 24f), Is.EqualTo("RightKneel"));
            Assert.That(SelectReaction(combat, 0f, 16f, 16f), Is.EqualTo("LeftKneel"));
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [TestCase("MainGanadoFemale", false)]
    [TestCase("MainGanadoMale", false)]
    [TestCase("MainGanadoArmed", true)]
    public void ProductionBodyTechniqueTracksEffectiveClips(string name, bool armed)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + name + ".prefab");
        var presenter = new SerializedObject(prefab.GetComponent<EnemyAnimationAdapter>());
        Assert.IsTrue(presenter.FindProperty("nativeBodyTechnique").boolValue);
        RuntimeAnimatorController current = prefab.GetComponent<Animator>().runtimeAnimatorController;
        if (armed)
        {
            AssertBindings(presenter.FindProperty("armedBodyTechnique"), current);
            var grip = new SerializedObject(prefab.GetComponent<EnemyHeldWeapon>());
            current = (RuntimeAnimatorController)grip.FindProperty("unarmedController").objectReferenceValue;
        }

        AssertBindings(presenter.FindProperty("unarmedBodyTechnique"), current);
    }

    private static string SelectReaction(EnemyCombatModule combat, float head, float leftLeg, float rightLeg, bool pistol = false)
    {
        Type shotType = typeof(EnemyCombatModule).Assembly.GetType("FirearmBodyTechniqueShot", true);
        object shot = Activator.CreateInstance(shotType);
        shotType.GetField("head", InstanceFields).SetValue(shot, head);
        shotType.GetField("leftLeg", InstanceFields).SetValue(shot, leftLeg);
        shotType.GetField("rightLeg", InstanceFields).SetValue(shot, rightLeg);
        shotType.GetField("pistol", InstanceFields).SetValue(shot, pistol);
        var arguments = new[] { shot, null };
        var select = typeof(EnemyCombatModule).GetMethod("TrySelectNativeFirearmReaction", InstanceFields);
        select.Invoke(combat, arguments);
        return arguments[1].ToString();
    }

    private static void AssertBindings(SerializedProperty profile, RuntimeAnimatorController controller)
    {
        var overrides = controller as AnimatorOverrideController;
        var graph = controller as AnimatorController;
        if (overrides != null)
        {
            graph = (AnimatorController)overrides.runtimeAnimatorController;
        }

        AnimatorStateMachine root = graph.layers[0].stateMachine;
        Check("head", "HitReact", "Hit_Head");
        Check("leftKneel", "HitReact", "Hit_NativeKneelLeft");
        Check("rightKneel", "HitReact", "Hit_NativeKneelRight");
        Check("perfectParry", "Stagger", "Stagger_Heavy_Front");

        void Check(string property, string machine, string state)
        {
            var clip = (AnimationClip)profile.FindPropertyRelative(property).objectReferenceValue;
            var stateMachine = root.stateMachines.Single(child => child.stateMachine.name == machine).stateMachine;
            AnimatorState boundState = stateMachine.states.Single(child => child.state.name == state).state;
            var expected = (AnimationClip)boundState.motion;
            if (overrides != null)
            {
                AnimationClip replacement = overrides[expected];
                if (replacement != null)
                {
                    expected = replacement;
                }
            }

            Assert.AreSame(expected, clip, state);
            if (property.EndsWith("Kneel", StringComparison.Ordinal))
            {
                var entry = stateMachine.entryTransitions.Single(item => item.destinationState == boundState);
                var condition = entry.conditions.Single(item => item.parameter == "HitDir");
                Assert.That(stateMachine.entryTransitions.Count(item => item.conditions.Any(value =>
                    value.parameter == "HitDir" && value.mode == AnimatorConditionMode.Equals
                    && value.threshold == condition.threshold)), Is.EqualTo(1), state);
                float end = profile.FindPropertyRelative("kneelFrames").vector2Value.y / clip.frameRate;
                var marker = AnimationUtility.GetAnimationEvents(clip).Single(item => item.functionName == "OnHitComplete");
                Assert.That(marker.time, Is.GreaterThan(end));
            }
        }
    }
}
