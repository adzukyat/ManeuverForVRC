using ManeuverForVRC;
using NUnit.Framework;
using UnityEngine;
using VRSL;

namespace ManeuverForVRC.Tests
{
    public class MfvVRSLTimelinePlayerTests
    {
        [Test]
        public void EvaluateAt_InterpolatesContinuousTrackAndHandlesSeekBack()
        {
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var playerObject = new GameObject("player");
            try
            {
                var fixture = CreateFixture(fixtureObject);
                var player = playerObject.AddComponent<MfvVRSLTimelinePlayer>();
                player.fixtures = new[] { fixture };
                player.trackFixture = new[] { 0 };
                player.trackProperty = new[] { MfvVRSLPropertyId.Intensity };
                player.keyStart = new[] { 0 };
                player.keyCount = new[] { 2 };
                player.keyTimes = new[] { 0f, 1f };
                player.keyValues = new[] { 0f, 1f };

                player.EvaluateAt(0.75f);
                Assert.That(fixture.globalIntensity, Is.EqualTo(0.75f).Within(0.0001f));

                player.EvaluateAt(0.25f);
                Assert.That(fixture.globalIntensity, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.IsFalse(fixture.enableDMXChannels);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void EvaluateAt_AppliesLastGoboEventAtOrBeforeTime()
        {
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var playerObject = new GameObject("player");
            try
            {
                var fixture = CreateFixture(fixtureObject);
                var player = playerObject.AddComponent<MfvVRSLTimelinePlayer>();
                player.fixtures = new[] { fixture };
                player.trackFixture = new int[0];
                player.trackProperty = new int[0];
                player.keyStart = new int[0];
                player.keyCount = new int[0];
                player.keyTimes = new float[0];
                player.keyValues = new float[0];
                player.eventFixture = new[] { 0 };
                player.eventProperty = new[] { MfvVRSLPropertyId.Gobo };
                player.eventStart = new[] { 0 };
                player.eventCount = new[] { 2 };
                player.eventTimes = new[] { 0.2f, 0.8f };
                player.eventValues = new[] { 3, 5 };

                player.EvaluateAt(0.3f);
                Assert.That(fixture.selectGOBO, Is.EqualTo(3));

                player.EvaluateAt(0.9f);
                Assert.That(fixture.selectGOBO, Is.EqualTo(5));

                player.EvaluateAt(0.4f);
                Assert.That(fixture.selectGOBO, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void EvaluateAt_MapsSlmTiltZeroToVrslDefaultTiltOffset()
        {
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var playerObject = new GameObject("player");
            try
            {
                var fixture = CreateFixture(fixtureObject);
                var player = playerObject.AddComponent<MfvVRSLTimelinePlayer>();
                player.fixtures = new[] { fixture };
                player.trackFixture = new[] { 0 };
                player.trackProperty = new[] { MfvVRSLPropertyId.Tilt };
                player.keyStart = new[] { 0 };
                player.keyCount = new[] { 1 };
                player.keyTimes = new[] { 0f };
                player.keyValues = new[] { 0f };

                player.EvaluateAt(0f);

                Assert.That(fixture.tiltOffsetBlue, Is.EqualTo(90f).Within(0.0001f),
                    "Baked SLM Tilt 0 should reproduce VRSL's default vertical tilt offset.");
                Assert.IsFalse(fixture.enableDMXChannels);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void EvaluateAt_InvertsSlmPanForVrslPanOffset()
        {
            var fixtureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var playerObject = new GameObject("player");
            try
            {
                var fixture = CreateFixture(fixtureObject);
                var player = playerObject.AddComponent<MfvVRSLTimelinePlayer>();
                player.fixtures = new[] { fixture };
                player.trackFixture = new[] { 0 };
                player.trackProperty = new[] { MfvVRSLPropertyId.Pan };
                player.keyStart = new[] { 0 };
                player.keyCount = new[] { 1 };
                player.keyTimes = new[] { 0f };
                player.keyValues = new[] { 30f };

                player.EvaluateAt(0f);

                Assert.That(fixture.panOffsetBlueGreen, Is.EqualTo(-30f).Within(0.0001f),
                    "Baked SLM Pan should be inverted when written to VRSL's pan offset.");
                Assert.IsFalse(fixture.enableDMXChannels);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(fixtureObject);
            }
        }

        private static VRStageLighting_DMX_Static CreateFixture(GameObject gameObject)
        {
            var fixture = gameObject.AddComponent<VRStageLighting_DMX_Static>();
            fixture.objRenderers = new[] { gameObject.GetComponent<MeshRenderer>() };
            return fixture;
        }
    }
}
