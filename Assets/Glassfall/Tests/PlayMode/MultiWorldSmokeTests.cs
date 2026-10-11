using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;

namespace Glassfall.Tests.PlayMode
{
    public sealed class MultiWorldSmokeTests
    {
        private const string ControllerTypeName =
            "Glassfall.Gameplay.Run.MicroRunController, Assembly-CSharp";
        private const string ProgressTypeName =
            "Glassfall.Gameplay.Run.MicroRunProgress, Assembly-CSharp";
        private const string SegmentTypeName =
            "Glassfall.Gameplay.Run.MicroRunWorldSegment, Assembly-CSharp";
        private const string MovementModeTypeName =
            "Glassfall.Gameplay.Combat.ArcHunterMovementMode, Assembly-CSharp";
        private const string WetnessTypeName =
            "Glassfall.Gameplay.Wetness.WetnessField, Assembly-CSharp";
        private const string ContaminationTypeName =
            "Glassfall.Gameplay.Run.ContaminationSystem, Assembly-CSharp";
        private const string RewardControllerTypeName =
            "Glassfall.Gameplay.Rewards.MidRunRewardController, Assembly-CSharp";
        private const string RewardTypeName =
            "Glassfall.Gameplay.Rewards.MidRunReward, Assembly-CSharp";
        private const string RouterTypeName =
            "Glassfall.Presentation.WorldPresentationRouter, Assembly-CSharp";
        private const string NeonVideoTypeName =
            "Glassfall.Presentation.NeonWindowVideoBackground, Assembly-CSharp";
        private const string FoggedBackgroundTypeName =
            "Glassfall.Presentation.FoggedWindowWorldBackground, Assembly-CSharp";

