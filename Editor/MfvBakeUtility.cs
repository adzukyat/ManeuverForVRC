using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ManeuverForVRSL;
using StageLightManeuver;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using VRSL;
using Debug = UnityEngine.Debug;

namespace ManeuverForVRSL.Editor
{
    public static class MfvBakeUtility
    {
        public const string DefaultOutputFolder = "Assets/ManeuverForVRSL/Baked";

        public static MfvBakeResult Bake(PlayableDirector director, MfvBakeSettings settings, string outputFolder = DefaultOutputFolder)
        {
            settings = settings != null ? settings : MfvBakeSettings.CreateDefault();
            if (!MfvBakeValidator.Validate(director, out var errors))
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"[ManeuverForVRSL] {error}", director);
                }

                return null;
            }

            EnsureFolder(outputFolder);

            var stopwatch = Stopwatch.StartNew();
            var channels = MfvBakeValidator.CollectFixtureChannels(director);
            foreach (var channel in channels)
            {
                channel.Init();
            }

            var fixtures = channels.Select(x => x.vrslFixture).Where(x => x != null).Distinct().ToArray();
            var fixtureIndices = new Dictionary<VRStageLighting_DMX_Static, int>();
            for (var i = 0; i < fixtures.Length; i++)
            {
                fixtureIndices[fixtures[i]] = i;
            }

            var sampledFrames = SampleFrames(director, channels, fixtures, fixtureIndices, settings);
            var bakedAsset = BuildBakedAsset(director, settings, fixtures, sampledFrames);
            var uploadTimeline = CreateUploadTimeline(director, outputFolder);

            var baseName = SanitizeAssetName(director.name);
            var bakedPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{baseName}_BakedShow.asset");
            AssetDatabase.CreateAsset(bakedAsset, bakedPath);
            AssetDatabase.SaveAssets();

            stopwatch.Stop();
            Debug.Log($"[ManeuverForVRSL] Baked {fixtures.Length} fixtures, {bakedAsset.ContinuousTrackCount} continuous tracks, {bakedAsset.EventTrackCount} event tracks in {stopwatch.ElapsedMilliseconds} ms. Asset: {bakedPath}", bakedAsset);

            return new MfvBakeResult
            {
                bakedAsset = bakedAsset,
                uploadTimeline = uploadTimeline,
                fixtures = fixtures
            };
        }

        public static void ConfigurePlayer(MfvVRSLTimelinePlayer player, PlayableDirector director, MfvBakeResult result)
        {
            if (player == null || result == null || result.bakedAsset == null)
            {
                return;
            }

            Undo.RecordObject(player, "Configure ManeuverForVRSL Player");
            player.director = director;
            player.fixtures = result.fixtures;
            result.bakedAsset.CopyTo(player);
            EditorUtility.SetDirty(player);
        }

        private static Dictionary<VRStageLighting_DMX_Static, List<MfvVRSLFrame>> SampleFrames(
            PlayableDirector director,
            IReadOnlyList<MfvVRSLFixtureChannel> channels,
            IReadOnlyList<VRStageLighting_DMX_Static> fixtures,
            IReadOnlyDictionary<VRStageLighting_DMX_Static, int> fixtureIndices,
            MfvBakeSettings settings)
        {
            var duration = Mathf.Max(0f, (float)director.duration);
            var sampleRate = Mathf.Max(1f, settings.internalSampleRate);
            var sampleCount = Mathf.Max(1, Mathf.FloorToInt(duration * sampleRate) + 1);
            var sampledFrames = fixtures.ToDictionary(x => x, _ => new List<MfvVRSLFrame>(sampleCount));
            var snapshots = fixtures.Select(FixtureSnapshot.Capture).ToList();
            var originalTime = director.time;

            try
            {
                for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    var time = Mathf.Min(duration, sampleIndex / sampleRate);
                    director.time = time;
                    director.Evaluate();

                    foreach (var fixture in fixtures)
                    {
                        sampledFrames[fixture].Add(MfvVRSLFrame.Default());
                    }

                    foreach (var channel in channels)
                    {
                        if (channel == null || channel.vrslFixture == null || !fixtureIndices.ContainsKey(channel.vrslFixture))
                        {
                            continue;
                        }

                        sampledFrames[channel.vrslFixture][sampleIndex] = channel.lastFrame;
                    }
                }
            }
            finally
            {
                director.time = originalTime;
                director.Evaluate();
                foreach (var snapshot in snapshots)
                {
                    snapshot.Restore();
                }
            }

            return sampledFrames;
        }

        private static MfvBakedShowAsset BuildBakedAsset(
            PlayableDirector director,
            MfvBakeSettings settings,
            IReadOnlyList<VRStageLighting_DMX_Static> fixtures,
            IReadOnlyDictionary<VRStageLighting_DMX_Static, List<MfvVRSLFrame>> sampledFrames)
        {
            var asset = ScriptableObject.CreateInstance<MfvBakedShowAsset>();
            asset.duration = Mathf.Max(0f, (float)director.duration);
            asset.sampleRate = settings.internalSampleRate;
            asset.fixtureNames = fixtures.Select(x => x != null ? x.name : "<missing>").ToArray();

            var trackFixture = new List<int>();
            var trackProperty = new List<int>();
            var keyStart = new List<int>();
            var keyCount = new List<int>();
            var keyTimes = new List<float>();
            var keyValues = new List<float>();
            var eventFixture = new List<int>();
            var eventProperty = new List<int>();
            var eventStart = new List<int>();
            var eventCount = new List<int>();
            var eventTimes = new List<float>();
            var eventValues = new List<int>();

            for (var fixtureIndex = 0; fixtureIndex < fixtures.Count; fixtureIndex++)
            {
                var frames = sampledFrames[fixtures[fixtureIndex]];
                AddContinuousTrack(fixtureIndex, MfvVRSLPropertyId.Pan, frames, settings, trackFixture, trackProperty, keyStart, keyCount, keyTimes, keyValues);
                AddContinuousTrack(fixtureIndex, MfvVRSLPropertyId.Tilt, frames, settings, trackFixture, trackProperty, keyStart, keyCount, keyTimes, keyValues);
                AddContinuousTrack(fixtureIndex, MfvVRSLPropertyId.Intensity, frames, settings, trackFixture, trackProperty, keyStart, keyCount, keyTimes, keyValues);
                AddContinuousTrack(fixtureIndex, MfvVRSLPropertyId.ColorR, frames, settings, trackFixture, trackProperty, keyStart, keyCount, keyTimes, keyValues);
                AddContinuousTrack(fixtureIndex, MfvVRSLPropertyId.ColorG, frames, settings, trackFixture, trackProperty, keyStart, keyCount, keyTimes, keyValues);
                AddContinuousTrack(fixtureIndex, MfvVRSLPropertyId.ColorB, frames, settings, trackFixture, trackProperty, keyStart, keyCount, keyTimes, keyValues);
                AddContinuousTrack(fixtureIndex, MfvVRSLPropertyId.ConeWidth, frames, settings, trackFixture, trackProperty, keyStart, keyCount, keyTimes, keyValues);
                AddContinuousTrack(fixtureIndex, MfvVRSLPropertyId.ConeLength, frames, settings, trackFixture, trackProperty, keyStart, keyCount, keyTimes, keyValues);
                AddGoboEvents(fixtureIndex, frames, settings.internalSampleRate, eventFixture, eventProperty, eventStart, eventCount, eventTimes, eventValues);
            }

            asset.trackFixture = trackFixture.ToArray();
            asset.trackProperty = trackProperty.ToArray();
            asset.keyStart = keyStart.ToArray();
            asset.keyCount = keyCount.ToArray();
            asset.keyTimes = keyTimes.ToArray();
            asset.keyValues = keyValues.ToArray();
            asset.eventFixture = eventFixture.ToArray();
            asset.eventProperty = eventProperty.ToArray();
            asset.eventStart = eventStart.ToArray();
            asset.eventCount = eventCount.ToArray();
            asset.eventTimes = eventTimes.ToArray();
            asset.eventValues = eventValues.ToArray();
            return asset;
        }

        private static void AddContinuousTrack(
            int fixtureIndex,
            int propertyId,
            IReadOnlyList<MfvVRSLFrame> frames,
            MfvBakeSettings settings,
            List<int> trackFixture,
            List<int> trackProperty,
            List<int> keyStart,
            List<int> keyCount,
            List<float> keyTimes,
            List<float> keyValues)
        {
            var times = new List<float>(frames.Count);
            var values = new List<float>(frames.Count);
            var sampleRate = Mathf.Max(1f, settings.internalSampleRate);
            for (var i = 0; i < frames.Count; i++)
            {
                times.Add(i / sampleRate);
                values.Add(GetFrameValue(frames[i], propertyId));
            }

            var reducedTimes = new List<float>();
            var reducedValues = new List<float>();
            MfvKeyframeReduction.Reduce(times, values, settings.GetTolerance(propertyId), reducedTimes, reducedValues);

            trackFixture.Add(fixtureIndex);
            trackProperty.Add(propertyId);
            keyStart.Add(keyTimes.Count);
            keyCount.Add(reducedTimes.Count);
            keyTimes.AddRange(reducedTimes);
            keyValues.AddRange(reducedValues);
        }

        private static void AddGoboEvents(
            int fixtureIndex,
            IReadOnlyList<MfvVRSLFrame> frames,
            float sampleRate,
            List<int> eventFixture,
            List<int> eventProperty,
            List<int> eventStart,
            List<int> eventCount,
            List<float> eventTimes,
            List<int> eventValues)
        {
            var start = eventTimes.Count;
            var previous = -1;
            sampleRate = Mathf.Max(1f, sampleRate);
            for (var i = 0; i < frames.Count; i++)
            {
                var gobo = frames[i].gobo;
                if (gobo <= 0 || gobo == previous)
                {
                    continue;
                }

                eventTimes.Add(i / sampleRate);
                eventValues.Add(gobo);
                previous = gobo;
            }

            if (eventTimes.Count == start)
            {
                return;
            }

            eventFixture.Add(fixtureIndex);
            eventProperty.Add(MfvVRSLPropertyId.Gobo);
            eventStart.Add(start);
            eventCount.Add(eventTimes.Count - start);
        }

        private static float GetFrameValue(MfvVRSLFrame frame, int propertyId)
        {
            switch (propertyId)
            {
                case MfvVRSLPropertyId.Pan:
                    return frame.pan;
                case MfvVRSLPropertyId.Tilt:
                    return frame.tilt;
                case MfvVRSLPropertyId.Intensity:
                    return frame.intensity;
                case MfvVRSLPropertyId.ColorR:
                    return frame.color.r;
                case MfvVRSLPropertyId.ColorG:
                    return frame.color.g;
                case MfvVRSLPropertyId.ColorB:
                    return frame.color.b;
                case MfvVRSLPropertyId.ConeWidth:
                    return frame.coneWidth;
                case MfvVRSLPropertyId.ConeLength:
                    return frame.coneLength;
                default:
                    return 0f;
            }
        }

        private static TimelineAsset CreateUploadTimeline(PlayableDirector director, string outputFolder)
        {
            var sourceTimeline = director.playableAsset as TimelineAsset;
            if (sourceTimeline == null)
            {
                return null;
            }

            var uploadTimeline = ScriptableObject.CreateInstance<TimelineAsset>();
            uploadTimeline.name = $"{sourceTimeline.name}_MfvUpload";
            var path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{SanitizeAssetName(uploadTimeline.name)}.playable");
            AssetDatabase.CreateAsset(uploadTimeline, path);
            uploadTimeline.durationMode = sourceTimeline.durationMode;
            uploadTimeline.fixedDuration = sourceTimeline.fixedDuration;

            foreach (var sourceTrack in sourceTimeline.GetRootTracks().Where(track => !(track is StageLightTimelineTrack)))
            {
                try
                {
                    uploadTimeline.CreateTrack(sourceTrack.GetType(), null, sourceTrack.name);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"Skipped copying upload timeline track '{sourceTrack.name}' ({sourceTrack.GetType().Name}): {exception.Message}");
                }
            }

            EditorUtility.SetDirty(uploadTimeline);
            AssetDatabase.SaveAssets();
            return uploadTimeline;
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string SanitizeAssetName(string value)
        {
            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }

        private sealed class FixtureSnapshot
        {
            private readonly VRStageLighting_DMX_Static _fixture;
            private readonly bool _enableDmx;
            private readonly bool _enableStrobe;
            private readonly float _pan;
            private readonly float _tilt;
            private readonly float _intensity;
            private readonly Color _color;
            private readonly float _coneWidth;
            private readonly float _coneLength;
            private readonly int _gobo;

            private FixtureSnapshot(VRStageLighting_DMX_Static fixture)
            {
                _fixture = fixture;
                _enableDmx = fixture.enableDMXChannels;
                _enableStrobe = fixture.enableStrobe;
                _pan = fixture.panOffsetBlueGreen;
                _tilt = fixture.tiltOffsetBlue;
                _intensity = fixture.globalIntensity;
                _color = fixture.lightColorTint;
                _coneWidth = fixture.coneWidth;
                _coneLength = fixture.coneLength;
                _gobo = fixture.selectGOBO;
            }

            public static FixtureSnapshot Capture(VRStageLighting_DMX_Static fixture)
            {
                return new FixtureSnapshot(fixture);
            }

            public void Restore()
            {
                _fixture.enableDMXChannels = _enableDmx;
                _fixture.enableStrobe = _enableStrobe;
                _fixture.panOffsetBlueGreen = _pan;
                _fixture.tiltOffsetBlue = _tilt;
                _fixture.globalIntensity = _intensity;
                _fixture.lightColorTint = _color;
                _fixture.coneWidth = _coneWidth;
                _fixture.coneLength = _coneLength;
                _fixture.selectGOBO = _gobo;
                _fixture._UpdateInstancedProperties();
            }
        }
    }
}
