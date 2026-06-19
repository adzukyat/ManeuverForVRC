using System.Collections.Generic;
using System.Linq;
using ManeuverForVRSL;
using StageLightManeuver;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ManeuverForVRSL.Editor
{
    public static class MfvBakeValidator
    {
        public static bool Validate(PlayableDirector director, out List<string> errors)
        {
            errors = new List<string>();
            if (director == null)
            {
                errors.Add("PlayableDirector is null.");
                return false;
            }

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null)
            {
                errors.Add($"{director.name} does not reference a TimelineAsset.");
                return false;
            }

            ValidateTracks(timeline, errors);
            ValidateStageLightBindings(director, timeline, errors);
            return errors.Count == 0;
        }

        public static List<MfvVRSLFixtureChannel> CollectFixtureChannels(PlayableDirector director)
        {
            var channels = new List<MfvVRSLFixtureChannel>();
            if (!(director.playableAsset is TimelineAsset timeline))
            {
                return channels;
            }

            foreach (var track in GetAllTracks(timeline))
            {
                if (!(track is StageLightTimelineTrack))
                {
                    continue;
                }

                var binding = director.GetGenericBinding(track) as StageLightFixtureBase;
                CollectFixtureChannels(binding, channels);
            }

            return channels.Distinct().ToList();
        }

        private static void ValidateTracks(TimelineAsset timeline, List<string> errors)
        {
            foreach (var track in GetAllTracks(timeline))
            {
                if (track is GroupTrack || track is StageLightTimelineTrack || track is ActivationTrack || track is AnimationTrack)
                {
                    continue;
                }

                if (track is ControlTrack || track is SignalTrack)
                {
                    errors.Add($"Unsupported Timeline track '{track.name}' ({track.GetType().Name}).");
                }
                else
                {
                    errors.Add($"Unsupported Timeline track '{track.name}' ({track.GetType().Name}). Only SLM, ActivationTrack, and AnimationTrack are supported in the initial MVP.");
                }
            }
        }

        private static void ValidateStageLightBindings(PlayableDirector director, TimelineAsset timeline, List<string> errors)
        {
            foreach (var track in GetAllTracks(timeline))
            {
                if (!(track is StageLightTimelineTrack))
                {
                    continue;
                }

                var binding = director.GetGenericBinding(track) as StageLightFixtureBase;
                if (binding == null)
                {
                    errors.Add($"SLM track '{track.name}' is not bound to a StageLightFixtureBase.");
                    continue;
                }

                ValidateFixtureBinding(track.name, binding, errors);
            }
        }

        private static void ValidateFixtureBinding(string trackName, StageLightFixtureBase binding, List<string> errors)
        {
            var fixtures = GetFixtures(binding);
            if (fixtures.Count == 0)
            {
                errors.Add($"SLM track '{trackName}' has no StageLightFixture entries.");
                return;
            }

            foreach (var fixture in fixtures)
            {
                var channels = fixture.GetComponents<StageLightChannelBase>();
                var hasMfvChannel = false;
                foreach (var channel in channels)
                {
                    if (channel is MfvVRSLFixtureChannel mfvChannel)
                    {
                        hasMfvChannel = true;
                        mfvChannel.Init();
                        if (mfvChannel.vrslFixture == null)
                        {
                            errors.Add($"Fixture '{fixture.name}' has MfvVRSLFixtureChannel but no VRStageLighting_DMX_Static target. Assign vrslFixture or place the VRSL fixture under the channel object.");
                        }
                    }
                    else if (channel != null)
                    {
                        errors.Add($"Fixture '{fixture.name}' uses unsupported SLM channel '{channel.GetType().Name}'.");
                    }
                }

                if (!hasMfvChannel)
                {
                    errors.Add($"Fixture '{fixture.name}' has no MfvVRSLFixtureChannel.");
                }
            }
        }

        private static void CollectFixtureChannels(StageLightFixtureBase binding, List<MfvVRSLFixtureChannel> channels)
        {
            foreach (var fixture in GetFixtures(binding))
            {
                var channel = fixture.GetComponent<MfvVRSLFixtureChannel>();
                if (channel != null)
                {
                    channels.Add(channel);
                }
            }
        }

        private static List<StageLightFixture> GetFixtures(StageLightFixtureBase binding)
        {
            var fixtures = new List<StageLightFixture>();
            if (binding == null)
            {
                return fixtures;
            }

            if (binding is StageLightFixture fixture)
            {
                fixtures.Add(fixture);
            }
            else if (binding.stageLightFixtures != null)
            {
                fixtures.AddRange(binding.stageLightFixtures.Where(x => x != null));
            }

            return fixtures;
        }

        private static IEnumerable<TrackAsset> GetAllTracks(TimelineAsset timeline)
        {
            return timeline.GetRootTracks().Concat(timeline.GetOutputTracks()).Distinct();
        }
    }
}
