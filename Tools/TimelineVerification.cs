using System;
using System.Collections.Generic;
using QDND.Combat.Animation;
using Godot;

namespace QDND.Tools
{
    /// <summary>
    /// Simple console verification for Animation Timeline system.
    /// Run this to verify the timeline implementation works correctly.
    /// </summary>
    public static class TimelineVerification
    {
        public static void RunVerification()
        {
            GD.Print("=== Animation Timeline Verification ===\n");

            bool allPassed = true;
            allPassed &= TestBasicTimeline();
            allPassed &= TestTimelineMarkers();
            allPassed &= TestPauseResume();
            allPassed &= TestFactoryMethods();
            allPassed &= TestPlaybackSpeed();

            GD.Print(allPassed ? "\n✓ All verifications passed!" : "\n✗ Some verifications failed!");
        }

        private static bool TestBasicTimeline()
        {
            GD.Print("Test: Basic Timeline");
            try
            {
                var timeline = new ActionTimeline("test");
                timeline.AddMarker(TimelineMarker.End(1.0f));

                if (timeline.Duration != 1.0f)
                {
                    GD.PrintErr($"  ✗ Duration mismatch: expected 1.0, got {timeline.Duration}");
                    return false;
                }

                timeline.Play();
                if (timeline.State != TimelineState.Playing)
                {
                    GD.PrintErr($"  ✗ State not Playing: {timeline.State}");
                    return false;
                }

                GD.Print("  ✓ Basic timeline works");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"  ✗ Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestTimelineMarkers()
        {
            GD.Print("Test: Timeline Markers");
            try
            {
                int markerCount = 0;
                var timeline = new ActionTimeline("test");
                timeline.AddMarker(new TimelineMarker(MarkerType.Custom, 0.3f, () => markerCount++));
                timeline.AddMarker(new TimelineMarker(MarkerType.Custom, 0.6f, () => markerCount++));
                timeline.AddMarker(TimelineMarker.End(1.0f));

                timeline.Play();
                timeline.Process(0.5f);

                if (markerCount != 1)
                {
                    GD.PrintErr($"  ✗ Expected 1 marker triggered, got {markerCount}");
                    return false;
                }

                timeline.Process(0.3f);

                if (markerCount != 2)
                {
                    GD.PrintErr($"  ✗ Expected 2 markers triggered, got {markerCount}");
                    return false;
                }

                GD.Print("  ✓ Markers trigger correctly");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"  ✗ Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestPauseResume()
        {
            GD.Print("Test: Pause & Resume");
            try
            {
                var timeline = new ActionTimeline("test");
                timeline.AddMarker(TimelineMarker.End(1.0f));

                timeline.Play();
                timeline.Process(0.3f);

                timeline.Pause();
                float pausedTime = timeline.CurrentTime;
                timeline.Process(0.5f);

                if (timeline.CurrentTime != pausedTime)
                {
                    GD.PrintErr($"  ✗ Timeline advanced while paused");
                    return false;
                }

                timeline.Resume();
                timeline.Process(0.2f);

                if (Math.Abs(timeline.CurrentTime - 0.5f) > 0.001f)
                {
                    GD.PrintErr($"  ✗ Timeline didn't resume correctly: {timeline.CurrentTime}");
                    return false;
                }

                GD.Print("  ✓ Pause & Resume work");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"  ✗ Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestFactoryMethods()
        {
            GD.Print("Test: Factory Methods");
            try
            {
                bool hitCalled = false;
                var melee = ActionTimeline.MeleeAttack(() => hitCalled = true);

                if (!melee.HasMarkerType(MarkerType.Hit))
                {
                    GD.PrintErr("  ✗ Melee attack missing Hit marker");
                    return false;
                }

                melee.Play();
                melee.Process(0.5f);

                if (!hitCalled)
                {
                    GD.PrintErr("  ✗ Hit callback not invoked");
                    return false;
                }

                bool projCalled = false;
                var ranged = ActionTimeline.RangedAttack(() => projCalled = true, () => { });

                if (!ranged.HasMarkerType(MarkerType.Projectile))
                {
                    GD.PrintErr("  ✗ Ranged attack missing Projectile marker");
                    return false;
                }

                GD.Print("  ✓ Factory methods work");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"  ✗ Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestPlaybackSpeed()
        {
            GD.Print("Test: Playback Speed");
            try
            {
                var timeline = new ActionTimeline("test");
                timeline.AddMarker(TimelineMarker.End(1.0f));
                timeline.PlaybackSpeed = 2.0f;

                timeline.Play();
                timeline.Process(0.5f);

                if (Math.Abs(timeline.CurrentTime - 1.0f) > 0.001f)
                {
                    GD.PrintErr($"  ✗ Playback speed not applied: {timeline.CurrentTime}");
                    return false;
                }

                GD.Print("  ✓ Playback speed works");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"  ✗ Exception: {ex.Message}");
                return false;
            }
        }
    }
}
