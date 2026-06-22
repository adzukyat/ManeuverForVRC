using System;
using System.Collections.Generic;
using StageLightManeuver;
using UnityEngine;
using VRSL;

namespace ManeuverForVRC
{
    [ExecuteAlways]
    [AddComponentMenu("Maneuver For VRC/MFV VRSL Fixture Channel")]
    public class MfvVRSLFixtureChannel : StageLightChannelBase
    {
        private const float SlmIntensityScale = 10f;
        private const float SlmMaxSpotAngle = 180f;
        private const float SlmMaxRange = 100f;
        private const float VrslMaxConeWidth = 5.5f;
        private const float VrslMinConeLength = 0.5f;
        private const float VrslMaxConeLength = 10f;

        [ChannelField(true, false)]
        public VRStageLighting_DMX_Static vrslFixture;

        [ChannelField(false)]
        public MfvVRSLFrame lastFrame = MfvVRSLFrame.Default();

        private readonly List<StageLightQueueData> _queueBuffer = new List<StageLightQueueData>();
        private bool _reportedMissingFixture;

        public override void Init()
        {
            base.Init();
            if (vrslFixture == null)
            {
                vrslFixture = GetComponentInChildren<VRStageLighting_DMX_Static>(true);
            }

            PropertyTypes = new List<Type>
            {
                typeof(LightIntensityProperty),
                typeof(LightColorProperty),
                typeof(LightProperty),
                typeof(PanProperty),
                typeof(TiltProperty),
                typeof(MfvVRSLGoboProperty)
            };
        }

        public override void InitializeTimelineProperties(StageLightQueueData stageLightQueueData, List<StageLightFixture> stageLightFixtures)
        {
            if (stageLightQueueData == null)
            {
                return;
            }

            if (vrslFixture == null)
            {
                vrslFixture = GetComponentInChildren<VRStageLighting_DMX_Static>(true);
            }

            if (vrslFixture == null)
            {
                return;
            }

            var fixtureIndex = GetFixtureIndex(stageLightFixtures);
            var fixtureCount = stageLightFixtures != null && stageLightFixtures.Count > 0 ? stageLightFixtures.Count : 1;
            InitializeManualProperties(stageLightQueueData, fixtureIndex, fixtureCount);

            if (fixtureIndex == 0)
            {
                InitializeScalarProperties(stageLightQueueData);
            }
        }

        public override List<Type> GetAddablePropertyTypes()
        {
            var types = base.GetAddablePropertyTypes();
            types.Add(typeof(LightFlickerProperty));
            types.Add(typeof(ManualLightArrayProperty));
            types.Add(typeof(ManualColorArrayProperty));
            types.Add(typeof(ManualPanTiltProperty));
            return types;
        }

        public override void EvaluateQue(float currentTime)
        {
            base.EvaluateQue(currentTime);
            _queueBuffer.Clear();
            while (stageLightDataQueue.Count > 0)
            {
                _queueBuffer.Add(stageLightDataQueue.Dequeue());
            }

            lastFrame = MfvVRSLFrameEvaluator.Evaluate(_queueBuffer, parentStageLightFixture, currentTime);
        }

        public override void UpdateChannel()
        {
            if (!MfvVRSLFixtureApplier.TryApply(vrslFixture, lastFrame) && !_reportedMissingFixture)
            {
                Debug.LogError(
                    $"Fixture '{name}' has MfvVRSLFixtureChannel but no VRStageLighting_DMX_Static target. Assign vrslFixture or place the VRSL fixture under the channel object.",
                    this);
                _reportedMissingFixture = true;
            }
        }

        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            Init();
        }

