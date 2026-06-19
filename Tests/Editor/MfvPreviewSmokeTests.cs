using System.Collections;
using System.Linq;
using ManeuverForVRSL.Editor;
using NUnit.Framework;
using StageLightManeuver;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace ManeuverForVRSL.Tests
{
    public class MfvPreviewSmokeTests
    {
        [UnityTest]
        public IEnumerator Level3_RealTimelinePreview_UpdatesChannelAndVrslFixture()
        {
            var context = MfvPreviewSmokeFixtureBuilder.OpenFreshScene();
            AssertPreviewContext(context);

            var sample = MfvPreviewSmokeFixtureBuilder.EvaluatePreview(context, MfvPreviewSmokeFixtureBuilder.PreviewTime);
            yield return null;

            var diagnostics = MfvPreviewSmokeFixtureBuilder.BuildDiagnostics(context, sample.Before, sample.After);
            Assert.That(context.Channel.lastFrame.intensity, Is.EqualTo(MfvPreviewSmokeFixtureBuilder.ExpectedIntensity).Within(0.0001f), diagnostics);
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
        public void Level4_BakeConsistency_MatchesPreviewAndKeepsUploadTimeline()
        {
            var context = MfvPreviewSmokeFixtureBuilder.OpenFreshScene();
            AssertPreviewContext(context);

            var preview = MfvPreviewSmokeFixtureBuilder
                .EvaluatePreview(context, MfvPreviewSmokeFixtureBuilder.PreviewTime)
                .After;
            var stateBeforeBake = preview;

            var settings = MfvBakeSettings.CreateDefault();
            settings.internalSampleRate = 30f;
            var result = BakeIgnoringExternalSdkImportNoise(context, settings);
            Object.DestroyImmediate(settings);

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

            var uploadTracks = result.uploadTimeline.GetOutputTracks().Concat(result.uploadTimeline.GetRootTracks()).ToArray();
            Assert.IsFalse(uploadTracks.Any(track => track is StageLightTimelineTrack), diagnostics);
            Assert.IsTrue(uploadTracks.Any(track => track is ActivationTrack), diagnostics);
            Assert.IsTrue(uploadTracks.Any(track => track is AnimationTrack), diagnostics);
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

        private static MfvBakeResult BakeIgnoringExternalSdkImportNoise(
            MfvPreviewSmokeFixtureBuilder.FixtureContext context,
            MfvBakeSettings settings)
        {
            var ignoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                return MfvBakeUtility.Bake(context.Director, settings, MfvPreviewSmokeFixtureBuilder.FolderPath + "/Baked");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoreFailingMessages;
            }
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
    }
}
