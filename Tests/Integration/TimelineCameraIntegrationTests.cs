using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Animation;
using QDND.Combat.Camera;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using Godot;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for timeline-driven camera focus via CameraStateHooks.
    /// Verifies that timeline CameraFocus/CameraRelease markers trigger
    /// correct camera state transitions in a headless environment.
    /// </summary>
    public class TimelineCameraIntegrationTests
    {
        [Fact]
        public void CameraFocusMarker_TransitionsCameraState_FromFreeToFocused()
        {
            // Arrange
            var cameraHooks = new CameraStateHooks();
            var timeline = new ActionTimeline("camera_test");

            var targetCombatantId = "target_001";

            // Add camera focus marker at 0.3s
            timeline.AddMarker(TimelineMarker.CameraFocus(0.3f, targetCombatantId));

            var stateChanges = new List<CameraState>();
            cameraHooks.StateChanged += (state) => stateChanges.Add((CameraState)state);

            var focusChanges = new List<(string requestId, int focusType)>();
            cameraHooks.FocusChanged += (requestId, focusType) => focusChanges.Add((requestId, focusType));

            timeline.Play();

            // Act - Process timeline and camera simultaneously
            Assert.Equal(CameraState.Free, cameraHooks.State);

            // Process 0.2s (before marker)
            timeline.Process(0.2f);
            cameraHooks.Process(0.2f);
            Assert.Equal(CameraState.Free, cameraHooks.State);

            // Handle marker when it triggers
            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                if (markerType == MarkerType.CameraFocus)
                {
                    var marker = timeline.Markers.First(m => m.Id == markerId);
                    var request = QDND.Combat.Camera.CameraFocusRequest.FocusCombatant(
                        marker.TargetId,
                        2.0f,
                        CameraPriority.Normal
                    );
                    request.TransitionTime = 0.2f;
                    cameraHooks.RequestFocus(request);
                }
            };

            // Process 0.15s more (total 0.35s, past marker at 0.3s)
            timeline.Process(0.15f);
            cameraHooks.Process(0.0f); // Process camera immediately to handle request

            // Assert - Camera should be transitioning
            Assert.Contains(CameraState.Transitioning, stateChanges);
            Assert.Equal(CameraState.Transitioning, cameraHooks.State);

            // Process transition duration (0.2s)
            cameraHooks.Process(0.2f);

            // Assert - Camera should now be focused
            Assert.Contains(CameraState.Focused, stateChanges);
            Assert.Equal(CameraState.Focused, cameraHooks.State);
            Assert.Single(focusChanges);
            Assert.Equal((int)CameraFocusType.Combatant, focusChanges[0].focusType);
            Assert.NotNull(cameraHooks.CurrentRequest);
            Assert.Equal(targetCombatantId, cameraHooks.CurrentRequest.TargetId);
        }

        [Fact]
        public void CameraReleaseMarker_ReturnsCameraToFree()
        {
            // Arrange
            var cameraHooks = new CameraStateHooks();
            var timeline = new ActionTimeline("camera_release_test");

            var targetId = "combatant_001";

            // Add focus at 0.2s, release at 1.0s
            timeline.AddMarker(TimelineMarker.CameraFocus(0.2f, targetId));
            timeline.AddMarker(TimelineMarker.CameraRelease(1.0f));

            var stateChanges = new List<CameraState>();
            cameraHooks.StateChanged += (state) => stateChanges.Add((CameraState)state);

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                if (markerType == MarkerType.CameraFocus)
                {
                    var marker = timeline.Markers.First(m => m.Id == markerId);
                    var request = QDND.Combat.Camera.CameraFocusRequest.FocusCombatant(
                        marker.TargetId,
                        2.0f,
                        CameraPriority.Normal
                    );
                    request.TransitionTime = 0.1f;
                    cameraHooks.RequestFocus(request);
                }
                else if (markerType == MarkerType.CameraRelease)
                {
                    cameraHooks.ReleaseFocus();
                }
            };

            timeline.Play();

            // Act - Process to focus marker
            timeline.Process(0.3f);
            cameraHooks.Process(0.0f); // Handle focus request
            cameraHooks.Process(0.15f); // Complete transition

            Assert.Equal(CameraState.Focused, cameraHooks.State);

            // Process to release marker
            timeline.Process(0.8f); // Total 1.1s, past release at 1.0s

            // Assert - Camera should be free
            Assert.Contains(CameraState.Free, stateChanges);
            Assert.Equal(CameraState.Free, cameraHooks.State);
        }

        [Fact]
        public void MultipleCameraMarkers_ProcessedInSequence()
        {
            // Arrange
            var cameraHooks = new CameraStateHooks();
            var timeline = new ActionTimeline("multi_camera_test");

            // Simulate ability sequence: focus attacker → focus target → release
            timeline.AddMarker(TimelineMarker.CameraFocus(0.0f, "attacker"));
            timeline.AddMarker(TimelineMarker.CameraFocus(0.5f, "target"));
            timeline.AddMarker(TimelineMarker.CameraRelease(1.2f));

            var focusChanges = new List<string>();
            cameraHooks.FocusChanged += (targetId, _) => focusChanges.Add(targetId);

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                if (markerType == MarkerType.CameraFocus)
                {
                    var marker = timeline.Markers.First(m => m.Id == markerId);
                    // Second marker gets higher priority to immediately interrupt first
                    var priority = marker.Time > 0.1f ? CameraPriority.High : CameraPriority.Normal;
                    var request = QDND.Combat.Camera.CameraFocusRequest.FocusCombatant(
                        marker.TargetId,
                        1.0f,
                        priority
                    );
                    request.TransitionTime = 0.1f;
                    cameraHooks.RequestFocus(request);
                }
                else if (markerType == MarkerType.CameraRelease)
                {
                    cameraHooks.ReleaseFocus();
                }
            };

            timeline.Play();

            // Act - Process timeline in steps
            // Step 1: Trigger first focus (attacker at 0.0s)
            timeline.Process(0.05f);
            cameraHooks.Process(0.0f);
            cameraHooks.Process(0.15f); // Complete transition

            Assert.Equal(CameraState.Focused, cameraHooks.State);
            Assert.Single(focusChanges);
            Assert.NotNull(cameraHooks.CurrentRequest);
            Assert.Equal("attacker", cameraHooks.CurrentRequest.TargetId);

            // Step 2: Trigger second focus (target at 0.5s) with higher priority to interrupt
            timeline.Process(0.5f);
            cameraHooks.Process(0.0f); // Handle the interrupting request
            cameraHooks.Process(0.15f); // Complete second transition

            Assert.Equal(CameraState.Focused, cameraHooks.State);
            Assert.Equal(2, focusChanges.Count);
            Assert.NotNull(cameraHooks.CurrentRequest);
            Assert.Equal("target", cameraHooks.CurrentRequest.TargetId);

            // Step 3: Release at 1.2s
            timeline.Process(0.75f);

            Assert.Equal(CameraState.Free, cameraHooks.State);
        }

        [Fact]
        public void CameraStateHooks_TransitionEvents_FireCorrectly()
        {
            // Arrange
            var cameraHooks = new CameraStateHooks();
            var timeline = new ActionTimeline("transition_test");

            timeline.AddMarker(TimelineMarker.CameraFocus(0.1f, "target"));

            var transitionStarted = false;
            var transitionCompleted = false;
            float transitionDuration = 0f;

            cameraHooks.TransitionStarted += (duration) =>
            {
                transitionStarted = true;
                transitionDuration = duration;
            };
            cameraHooks.TransitionCompleted += () => transitionCompleted = true;

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                if (markerType == MarkerType.CameraFocus)
                {
                    var marker = timeline.Markers.First(m => m.Id == markerId);
                    var request = QDND.Combat.Camera.CameraFocusRequest.FocusCombatant(
                        marker.TargetId,
                        1.0f,
                        CameraPriority.Normal
                    );
                    request.TransitionTime = 0.3f;
                    cameraHooks.RequestFocus(request);
                }
            };

            timeline.Play();

            // Act
            timeline.Process(0.15f);
            cameraHooks.Process(0.0f);

            Assert.True(transitionStarted);
            Assert.Equal(0.3f, transitionDuration);
            Assert.False(transitionCompleted);

            cameraHooks.Process(0.3f);

            // Assert
            Assert.True(transitionCompleted);
        }

        [Fact]
        public void HighPriorityCameraRequest_InterruptsLowerPriority()
        {
            // Arrange
            var cameraHooks = new CameraStateHooks();
            var timeline = new ActionTimeline("priority_test");

            // Normal priority focus at 0.1s, critical priority at 0.3s
            timeline.AddMarker(TimelineMarker.CameraFocus(0.1f, "target1"));
            timeline.AddMarker(TimelineMarker.CameraFocus(0.3f, "target2"));

            var focusChanges = new List<(string requestId, int focusType)>();
            cameraHooks.FocusChanged += (requestId, focusType) => focusChanges.Add((requestId, focusType));

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                if (markerType == MarkerType.CameraFocus)
                {
                    var marker = timeline.Markers.First(m => m.Id == markerId);

                    // First marker: normal priority
                    // Second marker: critical priority (should interrupt)
                    var priority = marker.Time < 0.2f ? CameraPriority.Normal : CameraPriority.Critical;

                    var request = QDND.Combat.Camera.CameraFocusRequest.FocusCombatant(
                        marker.TargetId,
                        2.0f,
                        priority
                    );
                    request.TransitionTime = 0.1f;
                    cameraHooks.RequestFocus(request);
                }
            };

            timeline.Play();

            // Act
            timeline.Process(0.15f);
            cameraHooks.Process(0.0f);
            cameraHooks.Process(0.15f);

            Assert.Single(focusChanges);
            Assert.NotNull(cameraHooks.CurrentRequest);
            Assert.Equal("target1", cameraHooks.CurrentRequest.TargetId);
            Assert.Equal(CameraState.Focused, cameraHooks.State);

            // Critical priority request
            timeline.Process(0.2f);
            cameraHooks.Process(0.0f);
            cameraHooks.Process(0.15f);

            // Assert - Should have interrupted and focused on target2
            Assert.Equal(2, focusChanges.Count);
            Assert.NotNull(cameraHooks.CurrentRequest);
            Assert.Equal("target2", cameraHooks.CurrentRequest.TargetId);
        }

        [Fact]
        public void CameraFocusWithPosition_HandledCorrectly()
        {
            // Arrange
            var cameraHooks = new CameraStateHooks();
            var timeline = new ActionTimeline("position_focus_test");

            var focusPosition = new Vector3(10f, 0f, 5f);
            var marker = new TimelineMarker(MarkerType.CameraFocus, 0.2f)
            {
                Position = focusPosition
            };
            timeline.AddMarker(marker);

            var focusChanges = new List<(string targetId, int focusType)>();
            cameraHooks.FocusChanged += (targetId, focusType) => focusChanges.Add((targetId, focusType));

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                if (markerType == MarkerType.CameraFocus)
                {
                    var m = timeline.Markers.First(x => x.Id == markerId);

                    // Create position-based focus request
                    var request = new QDND.Combat.Camera.CameraFocusRequest
                    {
                        Type = CameraFocusType.Position,
                        Position = m.Position,
                        Duration = 1.0f,
                        Priority = CameraPriority.Normal,
                        TransitionTime = 0.1f
                    };
                    cameraHooks.RequestFocus(request);
                }
            };

            timeline.Play();

            // Act
            timeline.Process(0.25f);
            cameraHooks.Process(0.0f);
            cameraHooks.Process(0.15f);

            // Assert
            Assert.Equal(CameraState.Focused, cameraHooks.State);
            Assert.Single(focusChanges);
            Assert.NotNull(cameraHooks.CurrentRequest);
            Assert.Equal((int)CameraFocusType.Position, focusChanges[0].focusType);

            var currentFocusPos = cameraHooks.GetCurrentFocusPosition();
            Assert.NotNull(currentFocusPos);
            Assert.Equal(focusPosition, currentFocusPos.Value);
        }

        [Fact]
        public void CameraFocusAndRelease_TransitionsInOrderedSequence()
        {
            // Arrange
            var cameraHooks = new CameraStateHooks();
            var timeline = new ActionTimeline("ordered_sequence_test");

            var targetId = "target_001";

            // Add focus at 0.2s, release at 1.0s
            timeline.AddMarker(TimelineMarker.CameraFocus(0.2f, targetId));
            timeline.AddMarker(TimelineMarker.CameraRelease(1.0f));

            // Track state changes in order
            var stateSequence = new List<CameraState>();
            cameraHooks.StateChanged += (state) => stateSequence.Add((CameraState)state);

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                if (markerType == MarkerType.CameraFocus)
                {
                    var marker = timeline.Markers.First(m => m.Id == markerId);
                    var request = QDND.Combat.Camera.CameraFocusRequest.FocusCombatant(
                        marker.TargetId,
                        2.0f,
                        CameraPriority.Normal
                    );
                    request.TransitionTime = 0.15f;
                    cameraHooks.RequestFocus(request);
                }
                else if (markerType == MarkerType.CameraRelease)
                {
                    cameraHooks.ReleaseFocus();
                }
            };

            timeline.Play();

            // Act - Process through focus marker
            Assert.Equal(CameraState.Free, cameraHooks.State);

            timeline.Process(0.25f); // Past focus marker at 0.2s
            cameraHooks.Process(0.0f); // Handle focus request

            // Assert - Should be transitioning
            Assert.Equal(CameraState.Transitioning, cameraHooks.State);
            Assert.Single(stateSequence);
            Assert.Equal(CameraState.Transitioning, stateSequence[0]);

            // Complete transition
            cameraHooks.Process(0.2f);

            // Assert - Should now be focused
            Assert.Equal(CameraState.Focused, cameraHooks.State);
            Assert.Equal(2, stateSequence.Count);
            Assert.Equal(CameraState.Focused, stateSequence[1]);

            // Process to release marker
            timeline.Process(0.85f); // Total 1.1s, past release at 1.0s

            // Assert - Should return to free in exact order
            Assert.Equal(CameraState.Free, cameraHooks.State);
            Assert.Equal(3, stateSequence.Count);

            // Verify exact ordered sequence: Free → Transitioning → Focused → Free
            // (Free is initial state, not in stateSequence)
            Assert.Equal(CameraState.Transitioning, stateSequence[0]);
            Assert.Equal(CameraState.Focused, stateSequence[1]);
            Assert.Equal(CameraState.Free, stateSequence[2]);
        }
    }
}