        private void InitializeScalarProperties(StageLightQueueData stageLightQueueData)
        {
            var intensity = FindProperty<LightIntensityProperty>(stageLightQueueData);
            if (intensity != null)
            {
                EnsureMinMax(ref intensity.lightToggleIntensity, ToSlmIntensity(vrslFixture.globalIntensity));
                intensity.propertyOverride = true;
                intensity.lightToggleIntensity.propertyOverride = true;
            }

            var color = FindProperty<LightColorProperty>(stageLightQueueData);
            if (color != null)
            {
                color.EnsureValues();
                color.lightToggleColor.value = CreateConstantGradient(vrslFixture.lightColorTint);
                color.propertyOverride = true;
                color.lightToggleColor.propertyOverride = true;
            }

            var light = FindProperty<LightProperty>(stageLightQueueData);
            if (light != null)
            {
                EnsureMinMax(ref light.spotAngle, ToSlmSpotAngle(vrslFixture.coneWidth));
                EnsureMinMax(ref light.range, ToSlmRange(vrslFixture.coneLength));
                light.propertyOverride = true;
                light.spotAngle.propertyOverride = true;
                light.range.propertyOverride = true;
            }

            var pan = FindProperty<PanProperty>(stageLightQueueData);
            if (pan != null)
            {
                EnsureMinMax(ref pan.rollTransform, MfvVRSLPanUtility.ToSlmPan(vrslFixture.panOffsetBlueGreen));
                pan.propertyOverride = true;
                pan.rollTransform.propertyOverride = true;
            }

            var tilt = FindProperty<TiltProperty>(stageLightQueueData);
            if (tilt != null)
            {
                EnsureMinMax(ref tilt.rollTransform, MfvVRSLTiltUtility.ToSlmTilt(vrslFixture.tiltOffsetBlue));
                tilt.propertyOverride = true;
                tilt.rollTransform.propertyOverride = true;
            }

            var gobo = FindProperty<MfvVRSLGoboProperty>(stageLightQueueData);
            if (gobo != null)
            {
                if (gobo.goboIndex == null)
                {
                    gobo.goboIndex = new SlmToggleValue<int>();
                }

                gobo.goboIndex.value = Mathf.Clamp(vrslFixture.selectGOBO, 1, 8);
                gobo.propertyOverride = true;
                gobo.goboIndex.propertyOverride = true;
            }
        }

        private void InitializeManualProperties(StageLightQueueData stageLightQueueData, int fixtureIndex, int fixtureCount)
        {
            var light = FindProperty<ManualLightArrayProperty>(stageLightQueueData);
            if (light != null)
            {
                if (light.lightValues == null)
                {
                    light.lightValues = new SlmToggleValue<List<LightPrimitiveValue>>();
                }

                if (light.lightValues.value == null)
                {
                    light.lightValues.value = new List<LightPrimitiveValue>();
                }

                EnsureListSize(light.lightValues.value, Mathf.Max(fixtureIndex + 1, fixtureCount), () => new LightPrimitiveValue());
                var value = light.lightValues.value[fixtureIndex] ?? new LightPrimitiveValue();
                value.name = GetFixtureName();
                value.intensity = ToSlmIntensity(vrslFixture.globalIntensity);
                value.angle = ToSlmSpotAngle(vrslFixture.coneWidth);
                value.innerAngle = value.angle;
                value.range = ToSlmRange(vrslFixture.coneLength);
                light.lightValues.value[fixtureIndex] = value;
                light.propertyOverride = true;
                light.lightValues.propertyOverride = true;
            }

            var color = FindProperty<ManualColorArrayProperty>(stageLightQueueData);
            if (color != null)
            {
                if (color.colorValues == null)
                {
                    color.colorValues = new SlmToggleValue<List<ColorPrimitiveValue>>();
                }

                if (color.colorValues.value == null)
                {
                    color.colorValues.value = new List<ColorPrimitiveValue>();
                }

                EnsureListSize(color.colorValues.value, Mathf.Max(fixtureIndex + 1, fixtureCount), () => new ColorPrimitiveValue());
                var value = color.colorValues.value[fixtureIndex] ?? new ColorPrimitiveValue();
                value.name = GetFixtureName();
                value.color = vrslFixture.lightColorTint;
                color.colorValues.value[fixtureIndex] = value;
                color.propertyOverride = true;
                color.colorValues.propertyOverride = true;
            }

            var panTilt = FindProperty<ManualPanTiltProperty>(stageLightQueueData);
            if (panTilt != null)
            {
                if (panTilt.positions == null)
                {
                    panTilt.positions = new SlmToggleValue<List<PanTiltPrimitive>>();
                }

                if (panTilt.positions.value == null)
                {
                    panTilt.positions.value = new List<PanTiltPrimitive>();
                }

                EnsureListSize(panTilt.positions.value, Mathf.Max(fixtureIndex + 1, fixtureCount), () => new PanTiltPrimitive());
                var value = panTilt.positions.value[fixtureIndex] ?? new PanTiltPrimitive();
                value.name = GetFixtureName();
                value.pan = MfvVRSLPanUtility.ToSlmPan(vrslFixture.panOffsetBlueGreen);
                value.tilt = MfvVRSLTiltUtility.ToSlmTilt(vrslFixture.tiltOffsetBlue);
                panTilt.positions.value[fixtureIndex] = value;
                panTilt.propertyOverride = true;
                panTilt.positions.propertyOverride = true;

                if (panTilt.mode == null)
                {
                    panTilt.mode = new SlmToggleValue<ManualPanTiltMode>();
                }

                panTilt.mode.value = ManualPanTiltMode.Overwrite;
                panTilt.mode.propertyOverride = true;
            }
        }