        [UnityTest]
        public IEnumerator SixEncounterRunHandsOffWorldAndKeepsRunState()
        {
            GameObject root = new GameObject("M26PlayModeSmoke");
            GameObject fogged = new GameObject("FoggedWindowWorldV1");
            GameObject neon = new GameObject("NeonWindowWorldV1");
            try
            {
                Component wetness = root.AddComponent(
                    Type.GetType(WetnessTypeName, true));
                Component contamination = root.AddComponent(
                    Type.GetType(ContaminationTypeName, true));
                Component rewards = root.AddComponent(
                    Type.GetType(RewardControllerTypeName, true));
                Component controller = root.AddComponent(
                    Type.GetType(ControllerTypeName, true));

                ConfigureM26Segments(controller);
                Invoke(controller, "ConfigureWorldHandoff", wetness);

                object progress = Activator.CreateInstance(
                    Type.GetType(ProgressTypeName, true),
                    6);
                Invoke(progress, "SelectBuild");
                Invoke(progress, "CompletePreparation");
                Invoke(progress, "CompleteResonance");
                SetPrivateField(controller, "_progress", progress);

                Component router = root.AddComponent(
                    Type.GetType(RouterTypeName, true));
                Invoke(router, "Configure", controller, fogged, neon, 0, 1);

                Invoke(contamination, "Configure");
                float contaminationBefore = (float)Invoke(
                    contamination,
                    "ApplyArcHunterHit",
                    false);
                Invoke(wetness, "DepositWorld", Vector2.zero, 1f, 1f);

                IList selectedRewards = (IList)GetPrivateField(
                    rewards,
                    "_selectedOneTimeRewards");
                Type rewardType = Type.GetType(RewardTypeName, true);
                selectedRewards.Add(Enum.Parse(rewardType, "StableLink"));
                Assert.That(selectedRewards.Count, Is.EqualTo(1));

                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                SetPrivateField(controller, "_activeWorldSegmentIndex", 0);
                InvokeNonPublic(controller, "ApplyWorldHandoffIfNeeded");
                InvokeNonPublic(router, "Update");

                Assert.That(GetProperty(controller, "CurrentWorldId").ToString(),
                    Is.EqualTo("NEON_WINDOW"));
                Assert.That((int)GetProperty(controller, "CurrentWorldEncounter"),
                    Is.EqualTo(1));
                Assert.That(fogged.activeSelf, Is.False);
                Assert.That(neon.activeSelf, Is.True);
                Assert.That((float)Invoke(wetness, "SampleWorld", Vector2.zero),
                    Is.EqualTo(0f));
                Assert.That((float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(contaminationBefore));
                Assert.That(selectedRewards.Count, Is.EqualTo(1));

                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                Assert.That((bool)GetProperty(controller, "IsRunFinalEncounter"),
                    Is.True);
                Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
                Assert.That(GetProperty(progress, "Phase").ToString(),
                    Is.EqualTo("Complete"));
                Assert.That((int)GetProperty(controller, "EncounterCount"),
                    Is.EqualTo(6));
                Assert.That((int)GetProperty(controller, "CurrentWorldEncounter"),
                    Is.EqualTo(3));

                yield return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(fogged);
                UnityEngine.Object.Destroy(neon);
            }
        }

        [UnityTest]
        public IEnumerator ConfiguredPrototypeSceneCompletesSixEncounterSequence()
        {
            SceneManager.LoadScene("Prototype", LoadSceneMode.Single);
            yield return null;

            GameObject runObject = FindSceneObject("MicroRunV1");
            GameObject fogged = FindSceneObject("FoggedWindowWorldV1");
            GameObject neon = FindSceneObject("NeonWindowWorldV1");
            Assert.That(runObject, Is.Not.Null);
            Assert.That(fogged, Is.Not.Null);
            Assert.That(neon, Is.Not.Null);

            Component controller = runObject.GetComponent(
                Type.GetType(ControllerTypeName, true));
            Component router = runObject.GetComponent(
                Type.GetType(RouterTypeName, true));
            Component neonVideo = neon.GetComponent(
                Type.GetType(NeonVideoTypeName, true));
            VideoPlayer neonPlayer = neon.GetComponent<VideoPlayer>();
            Component foggedBackground = fogged.GetComponent(
                Type.GetType(FoggedBackgroundTypeName, true));
            VideoPlayer foggedPlayer = fogged.GetComponent<VideoPlayer>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(neonVideo, Is.Not.Null);
            Assert.That(neonPlayer, Is.Not.Null);
            Assert.That(foggedBackground, Is.Not.Null);
            Assert.That(foggedPlayer, Is.Not.Null);
            Assert.That((int)GetProperty(controller, "WorldSegmentCount"),
                Is.EqualTo(2));
            Assert.That((int)GetProperty(controller, "EncounterCount"),
                Is.EqualTo(6));
            Assert.That(fogged.activeSelf, Is.True);
            Assert.That(neon.activeSelf, Is.False);
            Assert.That(neonPlayer.isPlaying, Is.False);

            object progress = GetPrivateField(controller, "_progress");
            Component wetness = (Component)GetPrivateField(
                controller,
                "wetnessField");
            Component contamination = (Component)GetPrivateField(
                controller,
                "contaminationSystem");
            Component rewards = (Component)GetPrivateField(
                controller,
                "rewardController");
            Assert.That(progress, Is.Not.Null);
            Assert.That(wetness, Is.Not.Null);
            Assert.That(contamination, Is.Not.Null);
            Assert.That(rewards, Is.Not.Null);

            Invoke(progress, "SelectBuild");
            Invoke(progress, "CompletePreparation");
            Invoke(progress, "CompleteResonance");
            float contaminationBefore = (float)Invoke(
                contamination,
                "ApplyArcHunterHit",
                false);
            Invoke(wetness, "DepositWorld", Vector2.zero, 1f, 1f);

            IList selectedRewards = (IList)GetPrivateField(
                rewards,
                "_selectedOneTimeRewards");
            Type rewardType = Type.GetType(RewardTypeName, true);
            selectedRewards.Add(Enum.Parse(rewardType, "StableLink"));

            CompleteEncounterCycle(progress);
            CompleteEncounterCycle(progress);
            CompleteEncounterCycle(progress);
            InvokeNonPublic(controller, "ApplyWorldHandoffIfNeeded");
            InvokeNonPublic(router, "Update");

            Assert.That(GetProperty(controller, "CurrentWorldId").ToString(),
                Is.EqualTo("NEON_WINDOW"));
            Assert.That(fogged.activeSelf, Is.False);
            Assert.That(neon.activeSelf, Is.True);
            Assert.That(foggedPlayer.isPlaying, Is.False);
            Assert.That(foggedPlayer.enabled, Is.False);
            Assert.That(
                (int)GetProperty(foggedBackground, "CurrentBlurPhase"),
                Is.EqualTo(0));
            Assert.That(
                (bool)GetProperty(neonVideo, "IsPlaybackRequested"),
                Is.True);
            Assert.That((float)Invoke(wetness, "SampleWorld", Vector2.zero),
                Is.EqualTo(0f));
            Assert.That((float)GetProperty(contamination, "Value01"),
                Is.EqualTo(contaminationBefore));
            Assert.That(selectedRewards.Count, Is.EqualTo(1));

            CompleteEncounterCycle(progress);
            CompleteEncounterCycle(progress);
            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("Complete"));
            Assert.That((int)GetProperty(controller, "CurrentWorldEncounter"),
                Is.EqualTo(3));
            Assert.That((int)GetProperty(controller, "WorldHandoffCount"),
                Is.EqualTo(1));

            ((Behaviour)router).enabled = false;
            neon.SetActive(false);
            yield return null;
            Assert.That(
                (bool)GetProperty(neonVideo, "IsPlaybackRequested"),
                Is.False);
            Assert.That(neonPlayer.isPlaying, Is.False);
            Assert.That(neonPlayer.enabled, Is.False);
        }

