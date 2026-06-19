using System.Collections.Generic;
using NUnit.Framework;
using StageLightManeuver;
using UnityEngine;
using VRSL;
using Object = UnityEngine.Object;

namespace ManeuverForVRSL.Tests
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
                Assert.That(fixture.panOffsetBlueGreen, Is.EqualTo(12f).Within(0.0001f));
                Assert.That(fixture.tiltOffsetBlue, Is.EqualTo(34f).Within(0.0001f));
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
    }
}