        private int GetFixtureIndex(List<StageLightFixture> stageLightFixtures)
        {
            var stageLightFixture = parentStageLightFixture != null ? parentStageLightFixture : GetComponent<StageLightFixture>();
            if (stageLightFixtures != null && stageLightFixture != null)
            {
                for (var i = 0; i < stageLightFixtures.Count; i++)
                {
                    if (stageLightFixtures[i] == stageLightFixture)
                    {
                        return i;
                    }
                }
            }

            return stageLightFixture != null ? Mathf.Max(0, stageLightFixture.order) : 0;
        }

        private string GetFixtureName()
        {
            var stageLightFixture = parentStageLightFixture != null ? parentStageLightFixture : GetComponent<StageLightFixture>();
            return stageLightFixture != null ? stageLightFixture.name : name;
        }

        private static void EnsureMinMax(ref SlmToggleValue<MinMaxEasingValue> toggleValue, float constant)
        {
            if (toggleValue == null)
            {
                toggleValue = new SlmToggleValue<MinMaxEasingValue>();
            }

            if (toggleValue.value == null)
            {
                toggleValue.value = new MinMaxEasingValue();
            }

            toggleValue.value.mode = StageLightManeuver.AnimationMode.Constant;
            toggleValue.value.constant = constant;
        }

        private static Gradient CreateConstantGradient(Color color)
        {
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
            return gradient;
        }

        private static void EnsureListSize<T>(List<T> values, int count, Func<T> createValue)
        {
            while (values.Count < count)
            {
                values.Add(createValue());
            }
        }

        private static T FindProperty<T>(StageLightQueueData stageLightQueueData)
            where T : SlmProperty
        {
            if (stageLightQueueData.stageLightProperties == null)
            {
                return null;
            }

            foreach (var property in stageLightQueueData.stageLightProperties)
            {
                if (property is T typedProperty)
                {
                    return typedProperty;
                }
            }

            return null;
        }

        private static float ToSlmIntensity(float globalIntensity)
        {
            return Mathf.Clamp01(globalIntensity) * SlmIntensityScale;
        }

        private static float ToSlmSpotAngle(float coneWidth)
        {
            return Mathf.InverseLerp(0f, VrslMaxConeWidth, Mathf.Clamp(coneWidth, 0f, VrslMaxConeWidth)) * SlmMaxSpotAngle;
        }

        private static float ToSlmRange(float coneLength)
        {
            return Mathf.InverseLerp(
                VrslMinConeLength,
                VrslMaxConeLength,
                Mathf.Clamp(coneLength, VrslMinConeLength, VrslMaxConeLength)) * SlmMaxRange;
        }
    }
}
