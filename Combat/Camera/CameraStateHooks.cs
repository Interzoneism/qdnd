using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QDND.Combat.Camera
{
    /// <summary>
    /// State of the camera system.
    /// </summary>
    public enum CameraState
    {
        Free,          // User control
        Following,     // Following active combatant
        Focused,       // Fixed focus on target
        Transitioning, // Moving between focuses
        Cinematics     // Cutscene mode
    }

    /// <summary>
    /// Camera state and focus management.
    /// </summary>
    public class CameraStateHooks
    {
        public event Action<string, int> FocusChanged;
        public event Action<int> StateChanged;
        public event Action<float> SlowMotionStarted;
        public event Action SlowMotionEnded;
        public event Action<float> TransitionStarted;
        public event Action TransitionCompleted;

        private readonly Queue<CameraFocusRequest> _requestQueue = new();
        private CameraFocusRequest _currentRequest;
        private CameraState _state = CameraState.Free;
        private float _requestTimer;
        private float _transitionTimer;
        private bool _isSlowMotion;
        private string _followTargetId;

        public CameraState State => _state;
        public CameraFocusRequest CurrentRequest => _currentRequest;
        public bool IsSlowMotion => _isSlowMotion;
        public string FollowTargetId => _followTargetId;
        public int QueuedRequests => _requestQueue.Count;

        /// <summary>
        /// Request a camera focus.
        /// </summary>
        public void RequestFocus(CameraFocusRequest request)
        {
            if (request == null) return;

            // If higher priority than current, interrupt
            if (_currentRequest != null && request.Priority > _currentRequest.Priority)
            {
                EndCurrentRequest();
            }

            // If no current request or equal/higher priority
            if (_currentRequest == null)
            {
                StartRequest(request);
            }
            else
            {
                _requestQueue.Enqueue(request);
            }
        }

        /// <summary>
        /// Follow a combatant with the camera.
        /// </summary>
        public void FollowCombatant(string combatantId)
        {
            _followTargetId = combatantId;
            if (_state == CameraState.Free)
            {
                SetState(CameraState.Following);
            }
        }

        /// <summary>
        /// Release camera to free mode.
        /// </summary>
        public void ReleaseFocus()
        {
            EndCurrentRequest();
            _requestQueue.Clear();
            SetState(CameraState.Free);
        }

        /// <summary>
        /// Process camera state (call each frame).
        /// </summary>
        public void Process(float delta)
        {
            // Handle transition
            if (_state == CameraState.Transitioning)
            {
                _transitionTimer -= delta;
                if (_transitionTimer <= 0)
                {
                    SetState(CameraState.Focused);
                    TransitionCompleted?.Invoke();
                }
                return;
            }

            // Handle current request timer
            if (_currentRequest != null && _currentRequest.Duration > 0)
            {
                _requestTimer -= delta;
                if (_requestTimer <= 0)
                {
                    EndCurrentRequest();
                    ProcessQueue();
                }
            }
        }

        /// <summary>
        /// Get current focus position.
        /// </summary>
        public Vector3? GetCurrentFocusPosition()
        {
            if (_currentRequest == null) return null;

            return _currentRequest.Type switch
            {
                CameraFocusType.Position or CameraFocusType.AoE => _currentRequest.Position,
                CameraFocusType.Combatant or CameraFocusType.TwoShot => null, // Would look up combatant position
                _ => null
            };
        }

        /// <summary>
        /// Get camera parameters for current focus.
        /// </summary>
        public (float distance, float angle) GetCameraParameters()
        {
            float distance = _currentRequest?.DistanceOverride ?? 15f;
            float angle = _currentRequest?.AngleOverride ?? 45f;
            return (distance, angle);
        }

        /// <summary>
        /// Check if should use slow motion.
        /// </summary>
        public float GetTimeScale()
        {
            if (_isSlowMotion && _currentRequest != null)
            {
                return _currentRequest.SlowMotionScale;
            }
            return 1f;
        }

        /// <summary>
        /// Clear all requests.
        /// </summary>
        public void ClearQueue()
        {
            _requestQueue.Clear();
        }

        /// <summary>
        /// Override current request with critical focus.
        /// </summary>
        public void ForceFocus(CameraFocusRequest request)
        {
            if (request == null) return;

            request.Priority = CameraPriority.Critical;
            EndCurrentRequest();
            _requestQueue.Clear();
            StartRequest(request);
        }

        /// <summary>
        /// Check if the camera can accept new requests.
        /// </summary>
        public bool CanAcceptRequest(CameraPriority priority)
        {
            if (_currentRequest == null) return true;
            return priority >= _currentRequest.Priority;
        }

        private void StartRequest(CameraFocusRequest request)
        {
            _currentRequest = request;
            _requestTimer = request.Duration;

            // Start transition
            if (request.TransitionTime > 0)
            {
                _transitionTimer = request.TransitionTime;
                SetState(CameraState.Transitioning);
                TransitionStarted?.Invoke(request.TransitionTime);
            }
            else
            {
                SetState(CameraState.Focused);
            }

            // Handle slow motion
            if (request.SlowMotion && !_isSlowMotion)
            {
                _isSlowMotion = true;
                SlowMotionStarted?.Invoke(request.SlowMotionScale);
            }

            FocusChanged?.Invoke(request.Id, (int)request.Type);
        }

        private void EndCurrentRequest()
        {
            if (_currentRequest == null) return;

            if (_isSlowMotion)
            {
                _isSlowMotion = false;
                SlowMotionEnded?.Invoke();
            }

            _currentRequest = null;
        }

        private void ProcessQueue()
        {
            if (_requestQueue.Count > 0)
            {
                var next = _requestQueue.Dequeue();
                StartRequest(next);
            }
            else
            {
                SetState(_followTargetId != null ? CameraState.Following : CameraState.Free);
            }
        }

        private void SetState(CameraState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                StateChanged?.Invoke((int)_state);
            }
        }
    }
}
