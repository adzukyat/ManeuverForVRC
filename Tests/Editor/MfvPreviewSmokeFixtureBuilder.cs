using System;
using System.Collections.Generic;
using System.Linq;
using ManeuverForVRSL.Editor;
using StageLightManeuver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using VRSL;
using Object = UnityEngine.Object;

namespace ManeuverForVRSL.Tests
{
    internal static class MfvPreviewSmokeFixtureBuilder
    {
        public const string FolderPath = "Assets/MfvTestFixtures";
        public const string ScenePath = FolderPath + "/PreviewSmoke.unity";
        public const string TimelinePath = FolderPath + "/PreviewSmoke.playable";
        public const float PreviewTime = 1f;
        public const float ExpectedPan = 30f;
        public const float ExpectedTilt = 60f;
        public const float ExpectedIntensity = 0.7f;
        public const float ExpectedConeWidth = 2.75f;
        public const float ExpectedConeLength = 5.25f;
        public const int ExpectedGobo = 4;
        public static readonly Color ExpectedColor = new Color(0.25f, 0.5f, 1f, 1f);

        [MenuItem("ManeuverForVRSL/Tests/Regenerate Preview Smoke Fixture")]
        public static void RegenerateAssets()
        {
            EnsureFolder(FolderPath);
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.DeleteAsset(TimelinePath);

            var timeline = CreateTimelineAsset(out var slmTrack);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneObjects(timeline, slmTrack);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void EnsureAssets()
        {
            if (HasCompleteAssets())
            {
                return;
            }

            var ignoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                RegenerateAssets();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoreFailingMessages;
            }
        }

        public static FixtureContext OpenFreshScene()
        {
            EnsureAssets();
            var ignoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoreFailingMessages;
            }

            return FindContext();
        }

        public static PreviewSample EvaluatePreview(FixtureContext context, float time)
        {
            var before = FixtureState.Capture(context.Fixture);
            context.Director.time = time;
            context.Director.Evaluate();
            var after = FixtureState.Capture(context.Fixture);
            return new PreviewSample(before, after);
        }

        public static string BuildDiagnostics(FixtureContext context, FixtureState before, FixtureState after)
        {
            var directorExists = context.Director != null;
            var assetPath = context.Timeline != null ? AssetDatabase.GetAssetPath(context.Timeline) : "<missing>";
            var slmTrackCount = context.Timeline != null
                ? GetAllTracks(context.Timeline).Count(track => track is StageLightTimelineTrack)
                : 0;
            var binding = context.Director != null && context.SlmTrack != null
                ? context.Director.GetGenericBinding(context.SlmTrack)
                : null;
            var hasClock = context.SlmClip != null &&
                context.SlmClip.StageLightQueueData.TryGetActiveProperty<ClockProperty>() != null;

            return string.Join(
                "\n",
                $"director exists: {directorExists}",
                $"director.playableAsset: {(context.Director != null ? context.Director.playableAsset : null)}",
                $"timeline name/path: {(context.Timeline != null ? context.Timeline.name : "<missing>")} / {assetPath}",
                $"SLM track count: {slmTrackCount}",
                $"binding object: {binding}",
                $"channel exists: {context.Channel != null}",
                $"channel.vrslFixture assigned: {(context.Channel != null && context.Channel.vrslFixture != null)}",
                $"SLM clip has ClockProperty: {hasClock}",
                $"channel.lastFrame before/after: {context.LastFrameBefore} -> {(context.Channel != null ? FormatFrame(context.Channel.lastFrame) : "<missing>")}",
                $"fixture fields before: {before}",
                $"fixture fields after: {after}");
        }

        public static string BuildBakeDiagnostics(
            FixtureState preview,
            MfvBakeResult result,
            FixtureState runtime,
            FixtureContext context)
        {
            var asset = result != null ? result.bakedAsset : null;
            var uploadTracks = result != null && result.uploadTimeline != null
                ? string.Join(", ", result.uploadTimeline.GetOutputTracks().Select(track => track.GetType().Name + ":" + track.name))
                : "<missing>";

            return string.Join(
                "\n",
                BuildDiagnostics(context, preview, runtime),
                $"preview values: {preview}",
                $"runtime values: {runtime}",
                $"baked asset exists: {asset != null}",
                $"baked fixtures: {(result != null && result.fixtures != null ? result.fixtures.Length : -1)}",
                $"continuous tracks/key arrays: {asset?.ContinuousTrackCount ?? -1} / {asset?.keyTimes?.Length ?? -1} / {asset?.keyValues?.Length ?? -1}",
                $"event tracks/event arrays: {asset?.EventTrackCount ?? -1} / {asset?.eventTimes?.Length ?? -1} / {asset?.eventValues?.Length ?? -1}",
                $"uploadTimeline tracks: {uploadTracks}");
        }

