using UnityEngine;
using UnityEngine.Playables;
using VRSL;

#if UDONSHARP
using UdonSharp;
using VRC.SDKBase;
#endif

namespace ManeuverForVRSL
{
#if UDONSHARP
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [AddComponentMenu("Maneuver For VRSL/MFV VRSL Timeline Player")]
    public class MfvVRSLTimelinePlayer : UdonSharpBehaviour
#else
    [AddComponentMenu("Maneuver For VRSL/MFV VRSL Timeline Player")]
    public class MfvVRSLTimelinePlayer : MonoBehaviour
#endif
    {
        public PlayableDirector director;
        public VRStageLighting_DMX_Static[] fixtures = new VRStageLighting_DMX_Static[0];

        public int[] trackFixture = new int[0];
        public int[] trackProperty = new int[0];
        public int[] keyStart = new int[0];
        public int[] keyCount = new int[0];
        public float[] keyTimes = new float[0];
        public float[] keyValues = new float[0];

        public int[] eventFixture = new int[0];
        public int[] eventProperty = new int[0];
        public int[] eventStart = new int[0];
        public int[] eventCount = new int[0];
        public float[] eventTimes = new float[0];
        public int[] eventValues = new int[0];

        private int[] _trackCursors;
        private int[] _eventCursors;
        private bool[] _fixtureTouched;
        private float _lastTime = -1f;
        private bool _initialized;

        private void Start()
        {
            InitializeRuntimeState();
        }

        private void Update()
        {
            if (director == null)
            {
                return;
            }

            EvaluateAt((float)director.time);
        }

        public void EvaluateAt(float time)
        {
            InitializeRuntimeState();
            var timeWentBackwards = _lastTime > time;

            for (var i = 0; i < trackFixture.Length; i++)
            {
                EvaluateContinuousTrack(i, time, timeWentBackwards);
            }

            for (var i = 0; i < eventFixture.Length; i++)
            {
                EvaluateEventTrack(i, time, timeWentBackwards);
            }

            FlushTouchedFixtures();
            _lastTime = time;
        }

        private void InitializeRuntimeState()
        {
            if (_initialized && _trackCursors != null && _trackCursors.Length == trackFixture.Length &&
                _eventCursors != null && _eventCursors.Length == eventFixture.Length &&
                _fixtureTouched != null && _fixtureTouched.Length == fixtures.Length)
            {
                return;
            }

            _trackCursors = new int[trackFixture.Length];
            _eventCursors = new int[eventFixture.Length];
            _fixtureTouched = new bool[fixtures.Length];
            _lastTime = -1f;
            _initialized = true;
        }

        private void EvaluateContinuousTrack(int trackIndex, float time, bool timeWentBackwards)
        {
            var count = keyCount[trackIndex];
            if (count <= 0)
            {
                return;
            }

            var start = keyStart[trackIndex];
            var value = keyValues[start];
            if (count > 1)
            {
                if (time <= keyTimes[start])
                {
                    _trackCursors[trackIndex] = 0;
                    value = keyValues[start];
                }
                else if (time >= keyTimes[start + count - 1])
                {
                    _trackCursors[trackIndex] = count - 2;
                    value = keyValues[start + count - 1];
                }
                else
                {
                    var cursor = _trackCursors[trackIndex];
                    if (timeWentBackwards || cursor < 0 || cursor >= count - 1 ||
                        time < keyTimes[start + cursor] || time > keyTimes[start + cursor + 1])
                    {
                        cursor = FindKeyCursor(start, count, time);
                    }
                    else
                    {
                        while (cursor < count - 2 && time > keyTimes[start + cursor + 1])
                        {
                            cursor++;
                        }
                    }

                    _trackCursors[trackIndex] = cursor;
                    var t0 = keyTimes[start + cursor];
                    var t1 = keyTimes[start + cursor + 1];
                    var v0 = keyValues[start + cursor];
                    var v1 = keyValues[start + cursor + 1];
                    var ratio = t1 > t0 ? Mathf.InverseLerp(t0, t1, time) : 0f;
                    value = Mathf.Lerp(v0, v1, ratio);
                }
            }

            ApplyValue(trackFixture[trackIndex], trackProperty[trackIndex], value);
        }

        private int FindKeyCursor(int start, int count, float time)
        {
            var low = 0;
            var high = count - 1;
            while (low <= high)
            {
                var mid = (low + high) / 2;
                if (keyTimes[start + mid] <= time)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Mathf.Clamp(low - 1, 0, count - 2);
        }

        private void EvaluateEventTrack(int trackIndex, float time, bool timeWentBackwards)
        {
            var count = eventCount[trackIndex];
            if (count <= 0)
            {
                return;
            }

            var start = eventStart[trackIndex];
            if (time < eventTimes[start])
            {
                _eventCursors[trackIndex] = -1;
                return;
            }

            var cursor = _eventCursors[trackIndex];
            if (timeWentBackwards || cursor < 0 || cursor >= count || time < eventTimes[start + cursor])
            {
                cursor = FindEventCursor(start, count, time);
            }
            else
            {
                while (cursor < count - 1 && time >= eventTimes[start + cursor + 1])
                {
                    cursor++;
                }
            }

            _eventCursors[trackIndex] = cursor;
            if (cursor >= 0)
            {
                ApplyValue(eventFixture[trackIndex], eventProperty[trackIndex], eventValues[start + cursor]);
            }
        }

        private int FindEventCursor(int start, int count, float time)
        {
            var low = 0;
            var high = count - 1;
            while (low <= high)
            {
                var mid = (low + high) / 2;
                if (eventTimes[start + mid] <= time)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Mathf.Clamp(low - 1, -1, count - 1);
        }

        private void ApplyValue(int fixtureIndex, int propertyId, float value)
        {
            if (fixtures == null || fixtureIndex < 0 || fixtureIndex >= fixtures.Length)
            {
                return;
            }

            var fixture = fixtures[fixtureIndex];
            if (fixture == null)
            {
                return;
            }

            fixture.enableDMXChannels = false;
            fixture.enableStrobe = false;

            if (propertyId == MfvVRSLPropertyId.Pan)
            {
                fixture.panOffsetBlueGreen = value;
            }
            else if (propertyId == MfvVRSLPropertyId.Tilt)
            {
                fixture.tiltOffsetBlue = value;
            }
            else if (propertyId == MfvVRSLPropertyId.Intensity)
            {
                fixture.globalIntensity = Mathf.Clamp01(value);
            }
            else if (propertyId == MfvVRSLPropertyId.ColorR)
            {
                var color = fixture.lightColorTint;
                color.r = value;
                fixture.lightColorTint = color;
            }
            else if (propertyId == MfvVRSLPropertyId.ColorG)
            {
                var color = fixture.lightColorTint;
                color.g = value;
                fixture.lightColorTint = color;
            }
            else if (propertyId == MfvVRSLPropertyId.ColorB)
            {
                var color = fixture.lightColorTint;
                color.b = value;
                fixture.lightColorTint = color;
            }
            else if (propertyId == MfvVRSLPropertyId.ConeWidth)
            {
                fixture.coneWidth = Mathf.Clamp(value, 0f, 5.5f);
            }
            else if (propertyId == MfvVRSLPropertyId.ConeLength)
            {
                fixture.coneLength = Mathf.Clamp(value, 0.5f, 10f);
            }
            else if (propertyId == MfvVRSLPropertyId.Gobo)
            {
                fixture.selectGOBO = Mathf.Clamp(Mathf.RoundToInt(value), 1, 8);
            }

            _fixtureTouched[fixtureIndex] = true;
        }

        private void FlushTouchedFixtures()
        {
            for (var i = 0; i < _fixtureTouched.Length; i++)
            {
                if (!_fixtureTouched[i])
                {
                    continue;
                }

                _fixtureTouched[i] = false;
                if (fixtures[i] != null)
                {
                    fixtures[i]._UpdateInstancedProperties();
                }
            }
        }
    }
}
