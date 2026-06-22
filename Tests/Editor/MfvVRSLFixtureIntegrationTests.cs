using System.Collections.Generic;
using NUnit.Framework;
using StageLightManeuver;
using UnityEngine;
using VRSL;
using Object = UnityEngine.Object;

namespace ManeuverForVRC.Tests
{
    public class MfvVRSLFixtureIntegrationTests
    {
        [Test]
        public void EvaluateQue_UpdatesLastFrameBeforeFixtureApply()
        {
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var stageFixture = fixtureObject.AddComponent<StageLightFixture>();
                var channel = fixtureObject.AddComponent<MfvVRSLFixtureChannel>();
                var vrslFixture = fixtureObject.AddComponent<VRStageLighting_DMX_Static>();
                vrslFixture.objRenderers = new[] { fixtureObject.GetComponent<MeshRenderer>() };
                vrslFixture.globalIntensity = 0f;
                channel.vrslFixture = vrslFixture;
                stageFixture.Init();

                stageFixture.AddQue(CreateQueueData());
                stageFixture.EvaluateQue(0.5f);

                Assert.That(channel.lastFrame.intensity, Is.EqualTo(0.5f).Within(0.0001f),
                    "EvaluateQue should consume SLM queue data and update channel.lastFrame.");
                Assert.That(vrslFixture.globalIntensity, Is.EqualTo(0f).Within(0.0001f),
                    "EvaluateQue should not mutate the VRSL fixture until UpdateChannel/Apply runs.");

                stageFixture.UpdateChannel();

                Assert.That(vrslFixture.globalIntensity, Is.EqualTo(0.5f).Within(0.0001f),
                    "UpdateChannel should apply channel.lastFrame to the assigned VRSL fixture.");
                Assert.IsFalse(vrslFixture.enableDMXChannels);
                Assert.IsFalse(vrslFixture.enableStrobe);
            }
            finally
            {
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void Apply_UpdatesRepresentativeVrslFixtureFields()
        {
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var fixture = fixtureObject.AddComponent<VRStageLighting_DMX_Static>();
                fixture.objRenderers = new[] { fixtureObject.GetComponent<MeshRenderer>() };
                fixture.enableDMXChannels = true;
                fixture.enableStrobe = true;

                var applied = MfvVRSLFixtureApplier.TryApply(fixture, new MfvVRSLFrame
                {
                    pan = 12f,
                    tilt = 34f,
                    intensity = 0.6f,
                    color = Color.green,
                    coneWidth = 2f,
                    coneLength = 6f,
                    gobo = 5
                });

                Assert.IsTrue(applied);
                Assert.That(fixture.panOffsetBlueGreen, Is.EqualTo(-12f).Within(0.0001f));
                Assert.That(fixture.tiltOffsetBlue, Is.EqualTo(124f).Within(0.0001f));
                Assert.That(fixture.globalIntensity, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(fixture.lightColorTint, Is.EqualTo(Color.green));
                Assert.That(fixture.coneWidth, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(fixture.coneLength, Is.EqualTo(6f).Within(0.0001f));
                Assert.That(fixture.selectGOBO, Is.EqualTo(5));
                Assert.IsFalse(fixture.enableDMXChannels);
                Assert.IsFalse(fixture.enableStrobe);
            }
            finally
            {
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void Apply_MapsSlmTiltZeroToVrslDefaultTiltOffset()
        {
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var fixture = fixtureObject.AddComponent<VRStageLighting_DMX_Static>();
                fixture.objRenderers = new[] { fixtureObject.GetComponent<MeshRenderer>() };

                var applied = MfvVRSLFixtureApplier.TryApply(fixture, new MfvVRSLFrame
                {
                    tilt = 0f
                });

                Assert.IsTrue(applied);
                Assert.That(fixture.tiltOffsetBlue, Is.EqualTo(90f).Within(0.0001f),
                    "SLM Tilt 0 should land on VRSL's default vertical tilt offset.");
            }
            finally
            {
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void InitializeTimelineProperties_UsesVrslFixtureDefaultsForFreshClip()
        {
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var stageFixture = fixtureObject.AddComponent<StageLightFixture>();
                var channel = fixtureObject.AddComponent<MfvVRSLFixtureChannel>();
                var vrslFixture = fixtureObject.AddComponent<VRStageLighting_DMX_Static>();
                vrslFixture.objRenderers = new[] { fixtureObject.GetComponent<MeshRenderer>() };
                vrslFixture.panOffsetBlueGreen = 12f;
                vrslFixture.tiltOffsetBlue = 34f;
                vrslFixture.globalIntensity = 0.65f;
                vrslFixture.lightColorTint = new Color(0.2f, 0.4f, 0.8f, 1f);
                vrslFixture.coneWidth = 2.2f;
                vrslFixture.coneLength = 7f;
                vrslFixture.selectGOBO = 6;
                channel.vrslFixture = vrslFixture;
                stageFixture.Init();

                var queueData = CreateFreshQueueData();
                stageFixture.InitializeTimelineProperties(queueData);

                var intensity = queueData.TryGetActiveProperty<LightIntensityProperty>();
                var color = queueData.TryGetActiveProperty<LightColorProperty>();
                var light = queueData.TryGetActiveProperty<LightProperty>();
                var pan = queueData.TryGetActiveProperty<PanProperty>();
                var tilt = queueData.TryGetActiveProperty<TiltProperty>();
                var gobo = queueData.TryGetActiveProperty<MfvVRSLGoboProperty>();

                Assert.NotNull(intensity);
                Assert.NotNull(color);
                Assert.NotNull(light);
                Assert.NotNull(pan);
                Assert.NotNull(tilt);
                Assert.NotNull(gobo);
                Assert.IsNull(queueData.TryGetActiveProperty<LightFlickerProperty>());
                Assert.IsNull(queueData.TryGetActiveProperty<ManualLightArrayProperty>());
                Assert.IsNull(queueData.TryGetActiveProperty<ManualColorArrayProperty>());
                Assert.IsNull(queueData.TryGetActiveProperty<ManualPanTiltProperty>());
                Assert.That(intensity.lightToggleIntensity.value.constant, Is.EqualTo(6.5f).Within(0.0001f));
                Assert.That(light.spotAngle.value.constant, Is.EqualTo(72f).Within(0.0001f));
                Assert.That(light.range.value.constant, Is.EqualTo(68.42105f).Within(0.0001f));
                Assert.That(color.lightToggleColor.value.Evaluate(0f), Is.EqualTo(vrslFixture.lightColorTint));
                Assert.That(pan.rollTransform.value.constant, Is.EqualTo(-12f).Within(0.0001f));
                Assert.That(tilt.rollTransform.value.constant, Is.EqualTo(-56f).Within(0.0001f));
                Assert.That(gobo.goboIndex.value, Is.EqualTo(6));

                var frame = MfvVRSLFrameEvaluator.Evaluate(new[] { queueData }, stageFixture, 0f);

                Assert.That(frame.pan, Is.EqualTo(-12f).Within(0.0001f));
                Assert.That(frame.tilt, Is.EqualTo(-56f).Within(0.0001f));
                Assert.That(frame.intensity, Is.EqualTo(0.65f).Within(0.0001f));
                Assert.That(frame.color.r, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(frame.color.g, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(frame.color.b, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(frame.coneWidth, Is.EqualTo(2.2f).Within(0.0001f));
                Assert.That(frame.coneLength, Is.EqualTo(7f).Within(0.0001f));
                Assert.That(frame.gobo, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void TryApply_ReturnsFalseForMissingVrslFixture()
        {
            var applied = MfvVRSLFixtureApplier.TryApply(null, MfvVRSLFrame.Default());

            Assert.IsFalse(applied,
                "A missing VRSL fixture must be observable by validation/tests instead of looking like a successful apply.");
        }

        private static StageLightQueueData CreateQueueData()
        {
            var intensity = new LightIntensityProperty();
            intensity.lightToggleIntensity.value.mode = AnimationMode.Constant;
            intensity.lightToggleIntensity.value.constant = 5f;

            return new StageLightQueueData
            {
                stageLightProperties = new List<SlmProperty>
                {
                    new ClockProperty(),
                    new StageLightOrderProperty(),
                    intensity
                }
            };
        }

        private static StageLightQueueData CreateFreshQueueData()
        {
            return new StageLightQueueData
            {
                stageLightProperties = new List<SlmProperty>
                {
                    new ClockProperty(),
                    new StageLightOrderProperty(),
                    new LightIntensityProperty(),
                    new LightColorProperty(),
                    new LightProperty(),
                    new PanProperty(),
                    new TiltProperty(),
                    new MfvVRSLGoboProperty()
                }
            };
        }
    }
}
