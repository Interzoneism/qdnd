using Xunit;
using QDND.Combat.Camera;
using Godot;

namespace QDND.Tests.Unit
{
    public class CameraStateTests
    {
        [Fact]
        public void RequestFocus_StartsRequest()
        {
            var hooks = new CameraStateHooks();
            var request = CameraFocusRequest.FocusCombatant("target1");
            request.TransitionTime = 0; // Skip transition

            hooks.RequestFocus(request);

            Assert.NotNull(hooks.CurrentRequest);
            Assert.Equal(CameraState.Focused, hooks.State);
        }

        [Fact]
        public void RequestFocus_QueuesLowerPriority()
        {
            var hooks = new CameraStateHooks();
            var high = new CameraFocusRequest { Priority = CameraPriority.High, TransitionTime = 0 };
            var low = new CameraFocusRequest { Priority = CameraPriority.Low };

            hooks.RequestFocus(high);
            hooks.RequestFocus(low);

            Assert.Equal(1, hooks.QueuedRequests);
            Assert.Equal(high.Id, hooks.CurrentRequest.Id);
        }

        [Fact]
        public void RequestFocus_InterruptsLowerPriority()
        {
            var hooks = new CameraStateHooks();
            var low = new CameraFocusRequest { Priority = CameraPriority.Low, TransitionTime = 0 };
            var critical = new CameraFocusRequest { Priority = CameraPriority.Critical, TransitionTime = 0 };

            hooks.RequestFocus(low);
            hooks.RequestFocus(critical);

            Assert.Equal(critical.Id, hooks.CurrentRequest.Id);
        }

        [Fact]
        public void Process_ExpiresRequestAfterDuration()
        {
            var hooks = new CameraStateHooks();
            var request = new CameraFocusRequest { Duration = 0.5f, TransitionTime = 0 };

            hooks.RequestFocus(request);
            hooks.Process(0.6f);

            Assert.Null(hooks.CurrentRequest);
            Assert.Equal(CameraState.Free, hooks.State);
        }

        [Fact]
        public void Process_ProcessesQueueAfterExpiry()
        {
            var hooks = new CameraStateHooks();
            var first = new CameraFocusRequest { Duration = 0.5f, TransitionTime = 0, Priority = CameraPriority.Normal };
            var second = new CameraFocusRequest { Duration = 1f, TransitionTime = 0, Priority = CameraPriority.Normal };

            hooks.RequestFocus(first);
            hooks.RequestFocus(second);
            hooks.Process(0.6f);

            Assert.Equal(second.Id, hooks.CurrentRequest.Id);
        }

        [Fact]
        public void ReleaseFocus_ClearsAll()
        {
            var hooks = new CameraStateHooks();
            var request = new CameraFocusRequest { TransitionTime = 0 };

            hooks.RequestFocus(request);
            hooks.RequestFocus(new CameraFocusRequest());
            hooks.ReleaseFocus();

            Assert.Null(hooks.CurrentRequest);
            Assert.Equal(0, hooks.QueuedRequests);
            Assert.Equal(CameraState.Free, hooks.State);
        }

        [Fact]
        public void FollowCombatant_SetsFollowState()
        {
            var hooks = new CameraStateHooks();

            hooks.FollowCombatant("combatant1");

            Assert.Equal("combatant1", hooks.FollowTargetId);
            Assert.Equal(CameraState.Following, hooks.State);
        }

        [Fact]
        public void ForceFocus_ClearsQueueAndFocuses()
        {
            var hooks = new CameraStateHooks();
            var queued = new CameraFocusRequest { TransitionTime = 0 };
            var forced = new CameraFocusRequest { TransitionTime = 0 };

            hooks.RequestFocus(queued);
            hooks.RequestFocus(new CameraFocusRequest());
            hooks.ForceFocus(forced);

            Assert.Equal(forced.Id, hooks.CurrentRequest.Id);
            Assert.Equal(0, hooks.QueuedRequests);
        }