        private static void ConfigureM26Segments(object controller)
        {
            Type segmentType = Type.GetType(SegmentTypeName, true);
            Type movementModeType = Type.GetType(MovementModeTypeName, true);
            Array segments = Array.CreateInstance(segmentType, 2);
            segments.SetValue(CreateSegment(
                segmentType,
                movementModeType,
                "FOGGED_WINDOW",
                new[]
                {
                    new Vector2(-11f, -5f),
                    new Vector2(0f, 7f),
                    new Vector2(13f, 2f)
                },
                new[] { "Chase", "Strafe", "WetRoutePressure" }), 0);
            segments.SetValue(CreateSegment(
                segmentType,
                movementModeType,
                "NEON_WINDOW",
                new[]
                {
                    new Vector2(-12f, 6.2f),
                    new Vector2(6.8f, -6.4f),
                    new Vector2(14.2f, 5.5f)
                },
                new[] { "Strafe", "WetRoutePressure", "Chase" }), 1);
            Invoke(controller, "ConfigureWorldSegments", segments);
        }

        private static object CreateSegment(
            Type segmentType,
            Type movementModeType,
            string worldId,
            Vector2[] positions,
            string[] movementModes)
        {
            Array modes = Array.CreateInstance(
                movementModeType,
                movementModes.Length);
            for (int i = 0; i < movementModes.Length; i++)
            {
                modes.SetValue(
                    Enum.Parse(movementModeType, movementModes[i]),
                    i);
            }

            return Activator.CreateInstance(
                segmentType,
                worldId,
                positions,
                modes);
        }

        private static void CompleteEncounterCycle(object progress)
        {
            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
            Assert.That((bool)Invoke(progress, "CompleteRewardChoice"), Is.True);
            Assert.That((bool)Invoke(progress, "CompleteRecovery"), Is.True);
            Assert.That((bool)Invoke(progress, "FinishIntermission"), Is.True);
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            ParameterInfo[] parameters = method.GetParameters();
            if (arguments.Length == parameters.Length)
                return method.Invoke(instance, arguments);

            object[] completed = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                completed[i] = i < arguments.Length
                    ? arguments[i]
                    : parameters[i].DefaultValue;
            }

            return method.Invoke(instance, completed);
        }

        private static object InvokeNonPublic(
            object instance,
            string methodName)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(instance, null);
        }

        private static void SetPrivateField(
            object instance,
            string fieldName,
            object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }

        private static object GetPrivateField(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(instance);
        }

        private static object GetProperty(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(instance);
        }

        private static GameObject FindSceneObject(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate.name == objectName &&
                    candidate.scene.IsValid() &&
                    candidate.scene.isLoaded)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
