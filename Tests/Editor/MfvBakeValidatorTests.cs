using ManeuverForVRSL.Editor;
using NUnit.Framework;
using StageLightManeuver;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ManeuverForVRSL.Tests
{
    public class MfvBakeValidatorTests
    {
        [Test]
        public void Validate_AllowsActivationAndAnimationTracks()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var directorObject = new GameObject("director");
            try
            {
                timeline.CreateTrack<ActivationTrack>(null, "Activation");
                timeline.CreateTrack<AnimationTrack>(null, "Animation");
                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;

                var valid = MfvBakeValidator.Validate(director, out var errors);

                Assert.IsTrue(valid, string.Join("\n", errors));
            }
            finally
            {
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void Validate_RejectsControlTrack()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var directorObject = new GameObject("director");
            try
            {
                timeline.CreateTrack<ControlTrack>(null, "Control");
                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;

                var valid = MfvBakeValidator.Validate(director, out var errors);

                Assert.IsFalse(valid);
                Assert.That(string.Join("\n", errors), Does.Contain("Unsupported Timeline track"));
            }
            finally
            {
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void Validate_RejectsMfvChannelWithoutVrslFixture()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var directorObject = new GameObject("director");
            var fixtureObject = new GameObject("Fixture");
            try
            {
                var track = timeline.CreateTrack<StageLightTimelineTrack>(null, "SLM");
                var director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                var fixture = fixtureObject.AddComponent<StageLightFixture>();
                fixtureObject.AddComponent<MfvVRSLFixtureChannel>();
                fixture.Init();
                director.SetGenericBinding(track, fixture);

                var valid = MfvBakeValidator.Validate(director, out var errors);

                Assert.IsFalse(valid);
                Assert.That(string.Join("\n", errors),
                    Does.Contain("Fixture 'Fixture' has MfvVRSLFixtureChannel but no VRStageLighting_DMX_Static target"));
            }
            finally
            {
                Object.DestroyImmediate(fixtureObject);
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(timeline);
            }
        }
    }
}
