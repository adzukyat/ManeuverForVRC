using System;
using System.Collections.Generic;
using StageLightManeuver;
using UnityEngine;
using VRSL;

namespace ManeuverForVRSL
{
    [ExecuteAlways]
    [AddComponentMenu("Maneuver For VRSL/MFV VRSL Fixture Channel")]
    public class MfvVRSLFixtureChannel : StageLightChannelBase
    {
        [ChannelField(true, false)]
        public VRStageLighting_DMX_Static vrslFixture;

        [ChannelField(false)]
        public MfvVRSLFrame lastFrame = MfvVRSLFrame.Default();

        private readonly List<StageLightQueueData> _queueBuffer = new List<StageLightQueueData>();

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
                typeof(LightFlickerProperty),
                typeof(PanProperty),
                typeof(TiltProperty),
                typeof(ManualPanTiltProperty),
                typeof(ManualLightArrayProperty),
                typeof(ManualColorArrayProperty),
                typeof(MfvVRSLGoboProperty)
            };
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
            MfvVRSLFixtureApplier.Apply(vrslFixture, lastFrame);
        }

        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            Init();
        }
    }
}
