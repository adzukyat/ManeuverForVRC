using System.Collections.Generic;
using StageLightManeuver;
using UnityEngine;

namespace ManeuverForVRSL
{
    public static class MfvVRSLFrameEvaluator
    {
        public static MfvVRSLFrame Evaluate(IReadOnlyList<StageLightQueueData> queueDataList, StageLightFixture fixture, float currentTime)
        {
            var state = new Accumulator();

            for (var i = 0; i < queueDataList.Count; i++)
            {
                Accumulate(queueDataList[i], fixture, currentTime, ref state);
            }

            return state.ToFrame();
        }

        public static void Accumulate(StageLightQueueData queueData, StageLightFixture fixture, float currentTime, ref Accumulator state)
        {
            if (queueData == null || fixture == null)
            {
                return;
            }

            var clockProperty = queueData.TryGetActiveProperty<ClockProperty>();
            if (clockProperty == null)
            {
                return;
            }

            var weight = queueData.weight;
            var orderProperty = queueData.TryGetActiveProperty<StageLightOrderProperty>();
            var index = orderProperty != null ? orderProperty.stageLightOrderQueue.GetStageLightIndex(fixture) : fixture.order;

            AccumulateLight(queueData, currentTime, index, weight, ref state);
            AccumulateColor(queueData, currentTime, index, weight, ref state);
            AccumulatePanTilt(queueData, currentTime, index, weight, ref state);
            AccumulateGobo(queueData, weight, ref state);
        }

        private static void AccumulateLight(StageLightQueueData queueData, float currentTime, int index, float weight, ref Accumulator state)
        {
            var manualLightArrayProperty = queueData.TryGetActiveProperty<ManualLightArrayProperty>();
            if (manualLightArrayProperty != null)
            {
                var values = manualLightArrayProperty.lightValues.value;
                if (values != null && index >= 0 && index < values.Count && values[index] != null)
                {
                    var lightValue = values[index];
                    state.intensity += lightValue.intensity * weight;
                    state.spotAngle += lightValue.angle * weight;
                    state.range += lightValue.range * weight;
                }

                return;
            }

            var intensityProperty = queueData.TryGetActiveProperty<LightIntensityProperty>();
            if (intensityProperty != null)
            {
                var t = GetNormalizedTimeSafe(currentTime, queueData, typeof(LightIntensityProperty), index);
                state.intensity += intensityProperty.lightToggleIntensity.value.Evaluate(t) * weight;
            }

            var flickerProperty = queueData.TryGetActiveProperty<LightFlickerProperty>();
            if (flickerProperty != null)
            {
                state.intensity *= flickerProperty.GetNoiseValue(currentTime, index) * weight;
            }

            var lightProperty = queueData.TryGetActiveProperty<LightProperty>();
            if (lightProperty != null)
            {
                var t = GetNormalizedTimeSafe(currentTime, queueData, typeof(LightProperty), index);
                state.spotAngle += lightProperty.spotAngle.value.Evaluate(t) * weight;
                state.range += lightProperty.range.value.Evaluate(t) * weight;
            }
        }

        private static void AccumulateColor(StageLightQueueData queueData, float currentTime, int index, float weight, ref Accumulator state)
        {
            var manualColorArrayProperty = queueData.TryGetActiveProperty<ManualColorArrayProperty>();
            if (manualColorArrayProperty != null)
            {
                var values = manualColorArrayProperty.colorValues.value;
                if (values != null && index >= 0 && index < values.Count && values[index] != null)
                {
                    state.color += values[index].color * weight;
                }

                return;
            }

            var colorProperty = queueData.TryGetActiveProperty<LightColorProperty>();
            if (colorProperty != null)
            {
                var t = GetNormalizedTimeSafe(currentTime, queueData, typeof(LightColorProperty), index);
                state.color += colorProperty.lightToggleColor.value.Evaluate(t) * weight;
            }
        }

        private static void AccumulatePanTilt(StageLightQueueData queueData, float currentTime, int index, float weight, ref Accumulator state)
        {
            var panProperty = queueData.TryGetActiveProperty<PanProperty>();
            var tiltProperty = queueData.TryGetActiveProperty<TiltProperty>();
            var manualPanTiltProperty = queueData.TryGetActiveProperty<ManualPanTiltProperty>();

            if (manualPanTiltProperty != null)
            {
                var positions = manualPanTiltProperty.positions.value;
                if (positions != null && index >= 0 && index < positions.Count && positions[index] != null)
                {
                    var position = positions[index];
                    var panBase = EvaluateRoll(queueData, panProperty, typeof(PanProperty), currentTime, index);
                    var tiltBase = EvaluateRoll(queueData, tiltProperty, typeof(TiltProperty), currentTime, index);

                    switch (manualPanTiltProperty.mode.value)
                    {
                        case ManualPanTiltMode.Add:
                            state.pan += (position.pan + panBase) * weight;
                            state.tilt += (position.tilt + tiltBase) * weight;
                            break;
                        case ManualPanTiltMode.Multiply:
                            state.pan += position.pan * panBase * weight;
                            state.tilt += position.tilt * tiltBase * weight;
                            break;
                        default:
                            state.pan += position.pan * weight;
                            state.tilt += position.tilt * weight;
                            break;
                    }
                }

                return;
            }

            if (panProperty != null)
            {
                state.pan += EvaluateRoll(queueData, panProperty, typeof(PanProperty), currentTime, index) * weight;
            }

            if (tiltProperty != null)
            {
                state.tilt += EvaluateRoll(queueData, tiltProperty, typeof(TiltProperty), currentTime, index) * weight;
            }
        }

        private static float EvaluateRoll(StageLightQueueData queueData, RollProperty property, System.Type propertyType, float currentTime, int index)
        {
            if (property == null)
            {
                return 0f;
            }

            var t = GetNormalizedTimeSafe(currentTime, queueData, propertyType, index);
            return property.rollTransform.value.Evaluate(t);
        }

        private static float GetNormalizedTimeSafe(float currentTime, StageLightQueueData queueData, System.Type propertyType, int index)
        {
            var additionalProperty = queueData.TryGetActiveProperty(propertyType);
            if (additionalProperty != null && additionalProperty.clockOverride.value == null)
            {
                additionalProperty.clockOverride.value = new ClockOverride();
            }

            return SlmUtility.GetNormalizedTime(currentTime, queueData, propertyType, index);
        }

        private static void AccumulateGobo(StageLightQueueData queueData, float weight, ref Accumulator state)
        {
            var goboProperty = queueData.TryGetActiveProperty<MfvVRSLGoboProperty>();
            if (goboProperty == null || weight < state.goboWeight)
            {
                return;
            }

            state.gobo = goboProperty.GetClampedGoboIndex();
            state.goboWeight = weight;
        }

        public struct Accumulator
        {
            public float pan;
            public float tilt;
            public float intensity;
            public Color color;
            public float spotAngle;
            public float range;
            public int gobo;
            public float goboWeight;

            public MfvVRSLFrame ToFrame()
            {
                return new MfvVRSLFrame
                {
                    pan = pan,
                    tilt = tilt,
                    intensity = Mathf.Clamp01(intensity * 0.1f),
                    color = new Color(color.r, color.g, color.b, 1f),
                    coneWidth = Mathf.Lerp(0f, 5.5f, Mathf.Clamp01(spotAngle / 180f)),
                    coneLength = Mathf.Lerp(0.5f, 10f, Mathf.Clamp01(range / 100f)),
                    gobo = gobo
                };
            }
        }
    }
}
