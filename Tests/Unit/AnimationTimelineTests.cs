using Godot;
using Xunit;
using QDND.Combat.Animation;
using System;
using System.Linq;

namespace QDND.Tests.Unit
{
    public class AnimationTimelineTests
    {
        [Fact]
        public void Timeline_Play_SetsState()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.End(1.0f));

            timeline.Play();

            Assert.Equal(TimelineState.Playing, timeline.State);
            Assert.True(timeline.IsPlaying);
        }

        [Fact]
        public void Timeline_Process_AdvancesTime()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();

            timeline.Process(0.5f);

            Assert.Equal(0.5f, timeline.CurrentTime);
        }

        [Fact]
        public void Timeline_Process_TriggersMarkers()
        {
            bool markerTriggered = false;
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(new TimelineMarker(MarkerType.Custom, 0.3f, () => markerTriggered = true));
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();

            timeline.Process(0.5f);

            Assert.True(markerTriggered);
        }

        [Fact]
        public void Timeline_OnHit_CallbackInvoked()
        {
            bool hitCalled = false;
            var timeline = new ActionTimeline("test");
            timeline.OnHit(0.3f, () => hitCalled = true);
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();

            timeline.Process(0.5f);

            Assert.True(hitCalled);
        }

        [Fact]
        public void Timeline_OnComplete_CallbackInvoked()
        {
            bool completeCalled = false;
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.OnComplete(() => completeCalled = true);
            timeline.Play();

            timeline.Process(1.5f);

            Assert.True(completeCalled);
            Assert.Equal(TimelineState.Completed, timeline.State);
        }

        [Fact]
        public void Timeline_Pause_StopsProcessing()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();
            timeline.Process(0.3f);

            timeline.Pause();
            float pausedTime = timeline.CurrentTime;
            timeline.Process(0.5f);

            Assert.Equal(TimelineState.Paused, timeline.State);
            Assert.Equal(pausedTime, timeline.CurrentTime);
        }

        [Fact]
        public void Timeline_Resume_ContinuesProcessing()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();
            timeline.Process(0.3f);
            timeline.Pause();

            timeline.Resume();
            timeline.Process(0.2f);

            Assert.Equal(TimelineState.Playing, timeline.State);
            Assert.Equal(0.5f, timeline.CurrentTime, 2);
        }

        [Fact]
        public void Timeline_Cancel_StopsAndSignals()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();

            timeline.Cancel();

            Assert.Equal(TimelineState.Cancelled, timeline.State);
            Assert.False(timeline.IsPlaying);
        }

        [Fact]
        public void Timeline_SkipTo_TriggersSkippedMarkers()
        {
            int triggeredCount = 0;
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(new TimelineMarker(MarkerType.Custom, 0.2f, () => triggeredCount++));
            timeline.AddMarker(new TimelineMarker(MarkerType.Custom, 0.4f, () => triggeredCount++));
            timeline.AddMarker(new TimelineMarker(MarkerType.Custom, 0.6f, () => triggeredCount++));
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();

            timeline.SkipTo(0.5f);

            Assert.Equal(2, triggeredCount);
            Assert.Equal(0.5f, timeline.CurrentTime);
        }

        [Fact]
        public void Timeline_Duration_CalculatedFromMarkers()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.Hit(0.3f));
            timeline.AddMarker(TimelineMarker.End(1.5f));
            timeline.AddMarker(TimelineMarker.Sound(0.1f, "test.wav"));

            Assert.Equal(1.5f, timeline.Duration);
        }

        [Fact]
        public void Timeline_PlaybackSpeed_AffectsTime()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.PlaybackSpeed = 2.0f;
            timeline.Play();

            timeline.Process(0.5f);

            Assert.Equal(1.0f, timeline.CurrentTime);
        }

        [Fact]
        public void TimelineMarker_FactoryMethods_SetCorrectType()
        {
            var start = TimelineMarker.Start();
            var hit = TimelineMarker.Hit(0.3f);
            var projectile = TimelineMarker.Projectile(0.2f);
            var end = TimelineMarker.End(1.0f);
            var sound = TimelineMarker.Sound(0.5f, "test.wav");
            var vfx = TimelineMarker.VFX(0.4f, "vfx_path", new Vector3(1, 2, 3));
            var cameraFocus = TimelineMarker.CameraFocus(0.1f, "target1");
            var cameraRelease = TimelineMarker.CameraRelease(0.9f);

            Assert.Equal(MarkerType.Start, start.Type);
            Assert.Equal(0f, start.Time);

            Assert.Equal(MarkerType.Hit, hit.Type);
            Assert.Equal(0.3f, hit.Time);

            Assert.Equal(MarkerType.Projectile, projectile.Type);
            Assert.Equal(0.2f, projectile.Time);

            Assert.Equal(MarkerType.AnimationEnd, end.Type);
            Assert.Equal(1.0f, end.Time);

            Assert.Equal(MarkerType.Sound, sound.Type);
            Assert.Equal("test.wav", sound.Data);

            Assert.Equal(MarkerType.VFX, vfx.Type);
            Assert.Equal("vfx_path", vfx.Data);
            Assert.Equal(new Vector3(1, 2, 3), vfx.Position);

            Assert.Equal(MarkerType.CameraFocus, cameraFocus.Type);
            Assert.Equal("target1", cameraFocus.TargetId);

            Assert.Equal(MarkerType.CameraRelease, cameraRelease.Type);
        }

        [Fact]
        public void MeleeAttack_Factory_ConfiguredCorrectly()
        {
            bool hitCalled = false;
            var timeline = ActionTimeline.MeleeAttack(() => hitCalled = true, 0.3f, 0.6f);

            Assert.Equal(0.6f, timeline.Duration);
            Assert.True(timeline.HasMarkerType(MarkerType.Start));
            Assert.True(timeline.HasMarkerType(MarkerType.Hit));
            Assert.True(timeline.HasMarkerType(MarkerType.AnimationEnd));
            Assert.True(timeline.HasMarkerType(MarkerType.CameraFocus));
            Assert.True(timeline.HasMarkerType(MarkerType.CameraRelease));

            timeline.Play();
            timeline.Process(0.5f);

            Assert.True(hitCalled);
        }

        [Fact]
        public void RangedAttack_Factory_IncludesProjectile()
        {
            bool projectileCalled = false;
            bool hitCalled = false;
            var timeline = ActionTimeline.RangedAttack(
                () => projectileCalled = true,
                () => hitCalled = true,
                0.2f,
                0.5f);

            Assert.True(timeline.HasMarkerType(MarkerType.Projectile));
            Assert.True(timeline.HasMarkerType(MarkerType.Hit));

            timeline.Play();
            timeline.Process(0.3f);
            Assert.True(projectileCalled);
            Assert.False(hitCalled);

            timeline.Process(0.3f);
            Assert.True(hitCalled);
        }

        [Fact]
        public void SpellCast_Factory_IncludesVFX()
        {
            bool castCalled = false;
            var timeline = ActionTimeline.SpellCast(() => castCalled = true, 1.0f, 1.2f);

            Assert.True(timeline.HasMarkerType(MarkerType.VFX));
            Assert.True(timeline.HasMarkerType(MarkerType.Sound));
            Assert.True(timeline.HasMarkerType(MarkerType.Hit));
            Assert.Equal(1.2f, timeline.Duration);

            timeline.Play();
            timeline.Process(1.1f);

            Assert.True(castCalled);
        }

        [Fact]
        public void Timeline_GetNextMarker_ReturnsUntriggered()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.Hit(0.3f));
            timeline.AddMarker(TimelineMarker.Sound(0.5f, "test.wav"));
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();

            var next = timeline.GetNextMarker();
            Assert.Equal(MarkerType.Hit, next.Type);

            timeline.Process(0.4f);
            next = timeline.GetNextMarker();
            Assert.Equal(MarkerType.Sound, next.Type);
        }

        [Fact]
        public void Timeline_Progress_CalculatesCorrectly()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.End(1.0f));
            timeline.Play();

            timeline.Process(0.5f);

            Assert.Equal(0.5f, timeline.Progress);
        }

        [Fact]
        public void Timeline_Movement_Factory_WorksCorrectly()
        {
            bool completeCalled = false;
            var timeline = ActionTimeline.Movement(2.0f, () => completeCalled = true);

            Assert.Equal(2.0f, timeline.Duration);
            timeline.Play();
            timeline.Process(2.5f);

            Assert.True(completeCalled);
            Assert.Equal(TimelineState.Completed, timeline.State);
        }

        [Fact]
        public void Timeline_Markers_ReadOnlyList()
        {
            var timeline = new ActionTimeline("test");
            timeline.AddMarker(TimelineMarker.Hit(0.3f));
            timeline.AddMarker(TimelineMarker.End(1.0f));

            var markers = timeline.Markers;

            Assert.Equal(2, markers.Count);
            Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<TimelineMarker>>(markers);
        }

        // Note: SkipAnimations tests require Godot runtime and are verified via autobattle/headless tests
    }
}