        [Fact]
        public void SlowMotion_TrackedCorrectly()
        {
            var hooks = new CameraStateHooks();
            var request = new CameraFocusRequest
            {
                SlowMotion = true,
                SlowMotionScale = 0.25f,
                TransitionTime = 0
            };

            hooks.RequestFocus(request);

            Assert.True(hooks.IsSlowMotion);
            Assert.Equal(0.25f, hooks.GetTimeScale());
        }

        [Fact]
        public void Transition_CompletesAfterTime()
        {
            var hooks = new CameraStateHooks();
            var request = new CameraFocusRequest
            {
                TransitionTime = 0.3f,
                Duration = 1f
            };

            hooks.RequestFocus(request);
            Assert.Equal(CameraState.Transitioning, hooks.State);

            hooks.Process(0.4f);
            Assert.Equal(CameraState.Focused, hooks.State);
        }

        [Fact]
        public void CameraParameters_ReflectRequest()
        {
            var hooks = new CameraStateHooks();
            var request = new CameraFocusRequest
            {
                DistanceOverride = 20f,
                AngleOverride = 60f,
                TransitionTime = 0
            };

            hooks.RequestFocus(request);
            var (distance, angle) = hooks.GetCameraParameters();

            Assert.Equal(20f, distance);
            Assert.Equal(60f, angle);
        }

        [Fact]
        public void TimeScale_ReturnsSlowMotionScale()
        {
            var hooks = new CameraStateHooks();

            Assert.Equal(1f, hooks.GetTimeScale()); // Normal

            var slowMo = new CameraFocusRequest
            {
                SlowMotion = true,
                SlowMotionScale = 0.2f,
                TransitionTime = 0
            };
            hooks.RequestFocus(slowMo);

            Assert.Equal(0.2f, hooks.GetTimeScale());
        }

        [Fact]
        public void CanAcceptRequest_ChecksPriority()
        {
            var hooks = new CameraStateHooks();
            var high = new CameraFocusRequest { Priority = CameraPriority.High, TransitionTime = 0 };

            hooks.RequestFocus(high);

            Assert.False(hooks.CanAcceptRequest(CameraPriority.Low));
            Assert.True(hooks.CanAcceptRequest(CameraPriority.High));
            Assert.True(hooks.CanAcceptRequest(CameraPriority.Critical));
        }

        [Fact]
        public void FactoryMethods_CreateCorrectRequests()
        {
            var combatant = CameraFocusRequest.FocusCombatant("c1", 2f);
            Assert.Equal(CameraFocusType.Combatant, combatant.Type);
            Assert.Equal("c1", combatant.TargetId);
            Assert.Equal(2f, combatant.Duration);

            var twoShot = CameraFocusRequest.TwoShot("a1", "t1");
            Assert.Equal(CameraFocusType.TwoShot, twoShot.Type);
            Assert.Equal("a1", twoShot.TargetId);
            Assert.Equal("t1", twoShot.SecondaryTargetId);

            var aoe = CameraFocusRequest.FocusAoE(Vector3.Zero, 10f);
            Assert.Equal(CameraFocusType.AoE, aoe.Type);
            Assert.Equal(10f, aoe.Radius);

            var overhead = CameraFocusRequest.Overhead();
            Assert.Equal(CameraFocusType.Overhead, overhead.Type);
            Assert.Equal(75f, overhead.AngleOverride);

            var crit = CameraFocusRequest.CriticalHit("t1");
            Assert.True(crit.SlowMotion);
            Assert.Equal(CameraPriority.Critical, crit.Priority);

            var death = CameraFocusRequest.Death("t1");
            Assert.True(death.SlowMotion);
            Assert.Equal(2f, death.Duration);

            var release = CameraFocusRequest.Release();
            Assert.Equal(CameraFocusType.Free, release.Type);
        }
    }
}