        public static FixtureState ResetFixtureForRuntime(FixtureContext context)
        {
            context.Fixture.enableDMXChannels = true;
            context.Fixture.enableStrobe = true;
            context.Fixture.panOffsetBlueGreen = 0f;
            context.Fixture.tiltOffsetBlue = 90f;
            context.Fixture.globalIntensity = 0f;
            context.Fixture.lightColorTint = Color.black;
            context.Fixture.coneWidth = 0f;
            context.Fixture.coneLength = 0.5f;
            context.Fixture.selectGOBO = 1;
            context.Fixture._UpdateInstancedProperties();
            return FixtureState.Capture(context.Fixture);
        }

        private static TimelineAsset CreateTimelineAsset(out StageLightTimelineTrack slmTrack)
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "PreviewSmoke";
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = 2.0;

            slmTrack = timeline.CreateTrack<StageLightTimelineTrack>(null, "PreviewSmoke SLM");
            var clip = slmTrack.CreateClip<StageLightTimelineClip>();
            clip.displayName = "PreviewSmoke Cue";
            clip.start = 0.0;
            clip.duration = 2.0;
            ConfigureStageLightClip((StageLightTimelineClip)clip.asset);

            timeline.CreateTrack<ActivationTrack>(null, "Activation Retained");
            timeline.CreateTrack<AnimationTrack>(null, "Animation Retained");
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void ConfigureStageLightClip(StageLightTimelineClip clip)
        {
            clip.behaviour.stageLightQueueData.stageLightProperties = new List<SlmProperty>
            {
                new ClockProperty(),
                new StageLightOrderProperty(),
                CreateIntensity(7f),
                CreateColor(ExpectedColor),
                CreateLight(90f, 50f),
                CreatePan(ExpectedPan),
                CreateTilt(ExpectedTilt),
                CreateGobo(ExpectedGobo)
            };
        }

        private static void CreateSceneObjects(TimelineAsset timeline, StageLightTimelineTrack slmTrack)
        {
            var directorObject = new GameObject("PreviewSmoke Director");
            var director = directorObject.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.extrapolationMode = DirectorWrapMode.Hold;

            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fixtureObject.name = "Fixture";
            var renderer = fixtureObject.GetComponent<MeshRenderer>();
            var stageLightFixture = fixtureObject.AddComponent<StageLightFixture>();
            var channel = fixtureObject.AddComponent<MfvVRSLFixtureChannel>();
            var vrslFixture = fixtureObject.AddComponent<VRStageLighting_DMX_Static>();
            vrslFixture.objRenderers = new[] { renderer };
            vrslFixture.enableDMXChannels = true;
            vrslFixture.enableStrobe = true;
            vrslFixture.globalIntensity = 0f;
            vrslFixture.lightColorTint = Color.black;
            vrslFixture.coneWidth = 0f;
            vrslFixture.coneLength = 0.5f;
            vrslFixture.selectGOBO = 1;
            channel.vrslFixture = vrslFixture;
            stageLightFixture.Init();

            if (slmTrack == null)
            {
                throw new InvalidOperationException("PreviewSmoke timeline does not contain a StageLightTimelineTrack after creation.");
            }

            director.SetGenericBinding(slmTrack, stageLightFixture);
        }

        private static FixtureContext FindContext()
        {
            var director = Object.FindObjectOfType<PlayableDirector>();
            var timeline = director != null ? director.playableAsset as TimelineAsset : null;
            var slmTrack = timeline != null ? FindSlmTrack(timeline) : null;
            var slmClip = slmTrack != null
                ? slmTrack.GetClips().Select(clip => clip.asset as StageLightTimelineClip).FirstOrDefault(clip => clip != null)
                : null;
            var binding = director != null && slmTrack != null
                ? director.GetGenericBinding(slmTrack) as StageLightFixture
                : null;
            var channel = binding != null ? binding.GetComponent<MfvVRSLFixtureChannel>() : null;
            var fixture = channel != null ? channel.vrslFixture : null;

            return new FixtureContext(director, timeline, slmTrack, slmClip, binding, channel, fixture);
        }

