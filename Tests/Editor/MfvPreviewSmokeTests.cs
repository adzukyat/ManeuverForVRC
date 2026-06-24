using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ManeuverForVRC.Editor;
using NUnit.Framework;
using StageLightManeuver;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

#if UDONSHARP
using UdonSharp;
using UdonSharpEditor;
#endif

namespace ManeuverForVRC.Tests
{
    public class MfvPreviewSmokeTests
    {
        private const string MenuPlayerName = "ManeuverForVRC Baked Player";
        private const string DefaultBakeRoot = "Assets/ManeuverForVRC";

        [UnityTest]
        public IEnumerator Level3_RealTimelinePreview_UpdatesChannelAndVrslFixture()
        {
            var context = MfvPreviewSmokeFixtureBuilder.OpenFreshScene();
            AssertPreviewContext(context);

            MfvPreviewSmokeFixtureBuilder.ResetFixtureForRuntime(context);
            context.Channel.lastFrame = MfvVRSLFrame.Default();
            var sample = MfvPreviewSmokeFixtureBuilder.EvaluatePreview(context, MfvPreviewSmokeFixtureBuilder.PreviewTime);
            yield return null;

            var diagnostics = MfvPreviewSmokeFixtureBuilder.BuildDiagnostics(context, sample.Before, sample.After);
            AssertFixtureChanged(sample.Before, sample.After, diagnostics);
            Assert.That(context.Channel.lastFrame.pan, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedPan).Within(0.0001f), diagnostics);
            Assert.That(context.Channel.lastFrame.tilt, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedTilt).Within(0.0001f), diagnostics);
            Assert.That(context.Channel.lastFrame.intensity, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedIntensity).Within(0.0001f), diagnostics);
            Assert.That(sample.After.Pan, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedVrslPan).Within(0.0001f), diagnostics);
            Assert.That(sample.After.Tilt, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedVrslTilt).Within(0.0001f), diagnostics);
            Assert.That(sample.After.Intensity, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedIntensity).Within(0.0001f), diagnostics);
            Assert.That(sample.After.Color.r, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedColor.r).Within(0.0001f), diagnostics);
            Assert.That(sample.After.Color.g, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedColor.g).Within(0.0001f), diagnostics);
            Assert.That(sample.After.Color.b, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedColor.b).Within(0.0001f), diagnostics);
            Assert.That(sample.After.ConeWidth, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedConeWidth).Within(0.0001f), diagnostics);
            Assert.That(sample.After.ConeLength, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedConeLength).Within(0.0001f), diagnostics);
            Assert.That(sample.After.Gobo, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedGobo), diagnostics);
            Assert.IsFalse(sample.After.EnableDmx, diagnostics);
            Assert.IsFalse(sample.After.EnableStrobe, diagnostics);
        }

        [Test]
        public void Level3_FreshTimelineClipEditorAndEvaluation_DoesNotLogErrors()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var directorObject = new GameObject("Fresh Timeline Director");
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                timeline.name = "Fresh Timeline Clip Smoke";
                var track = timeline.CreateTrack<StageLightTimelineTrack>(null, "Fresh SLM");
                var clip = track.CreateClip<StageLightTimelineClip>();
                clip.start = 0;
                clip.duration = 1;
                var slmClip = (StageLightTimelineClip)clip.asset;
                slmClip.behaviour.stageLightQueueData.stageLightProperties.Add(new LightProperty());
                slmClip.behaviour.stageLightQueueData.stageLightProperties.Add(new LightIntensityProperty());
                slmClip.behaviour.stageLightQueueData.stageLightProperties.Add(new LightColorProperty());

                var clipEditor = new StageLightTimelineClipEditor();
                Assert.DoesNotThrow(() => clipEditor.OnClipChanged(clip));
                AssertClockOverridesInitialized(slmClip);

                slmClip.track = null;
                track.drawCustomClip = false;
                Assert.DoesNotThrow(() => clipEditor.DrawBackground(
                    clip,
                    new ClipBackgroundRegion(new Rect(0f, 0f, 160f, 18f), 0, clip.duration)));

                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                var stageLightFixture = fixtureObject.AddComponent<StageLightFixture>();
                var channel = fixtureObject.AddComponent<MfvVRSLFixtureChannel>();
                var vrslFixture = fixtureObject.AddComponent<VRSL.VRStageLighting_DMX_Static>();
                vrslFixture.objRenderers = new[] { fixtureObject.GetComponent<MeshRenderer>() };
                channel.vrslFixture = vrslFixture;
                stageLightFixture.Init();
                director.SetGenericBinding(track, stageLightFixture);

                Assert.DoesNotThrow(() =>
                {
                    director.time = 0.5;
                    director.Evaluate();
                });
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void Level3_FreshTimelineClipWithoutBinding_DoesNotWarnOnEvaluate()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var directorObject = new GameObject("Unbound Fresh Timeline Director");
            try
            {
                var track = timeline.CreateTrack<StageLightTimelineTrack>(null, "Unbound Fresh SLM");
                var clip = track.CreateClip<StageLightTimelineClip>();
                clip.start = 0;
                clip.duration = 1;

                var clipEditor = new StageLightTimelineClipEditor();
                Assert.DoesNotThrow(() => clipEditor.OnCreate(clip, track, null));

                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;

                Assert.DoesNotThrow(() =>
                {
                    director.time = 0.5;
                    director.Evaluate();
                });
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(directorObject);
            }
        }

        [Test]
        public void Level3_FreshTimelineClipEvaluation_AddsScalarVrslPropertiesFromFixtureDefaults()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var directorObject = new GameObject("Fresh Timeline Defaults Director");
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                timeline.name = "Fresh Timeline Defaults";
                var track = timeline.CreateTrack<StageLightTimelineTrack>(null, "Fresh SLM Defaults");
                var clip = track.CreateClip<StageLightTimelineClip>();
                clip.start = 0;
                clip.duration = 1;
                var slmClip = (StageLightTimelineClip)clip.asset;

                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;

                var stageLightFixture = fixtureObject.AddComponent<StageLightFixture>();
                var channel = fixtureObject.AddComponent<MfvVRSLFixtureChannel>();
                var vrslFixture = fixtureObject.AddComponent<VRSL.VRStageLighting_DMX_Static>();
                vrslFixture.objRenderers = new[] { fixtureObject.GetComponent<MeshRenderer>() };
                vrslFixture.enableDMXChannels = true;
                vrslFixture.enableStrobe = true;
                vrslFixture.panOffsetBlueGreen = 12f;
                vrslFixture.tiltOffsetBlue = 34f;
                vrslFixture.globalIntensity = 0.65f;
                vrslFixture.lightColorTint = new Color(0.2f, 0.4f, 0.8f, 1f);
                vrslFixture.coneWidth = 2.2f;
                vrslFixture.coneLength = 7f;
                vrslFixture.selectGOBO = 6;
                channel.vrslFixture = vrslFixture;
                stageLightFixture.Init();
                director.SetGenericBinding(track, stageLightFixture);

                director.time = 0.5;
                director.Evaluate();

                Assert.NotNull(slmClip.StageLightQueueData.TryGetActiveProperty<LightIntensityProperty>());
                Assert.NotNull(slmClip.StageLightQueueData.TryGetActiveProperty<LightColorProperty>());
                Assert.NotNull(slmClip.StageLightQueueData.TryGetActiveProperty<LightProperty>());
                Assert.NotNull(slmClip.StageLightQueueData.TryGetActiveProperty<PanProperty>());
                Assert.NotNull(slmClip.StageLightQueueData.TryGetActiveProperty<TiltProperty>());
                Assert.NotNull(slmClip.StageLightQueueData.TryGetActiveProperty<MfvVRSLGoboProperty>());
                Assert.IsNull(slmClip.StageLightQueueData.TryGetActiveProperty<LightFlickerProperty>());
                Assert.IsNull(slmClip.StageLightQueueData.TryGetActiveProperty<ManualLightArrayProperty>());
                Assert.IsNull(slmClip.StageLightQueueData.TryGetActiveProperty<ManualColorArrayProperty>());
                Assert.IsNull(slmClip.StageLightQueueData.TryGetActiveProperty<ManualPanTiltProperty>());
                Assert.That(channel.lastFrame.pan, Is.EqualTo(-12f).Within(0.0001f));
                Assert.That(channel.lastFrame.tilt, Is.EqualTo(-56f).Within(0.0001f));
                Assert.That(channel.lastFrame.intensity, Is.EqualTo(0.65f).Within(0.0001f));
                Assert.That(channel.lastFrame.color.r, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(channel.lastFrame.color.g, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(channel.lastFrame.color.b, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(channel.lastFrame.coneWidth, Is.EqualTo(2.2f).Within(0.0001f));
                Assert.That(channel.lastFrame.coneLength, Is.EqualTo(7f).Within(0.0001f));
                Assert.That(channel.lastFrame.gobo, Is.EqualTo(6));
                Assert.That(vrslFixture.globalIntensity, Is.EqualTo(0.65f).Within(0.0001f));
                Assert.That(vrslFixture.lightColorTint.r, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(vrslFixture.lightColorTint.g, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(vrslFixture.lightColorTint.b, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(vrslFixture.coneWidth, Is.EqualTo(2.2f).Within(0.0001f));
                Assert.That(vrslFixture.coneLength, Is.EqualTo(7f).Within(0.0001f));
                Assert.That(vrslFixture.selectGOBO, Is.EqualTo(6));
                Assert.IsFalse(vrslFixture.enableDMXChannels);
                Assert.IsFalse(vrslFixture.enableStrobe);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void Level3_AddablePropertyTypes_AreFilteredToBoundChannels()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var directorObject = new GameObject("Addable Property Director");
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var track = timeline.CreateTrack<StageLightTimelineTrack>(null, "SLM Addable Filter");
                var clip = track.CreateClip<StageLightTimelineClip>();
                var slmClip = (StageLightTimelineClip)clip.asset;

                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;

                var stageLightFixture = fixtureObject.AddComponent<StageLightFixture>();
                fixtureObject.AddComponent<LightChannel>();
                fixtureObject.AddComponent<LightPanChannel>();
                stageLightFixture.Init();
                director.SetGenericBinding(track, stageLightFixture);

                var addableTypes = slmClip.GetAddablePropertyTypes(director);

                Assert.That(addableTypes, Has.Member(typeof(LightProperty)));
                Assert.That(addableTypes, Has.Member(typeof(LightColorProperty)));
                Assert.That(addableTypes, Has.Member(typeof(LightIntensityProperty)));
                Assert.That(addableTypes, Has.Member(typeof(LightFlickerProperty)));
                Assert.That(addableTypes, Has.Member(typeof(ManualLightArrayProperty)));
                Assert.That(addableTypes, Has.Member(typeof(ManualColorArrayProperty)));
                Assert.That(addableTypes, Has.Member(typeof(PanProperty)));
                Assert.That(addableTypes, Has.Member(typeof(ManualPanTiltProperty)));
                Assert.That(addableTypes, Has.No.Member(typeof(MaterialFloatProperty)));
                Assert.That(addableTypes, Has.No.Member(typeof(MaterialColorProperty)));
                Assert.That(addableTypes, Has.No.Member(typeof(EnvironmentProperty)));
                Assert.That(addableTypes, Has.No.Member(typeof(ReflectionProbeProperty)));
            }
            finally
            {
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void Level3_ManualAddProperty_DoesNotAutoAddLightColor()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var directorObject = new GameObject("Manual Add Property Director");
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var track = timeline.CreateTrack<StageLightTimelineTrack>(null, "SLM Manual Add");
                var clip = track.CreateClip<StageLightTimelineClip>();
                var slmClip = (StageLightTimelineClip)clip.asset;

                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;

                var stageLightFixture = fixtureObject.AddComponent<StageLightFixture>();
                var channel = fixtureObject.AddComponent<MfvVRSLFixtureChannel>();
                var vrslFixture = fixtureObject.AddComponent<VRSL.VRStageLighting_DMX_Static>();
                vrslFixture.objRenderers = new[] { fixtureObject.GetComponent<MeshRenderer>() };
                vrslFixture.panOffsetBlueGreen = 12f;
                channel.vrslFixture = vrslFixture;
                stageLightFixture.Init();
                director.SetGenericBinding(track, stageLightFixture);

                Assert.IsTrue(SlmEditorUtility.AddPropertyInClip(slmClip, typeof(PanProperty), director, false));
                Assert.DoesNotThrow(() => new StageLightTimelineClipEditor().OnClipChanged(clip));

                var pan = slmClip.StageLightQueueData.TryGetActiveProperty<PanProperty>();
                Assert.NotNull(pan);
                Assert.That(pan.rollTransform.value.constant, Is.EqualTo(-12f).Within(0.0001f));
                Assert.IsNull(slmClip.StageLightQueueData.TryGetActiveProperty<LightColorProperty>());
            }
            finally
            {
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void Level3_StageLightPropertiesDrawer_OrderedViewKeepsSerializedPropertyMapping()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            try
            {
                var track = timeline.CreateTrack<StageLightTimelineTrack>(null, "SLM Drawer Mapping");
                var clip = track.CreateClip<StageLightTimelineClip>();
                var slmClip = (StageLightTimelineClip)clip.asset;
                var properties = new List<SlmProperty>
                {
                    new ClockProperty(),
                    new StageLightOrderProperty(),
                    new LightIntensityProperty(),
                    new LightColorProperty(),
                    new LightProperty()
                };
                slmClip.behaviour.stageLightQueueData.stageLightProperties = properties;

                using var serializedObject = new SerializedObject(slmClip);
                var serializedProperties = serializedObject
                    .FindProperty("behaviour")
                    .FindPropertyRelative("stageLightQueueData")
                    .FindPropertyRelative("stageLightProperties");

                var drawerType = typeof(StageLightPropertiesDrawer);
                var createOrderedView = drawerType.GetMethod(
                    "CreateOrderedPropertyView",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var getArrayElementForPropertyInstance = drawerType.GetMethod(
                    "GetArrayElementForPropertyInstance",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(createOrderedView);
                Assert.NotNull(getArrayElementForPropertyInstance);

                var orderedView = (List<SlmProperty>)createOrderedView.Invoke(null, new object[] { properties });
                var lightProperty = orderedView.OfType<LightProperty>().Single();
                var serializedLightProperty = (SerializedProperty)getArrayElementForPropertyInstance.Invoke(
                    null,
                    new object[] { serializedProperties, properties, lightProperty });

                Assert.That(properties.Select(stageLightProperty => stageLightProperty.GetType()), Is.EqualTo(new[]
                {
                    typeof(ClockProperty),
                    typeof(StageLightOrderProperty),
                    typeof(LightIntensityProperty),
                    typeof(LightColorProperty),
                    typeof(LightProperty)
                }));
                Assert.NotNull(serializedLightProperty);
                Assert.That(
                    serializedLightProperty.FindPropertyRelative("propertyName").stringValue,
                    Is.EqualTo("Light"));
            }
            finally
            {
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void Level4_BakeConsistency_MatchesPreviewAndKeepsUploadTimeline()
        {
            var context = MfvPreviewSmokeFixtureBuilder.OpenFreshScene();
            AssertPreviewContext(context);

            MfvPreviewSmokeFixtureBuilder.ResetFixtureForRuntime(context);
            var preview = MfvPreviewSmokeFixtureBuilder
                .EvaluatePreview(context, MfvPreviewSmokeFixtureBuilder.PreviewTime)
                .After;
            var stateBeforeBake = preview;

            var settings = MfvBakeSettings.CreateDefault();
            settings.internalSampleRate = 30f;
            var outputFolder = MfvPreviewSmokeFixtureBuilder.CreateTemporaryBakeFolder();
            MfvBakeResult result = null;
            try
            {
                result = BakePreviewFixture(context, settings, outputFolder);

                var afterBake = MfvPreviewSmokeFixtureBuilder.FixtureState.Capture(context.Fixture);
                var diagnostics = MfvPreviewSmokeFixtureBuilder.BuildBakeDiagnostics(preview, result, afterBake, context);
                Assert.NotNull(result, diagnostics);
                Assert.NotNull(result.bakedAsset, diagnostics);
                Assert.That(result.fixtures, Has.Length.EqualTo(1), diagnostics);
                Assert.That(result.bakedAsset.FixtureCount, Is.EqualTo(1), diagnostics);
                Assert.That(result.bakedAsset.ContinuousTrackCount, Is.GreaterThan(0), diagnostics);
                Assert.That(result.bakedAsset.keyTimes, Is.Not.Empty, diagnostics);
                Assert.That(result.bakedAsset.keyValues, Is.Not.Empty, diagnostics);
                Assert.That(result.bakedAsset.EventTrackCount, Is.GreaterThan(0), diagnostics);
                Assert.That(result.bakedAsset.eventTimes, Is.Not.Empty, diagnostics);
                Assert.That(result.bakedAsset.eventValues, Is.Not.Empty, diagnostics);
                Assert.NotNull(result.uploadTimeline, diagnostics);

                var uploadTracks = GetAllTracks(result.uploadTimeline);
                Assert.IsFalse(uploadTracks.Any(track => track is StageLightTimelineTrack), diagnostics);
                var uploadActivation = uploadTracks.OfType<ActivationTrack>().SingleOrDefault();
                var uploadAnimation = uploadTracks.OfType<AnimationTrack>().SingleOrDefault();
                AssertRetainedTrackClips(
                    context.ActivationTrack,
                    uploadActivation,
                    MfvPreviewSmokeFixtureBuilder.ExpectedActivationStart,
                    MfvPreviewSmokeFixtureBuilder.ExpectedActivationDuration,
                    "ActivationTrack",
                    diagnostics);
                AssertRetainedTrackClips(
                    context.AnimationTrack,
                    uploadAnimation,
                    MfvPreviewSmokeFixtureBuilder.ExpectedAnimationStart,
                    MfvPreviewSmokeFixtureBuilder.ExpectedAnimationDuration,
                    "AnimationTrack",
                    diagnostics);
                AssertAnimationClipCurvesRetained(uploadAnimation, diagnostics);
                Assert.IsTrue(context.Timeline.GetOutputTracks().Any(track => track is StageLightTimelineTrack),
                    "Bake should not delete SLM tracks from the source Timeline.\n" + diagnostics);
                AssertFixtureClose(stateBeforeBake, afterBake, "Bake should restore the source fixture state.", diagnostics);

                var playerObject = new GameObject("Runtime Player");
                try
                {
                    var player = playerObject.AddComponent<MfvVRSLTimelinePlayer>();
                    MfvBakeUtility.ConfigurePlayer(player, context.Director, result);
                    MfvPreviewSmokeFixtureBuilder.ResetFixtureForRuntime(context);
                    player.EvaluateAt(MfvPreviewSmokeFixtureBuilder.PreviewTime);
                    var runtime = MfvPreviewSmokeFixtureBuilder.FixtureState.Capture(context.Fixture);
                    diagnostics = MfvPreviewSmokeFixtureBuilder.BuildBakeDiagnostics(preview, result, runtime, context);

                    AssertFixtureClose(preview, runtime, "Runtime player should match real Timeline preview at the sampled time.", diagnostics);
                }
                finally
                {
                    Object.DestroyImmediate(playerObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(settings);
                MfvPreviewSmokeFixtureBuilder.CleanBakeOutputRoot();
            }
        }

#if UDONSHARP
        [Test]
        public void Level4_BakeMenu_CreatesUploadReadyUdonSharpPlayer()
        {
            var context = MfvPreviewSmokeFixtureBuilder.OpenFreshScene();
            AssertPreviewContext(context);

            var hadDefaultBakeRoot = AssetDatabase.IsValidFolder(DefaultBakeRoot);
            var hadDefaultBakeOutput = AssetDatabase.IsValidFolder(MfvBakeUtility.DefaultOutputFolder);
            var hadProgramAssetFolder = AssetDatabase.IsValidFolder(MfvBakeUtility.UdonSharpProgramAssetFolder);
            var hadProgramAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(MfvBakeUtility.UdonSharpPlayerProgramAssetPath) != null;
            string generatedSerializedProgramPath = null;

            try
            {
                Selection.activeGameObject = context.Director.gameObject;
                MfvBakeMenu.BakeSelectedDirector();

                var programAsset = UdonSharpProgramAsset.GetProgramAssetForClass(typeof(MfvVRSLTimelinePlayer));
                Assert.NotNull(programAsset, "Bake menu did not create a UdonSharpProgramAsset for MfvVRSLTimelinePlayer.");
                if (!hadProgramAsset && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out var programGuid, out long _))
                {
                    generatedSerializedProgramPath = $"Assets/SerializedUdonPrograms/{programGuid}.asset";
                }

                var playerTransform = context.Director.transform.Find(MenuPlayerName);
                Assert.NotNull(playerTransform, "Bake menu did not create the ManeuverForVRC baked player.");

                var player = playerTransform.GetComponent<MfvVRSLTimelinePlayer>();
                Assert.NotNull(player, "Baked player is missing MfvVRSLTimelinePlayer.");
                Assert.That(player.fixtures, Has.Length.EqualTo(1), "Baked player should reference the baked VRSL fixture.");
                Assert.That(player.keyTimes, Is.Not.Empty, "Baked player should receive continuous key data.");

                var backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(player);
                Assert.NotNull(backingBehaviour, "Baked player has no backing UdonBehaviour.");
                Assert.IsTrue(
                    UdonSharpEditorUtility.IsUdonSharpBehaviour(backingBehaviour),
                    "Baked player's backing UdonBehaviour is not associated with a valid U# program asset.");
                Assert.NotNull(backingBehaviour.programSource, "Baked player's Udon Program Source is not assigned.");
                Assert.NotZero(backingBehaviour.ProgramId, "Baked player's serialized Udon program is not assigned.");
            }
            finally
            {
                Selection.activeObject = null;

                if (!hadProgramAsset && string.IsNullOrEmpty(generatedSerializedProgramPath))
                {
                    var programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(MfvBakeUtility.UdonSharpPlayerProgramAssetPath);
                    if (programAsset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out var programGuid, out long _))
                    {
                        generatedSerializedProgramPath = $"Assets/SerializedUdonPrograms/{programGuid}.asset";
                    }
                }

                if (!hadDefaultBakeOutput && AssetDatabase.IsValidFolder(MfvBakeUtility.DefaultOutputFolder))
                {
                    AssetDatabase.DeleteAsset(MfvBakeUtility.DefaultOutputFolder);
                }

                if (!hadProgramAsset && AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(MfvBakeUtility.UdonSharpPlayerProgramAssetPath) != null)
                {
                    AssetDatabase.DeleteAsset(MfvBakeUtility.UdonSharpPlayerProgramAssetPath);
                }

                if (!string.IsNullOrEmpty(generatedSerializedProgramPath) && AssetDatabase.LoadAssetAtPath<Object>(generatedSerializedProgramPath) != null)
                {
                    AssetDatabase.DeleteAsset(generatedSerializedProgramPath);
                }

                if (!hadProgramAssetFolder && AssetDatabase.IsValidFolder(MfvBakeUtility.UdonSharpProgramAssetFolder))
                {
                    AssetDatabase.DeleteAsset(MfvBakeUtility.UdonSharpProgramAssetFolder);
                }

                if (!hadDefaultBakeRoot && AssetDatabase.IsValidFolder(DefaultBakeRoot))
                {
                    AssetDatabase.DeleteAsset(DefaultBakeRoot);
                }
            }
        }
#endif

        private static MfvBakeResult BakePreviewFixture(
            MfvPreviewSmokeFixtureBuilder.FixtureContext context,
            MfvBakeSettings settings,
            string outputFolder)
        {
            return MfvBakeUtility.Bake(context.Director, settings, outputFolder);
        }

        private static void AssertPreviewContext(MfvPreviewSmokeFixtureBuilder.FixtureContext context)
        {
            Assert.NotNull(context.Director, "PreviewSmoke Director was not found.");
            Assert.NotNull(context.Director.playableAsset, "PreviewSmoke Director has no playableAsset.");
            Assert.NotNull(context.Timeline, "PreviewSmoke playableAsset is not a TimelineAsset.");
            Assert.NotNull(context.SlmTrack, "PreviewSmoke Timeline has no StageLightTimelineTrack.");
            Assert.NotNull(context.SlmClip, "PreviewSmoke SLM track has no StageLightTimelineClip.");
            Assert.NotNull(context.StageLightFixture, "PreviewSmoke SLM track is not bound to a StageLightFixture.");
            Assert.NotNull(context.Channel, "PreviewSmoke Fixture has no MfvVRSLFixtureChannel.");
            Assert.NotNull(context.Channel.vrslFixture, "PreviewSmoke channel.vrslFixture is null.");
            Assert.NotNull(context.ActivationTrack, "PreviewSmoke Timeline has no retained ActivationTrack.");
            Assert.NotNull(context.AnimationTrack, "PreviewSmoke Timeline has no retained AnimationTrack.");
            Assert.NotNull(context.SlmClip.StageLightQueueData.TryGetActiveProperty<ClockProperty>(),
                "PreviewSmoke SLM queue has no active ClockProperty; MfvVRSLFrameEvaluator will ignore the queue.");
        }

        private static void AssertFixtureClose(
            MfvPreviewSmokeFixtureBuilder.FixtureState expected,
            MfvPreviewSmokeFixtureBuilder.FixtureState actual,
            string reason,
            string diagnostics)
        {
            Assert.That(actual.Intensity, Is.EqualTo(expected.Intensity).Within(0.02f), reason + "\n" + diagnostics);
            Assert.That(actual.Color.r, Is.EqualTo(expected.Color.r).Within(2f / 255f + 0.01f), reason + "\n" + diagnostics);
            Assert.That(actual.Color.g, Is.EqualTo(expected.Color.g).Within(2f / 255f + 0.01f), reason + "\n" + diagnostics);
            Assert.That(actual.Color.b, Is.EqualTo(expected.Color.b).Within(2f / 255f + 0.01f), reason + "\n" + diagnostics);
            Assert.That(actual.ConeWidth, Is.EqualTo(expected.ConeWidth).Within(0.02f), reason + "\n" + diagnostics);
            Assert.That(actual.ConeLength, Is.EqualTo(expected.ConeLength).Within(0.02f), reason + "\n" + diagnostics);
            Assert.That(actual.Pan, Is.EqualTo(expected.Pan).Within(0.5f), reason + "\n" + diagnostics);
            Assert.That(actual.Tilt, Is.EqualTo(expected.Tilt).Within(0.5f), reason + "\n" + diagnostics);
            Assert.That(actual.Gobo, Is.EqualTo(expected.Gobo), reason + "\n" + diagnostics);
            Assert.That(actual.EnableDmx, Is.EqualTo(expected.EnableDmx), reason + "\n" + diagnostics);
            Assert.That(actual.EnableStrobe, Is.EqualTo(expected.EnableStrobe), reason + "\n" + diagnostics);
        }

        private static void AssertClockOverridesInitialized(StageLightTimelineClip clip)
        {
            foreach (var property in clip.StageLightQueueData.stageLightProperties.OfType<SlmAdditionalProperty>())
            {
                Assert.NotNull(property.clockOverride, $"{property.propertyName} clockOverride should be initialized.");
                Assert.NotNull(property.clockOverride.value, $"{property.propertyName} clockOverride.value should be initialized.");
                Assert.NotNull(property.clockOverride.value.arrayStaggerValue, $"{property.propertyName} clockOverride array stagger should be initialized.");
            }
        }

        private static void AssertFixtureChanged(
            MfvPreviewSmokeFixtureBuilder.FixtureState before,
            MfvPreviewSmokeFixtureBuilder.FixtureState after,
            string diagnostics)
        {
            Assert.That(Mathf.Abs(after.Pan - before.Pan), Is.GreaterThan(0.001f), "Preview should change pan.\n" + diagnostics);
            Assert.That(Mathf.Abs(after.Tilt - before.Tilt), Is.GreaterThan(0.001f), "Preview should change tilt.\n" + diagnostics);
            Assert.That(Mathf.Abs(after.Intensity - before.Intensity), Is.GreaterThan(0.001f), "Preview should change intensity.\n" + diagnostics);
            Assert.That(Mathf.Abs(after.ConeWidth - before.ConeWidth), Is.GreaterThan(0.001f), "Preview should change coneWidth.\n" + diagnostics);
            Assert.That(Mathf.Abs(after.ConeLength - before.ConeLength), Is.GreaterThan(0.001f), "Preview should change coneLength.\n" + diagnostics);
            Assert.That(after.Gobo, Is.Not.EqualTo(before.Gobo), "Preview should change gobo.\n" + diagnostics);
            Assert.That(after.EnableDmx, Is.Not.EqualTo(before.EnableDmx), "Preview should disable DMX channels.\n" + diagnostics);
            Assert.That(after.EnableStrobe, Is.Not.EqualTo(before.EnableStrobe), "Preview should disable strobe.\n" + diagnostics);
        }

        private static TrackAsset[] GetAllTracks(TimelineAsset timeline)
        {
            return timeline.GetRootTracks()
                .Concat(timeline.GetOutputTracks())
                .Concat(timeline.outputs.Select(output => output.sourceObject).OfType<TrackAsset>())
                .Distinct()
                .ToArray();
        }

        private static void AssertRetainedTrackClips(
            TrackAsset sourceTrack,
            TrackAsset uploadTrack,
            double expectedStart,
            double expectedDuration,
            string trackName,
            string diagnostics)
        {
            Assert.NotNull(sourceTrack, $"Source {trackName} is missing.\n{diagnostics}");
            Assert.NotNull(uploadTrack, $"Upload Timeline is missing {trackName}.\n{diagnostics}");

            var sourceClips = sourceTrack.GetClips().ToArray();
            var uploadClips = uploadTrack.GetClips().ToArray();
            Assert.That(sourceClips, Is.Not.Empty, $"Source {trackName} has no clips.\n{diagnostics}");
            Assert.That(uploadClips, Has.Length.EqualTo(sourceClips.Length), $"Upload {trackName} clip count changed.\n{diagnostics}");
            Assert.That(uploadClips[0].start, Is.EqualTo(expectedStart).Within(0.001), $"Upload {trackName} clip start changed.\n{diagnostics}");
            Assert.That(uploadClips[0].duration, Is.EqualTo(expectedDuration).Within(0.001), $"Upload {trackName} clip duration changed.\n{diagnostics}");
            Assert.NotNull(uploadClips[0].asset, $"Upload {trackName} clip asset was lost.\n{diagnostics}");
        }

        private static void AssertAnimationClipCurvesRetained(AnimationTrack uploadAnimation, string diagnostics)
        {
            var timelineClip = uploadAnimation.GetClips().FirstOrDefault();
            var playableAsset = timelineClip != null ? timelineClip.asset as AnimationPlayableAsset : null;
            Assert.NotNull(playableAsset, "Upload AnimationTrack clip is not an AnimationPlayableAsset.\n" + diagnostics);
            Assert.NotNull(playableAsset.clip, "Upload AnimationTrack lost its AnimationClip.\n" + diagnostics);
            Assert.That(AnimationUtility.GetCurveBindings(playableAsset.clip), Is.Not.Empty, "Upload AnimationTrack AnimationClip lost its curves.\n" + diagnostics);
        }
    }
}
