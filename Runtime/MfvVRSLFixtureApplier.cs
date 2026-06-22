using UnityEngine;
using VRSL;

namespace ManeuverForVRC
{
    public static class MfvVRSLFixtureApplier
    {
        public static void Apply(VRStageLighting_DMX_Static fixture, MfvVRSLFrame frame)
        {
            TryApply(fixture, frame);
        }

        public static bool TryApply(VRStageLighting_DMX_Static fixture, MfvVRSLFrame frame)
        {
            if (fixture == null)
            {
                return false;
            }

            fixture.enableDMXChannels = false;
            fixture.enableStrobe = false;
            fixture.panOffsetBlueGreen = MfvVRSLPanUtility.ToVrslPanOffset(frame.pan);
            fixture.tiltOffsetBlue = MfvVRSLTiltUtility.ToVrslTiltOffset(frame.tilt);
            fixture.globalIntensity = Mathf.Clamp01(frame.intensity);
            fixture.lightColorTint = frame.color;
            fixture.coneWidth = Mathf.Clamp(frame.coneWidth, 0f, 5.5f);
            fixture.coneLength = Mathf.Clamp(frame.coneLength, 0.5f, 10f);

            if (frame.gobo > 0)
            {
                fixture.selectGOBO = Mathf.Clamp(frame.gobo, 1, 8);
            }

            fixture._UpdateInstancedProperties();
            return true;
        }
    }
}
