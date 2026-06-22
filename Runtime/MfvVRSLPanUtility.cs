namespace ManeuverForVRC
{
    public static class MfvVRSLPanUtility
    {
        public static float ToVrslPanOffset(float slmPan)
        {
            return -slmPan;
        }

        public static float ToSlmPan(float vrslPanOffset)
        {
            return -vrslPanOffset;
        }
    }
}
