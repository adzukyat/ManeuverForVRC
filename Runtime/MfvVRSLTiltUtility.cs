namespace ManeuverForVRC
{
    public static class MfvVRSLTiltUtility
    {
        public const float DefaultVrslTiltOffset = 90f;

        public static float ToVrslTiltOffset(float slmTilt)
        {
            return slmTilt + DefaultVrslTiltOffset;
        }

        public static float ToSlmTilt(float vrslTiltOffset)
        {
            return vrslTiltOffset - DefaultVrslTiltOffset;
        }
    }
}
