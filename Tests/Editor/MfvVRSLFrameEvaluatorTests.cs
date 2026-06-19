using ManeuverForVRSL;
using NUnit.Framework;
using StageLightManeuver;
using UnityEngine;

namespace ManeuverForVRSL.Tests
{
    public class MfvVRSLFrameEvaluatorTests
    {
        [Test]
        public void Evaluate_MapsSlmLightValuesToVrslRanges()
        {
            var fixtureObject = new GameObject("fixture");
            try
            {
                var fixture = fixtureObject.AddComponent<StageLightFixture>();
                fixture.order = 0;
                var queueData = new StageLightQueueData();
                queueData.stageLightProperties.Add(new ClockProperty());
                queueData.stageLightProperties.Add(CreateIntensity(10f));
                queueData.stageLightProperties.Add(CreateLight(180f, 100f));
                queueData.stageLightProperties.Add(CreateColor(Color.red));

                var frame = MfvVRSLFrameEvaluator.Evaluate(new[] { queueData }, fixture, 0f);

                Assert.That(frame.intensity, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(frame.coneWidth, Is.EqualTo(5.5f).Within(0.0001f));
                Assert.That(frame.coneLength, Is.EqualTo(10f).Within(0.0001f));
                Assert.That(frame.color.r, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void Evaluate_LightFlickerIsDeterministic()
        {
            var fixtureObject = new GameObject("fixture");
            try
            {
                var fixture = fixtureObject.AddComponent<StageLightFixture>();
                fixture.order = 0;
                var queueData = new StageLightQueueData();
                queueData.stageLightProperties.Add(new ClockProperty());
                queueData.stageLightProperties.Add(CreateIntensity(5f));
                queueData.stageLightProperties.Add(new LightFlickerProperty());

                var frameA = MfvVRSLFrameEvaluator.Evaluate(new[] { queueData }, fixture, 0.25f);
                var frameB = MfvVRSLFrameEvaluator.Evaluate(new[] { queueData }, fixture, 0.25f);

                Assert.That(frameA.intensity, Is.EqualTo(frameB.intensity).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(fixtureObject);
            }
        }

        private static LightIntensityProperty CreateIntensity(float value)
        {
            var property = new LightIntensityProperty();
            property.lightToggleIntensity.value.mode = AnimationMode.Constant;
            property.lightToggleIntensity.value.constant = value;
            return property;
        }

        private static LightProperty CreateLight(float spotAngle, float range)
        {
            var property = new LightProperty();
            property.spotAngle.value.mode = AnimationMode.Constant;
            property.spotAngle.value.constant = spotAngle;
            property.range.value.mode = AnimationMode.Constant;
            property.range.value.constant = range;
            return property;
        }

        private static LightColorProperty CreateColor(Color color)
        {
            var property = new LightColorProperty();
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            property.lightToggleColor.value = gradient;
            return property;
        }
    }
}