        private static bool HasCompleteAssets()
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            return timeline != null && FindSlmTrack(timeline) != null && System.IO.File.Exists(ScenePath);
        }

        private static IEnumerable<TrackAsset> GetAllTracks(TimelineAsset timeline)
        {
            return timeline.GetRootTracks()
                .Concat(timeline.GetOutputTracks())
                .Concat(timeline.outputs.Select(output => output.sourceObject).OfType<TrackAsset>())
                .Distinct();
        }

        private static StageLightTimelineTrack FindSlmTrack(TimelineAsset timeline)
        {
            return GetAllTracks(timeline).OfType<StageLightTimelineTrack>().SingleOrDefault();
        }

        private static LightIntensityProperty CreateIntensity(float value)
        {
            var property = new LightIntensityProperty();
            property.lightToggleIntensity.value.mode = StageLightManeuver.AnimationMode.Constant;
            property.lightToggleIntensity.value.constant = value;
            return property;
        }

        private static LightColorProperty CreateColor(Color color)
        {
            var property = new LightColorProperty();
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(color.a, 1f)
                });
            property.lightToggleColor.value = gradient;
            return property;
        }

        private static LightProperty CreateLight(float spotAngle, float range)
        {
            var property = new LightProperty();
            property.spotAngle.value.mode = StageLightManeuver.AnimationMode.Constant;
            property.spotAngle.value.constant = spotAngle;
            property.range.value.mode = StageLightManeuver.AnimationMode.Constant;
            property.range.value.constant = range;
            return property;
        }

        private static PanProperty CreatePan(float value)
        {
            var property = new PanProperty();
            property.rollTransform.value.mode = StageLightManeuver.AnimationMode.Constant;
            property.rollTransform.value.constant = value;
            return property;
        }

        private static TiltProperty CreateTilt(float value)
        {
            var property = new TiltProperty();
            property.rollTransform.value.mode = StageLightManeuver.AnimationMode.Constant;
            property.rollTransform.value.constant = value;
            return property;
        }

        private static MfvVRSLGoboProperty CreateGobo(int value)
        {
            var property = new MfvVRSLGoboProperty();
            property.goboIndex.value = value;
            return property;
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string FormatFrame(MfvVRSLFrame frame)
        {
            return $"pan={frame.pan:0.###}, tilt={frame.tilt:0.###}, intensity={frame.intensity:0.###}, color={frame.color}, coneWidth={frame.coneWidth:0.###}, coneLength={frame.coneLength:0.###}, gobo={frame.gobo}";
        }

        internal sealed class FixtureContext
        {
            public readonly PlayableDirector Director;
            public readonly TimelineAsset Timeline;
            public readonly StageLightTimelineTrack SlmTrack;
            public readonly StageLightTimelineClip SlmClip;
            public readonly StageLightFixture StageLightFixture;
            public readonly MfvVRSLFixtureChannel Channel;
            public readonly VRStageLighting_DMX_Static Fixture;
            public readonly string LastFrameBefore;

            public FixtureContext(
                PlayableDirector director,
                TimelineAsset timeline,
                StageLightTimelineTrack slmTrack,
                StageLightTimelineClip slmClip,
                StageLightFixture stageLightFixture,
                MfvVRSLFixtureChannel channel,
                VRStageLighting_DMX_Static fixture)
            {
                Director = director;
                Timeline = timeline;
                SlmTrack = slmTrack;
                SlmClip = slmClip;
                StageLightFixture = stageLightFixture;
                Channel = channel;
                Fixture = fixture;
                LastFrameBefore = channel != null ? FormatFrame(channel.lastFrame) : "<missing>";
            }
        }

        internal readonly struct PreviewSample
        {
            public readonly FixtureState Before;
            public readonly FixtureState After;

            public PreviewSample(FixtureState before, FixtureState after)
            {
                Before = before;
                After = after;
            }
        }

        internal readonly struct FixtureState
        {
            public readonly bool EnableDmx;
            public readonly bool EnableStrobe;
            public readonly float Pan;
            public readonly float Tilt;
            public readonly float Intensity;
            public readonly Color Color;
            public readonly float ConeWidth;
            public readonly float ConeLength;
            public readonly int Gobo;

            private FixtureState(VRStageLighting_DMX_Static fixture)
            {
                EnableDmx = fixture.enableDMXChannels;
                EnableStrobe = fixture.enableStrobe;
                Pan = fixture.panOffsetBlueGreen;
                Tilt = fixture.tiltOffsetBlue;
                Intensity = fixture.globalIntensity;
                Color = fixture.lightColorTint;
                ConeWidth = fixture.coneWidth;
                ConeLength = fixture.coneLength;
                Gobo = fixture.selectGOBO;
            }

            public static FixtureState Capture(VRStageLighting_DMX_Static fixture)
            {
                return new FixtureState(fixture);
            }

            public override string ToString()
            {
                return $"dmx={EnableDmx}, strobe={EnableStrobe}, pan={Pan:0.###}, tilt={Tilt:0.###}, intensity={Intensity:0.###}, color={Color}, coneWidth={ConeWidth:0.###}, coneLength={ConeLength:0.###}, gobo={Gobo}";
            }
        }
    }
}
