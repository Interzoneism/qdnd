using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QDND.Combat.Animation
{
    /// <summary>
    /// State of a timeline.
    /// </summary>
    public enum TimelineState
    {
        Idle,
        Playing,
        Paused,
        Completed,
        Cancelled
    }

    /// <summary>
    /// Event data for timeline signals.
    /// </summary>
    public class TimelineEvent
    {
        public string TimelineId { get; set; }
        public TimelineMarker Marker { get; set; }
        public float CurrentTime { get; set; }
    }

    /// <summary>
    /// Sequences animation events for a combat action.
    /// </summary>
    public class ActionTimeline
    {
        // Events for timeline state changes
        public event Action<string, MarkerType> MarkerTriggered;
        public event Action TimelineCompleted;
        public event Action TimelineCancelled;
        public event Action Hit;
        public event Action<TimelineState> StateChanged;

        private readonly List<TimelineMarker> _markers = new();
        private readonly string _id;
        private TimelineState _state = TimelineState.Idle;
        private float _currentTime;
        private float _duration;
        private float _playbackSpeed = 1f;
        private Action _onComplete;

        public string Id => _id;
        public TimelineState State => _state;
        public float CurrentTime => _currentTime;
        public float Duration => _duration;
        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Math.Max(0.1f, value);
        }
        public bool IsPlaying => _state == TimelineState.Playing;
        public float Progress => _duration > 0 ? _currentTime / _duration : 0;
        public IReadOnlyList<TimelineMarker> Markers => _markers.AsReadOnly();

        public ActionTimeline(string actionName = null)
        {
            _id = actionName ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Add a marker to the timeline.
        /// </summary>
        public ActionTimeline AddMarker(TimelineMarker marker)
        {
            _markers.Add(marker);
            UpdateDuration();
            return this;
        }

        /// <summary>
        /// Add hit marker with callback.
        /// </summary>
        public ActionTimeline OnHit(float time, Action callback = null)
        {
            AddMarker(TimelineMarker.Hit(time, callback));
            return this;
        }

        /// <summary>
        /// Add completion callback.
        /// </summary>
        public ActionTimeline OnComplete(Action callback)
        {
            _onComplete = callback;
            return this;
        }

        /// <summary>
        /// Start playing the timeline.
        /// </summary>
        public void Play()
        {
            if (_state == TimelineState.Playing) return;
            
            _state = TimelineState.Playing;
            _currentTime = 0;
            
            // Reset all markers
            foreach (var marker in _markers)
            {
                marker.Triggered = false;
            }
            
            // Trigger start marker immediately
            var startMarker = _markers.FirstOrDefault(m => m.Type == MarkerType.Start);
            if (startMarker != null)
            {
                TriggerMarker(startMarker);
            }
            
            StateChanged?.Invoke(_state);
        }

        /// <summary>
        /// Pause the timeline.
        /// </summary>
        public void Pause()
        {
            if (_state != TimelineState.Playing) return;
            
            _state = TimelineState.Paused;
            StateChanged?.Invoke(_state);
        }

        /// <summary>
        /// Resume a paused timeline.
        /// </summary>
        public void Resume()
        {
            if (_state != TimelineState.Paused) return;
            
            _state = TimelineState.Playing;
            StateChanged?.Invoke(_state);
        }

        /// <summary>
        /// Cancel the timeline.
        /// </summary>
        public void Cancel()
        {
            _state = TimelineState.Cancelled;
            TimelineCancelled?.Invoke();
            StateChanged?.Invoke(_state);
        }

        /// <summary>
        /// Skip to a specific time.
        /// </summary>
        public void SkipTo(float time)
        {
            float oldTime = _currentTime;
            _currentTime = Math.Clamp(time, 0, _duration);
            
            // Trigger any markers we skipped
            foreach (var marker in _markers.Where(m => !m.Triggered && m.Time <= _currentTime && m.Time >= oldTime))
            {
                TriggerMarker(marker);
            }
        }

        /// <summary>
        /// Process a time delta (call each frame).
        /// </summary>
        public void Process(float delta)
        {
            if (_state != TimelineState.Playing) return;
            
            _currentTime += delta * _playbackSpeed;
            
            // Check markers
            foreach (var marker in _markers.Where(m => !m.Triggered && m.Time <= _currentTime).ToList())
            {
                TriggerMarker(marker);
            }
            
            // Check completion
            if (_currentTime >= _duration)
            {
                Complete();
            }
        }

        /// <summary>
        /// Get next untriggered marker.
        /// </summary>
        public TimelineMarker GetNextMarker()
        {
            return _markers
                .Where(m => !m.Triggered)
                .OrderBy(m => m.Time)
                .FirstOrDefault();
        }

        /// <summary>
        /// Check if timeline has any markers of type.
        /// </summary>
        public bool HasMarkerType(MarkerType type)
        {
            return _markers.Any(m => m.Type == type);
        }

        private void TriggerMarker(TimelineMarker marker)
        {
            if (marker.Triggered) return;
            
            marker.Triggered = true;
            marker.Callback?.Invoke();
            
            MarkerTriggered?.Invoke(marker.Id, marker.Type);
            
            if (marker.Type == MarkerType.Hit)
            {
                Hit?.Invoke();
            }
        }

        private void Complete()
        {
            _state = TimelineState.Completed;
            _onComplete?.Invoke();
            TimelineCompleted?.Invoke();
            StateChanged?.Invoke(_state);
        }

        private void UpdateDuration()
        {
            _duration = _markers.Any() ? _markers.Max(m => m.Time) : 0;
        }

        // Factory methods for common timelines

        /// <summary>
        /// Create a simple melee attack timeline.
        /// </summary>
        public static ActionTimeline MeleeAttack(Action onHit, float hitTime = 0.3f, float totalDuration = 0.6f)
        {
            return new ActionTimeline("melee_attack")
                .AddMarker(TimelineMarker.Start())
                .AddMarker(TimelineMarker.CameraFocus(0f, null))
                .OnHit(hitTime, onHit)
                .AddMarker(TimelineMarker.CameraRelease(totalDuration - 0.1f))
                .AddMarker(TimelineMarker.End(totalDuration));
        }

        /// <summary>
        /// Create a ranged attack timeline with projectile.
        /// </summary>
        public static ActionTimeline RangedAttack(Action onProjectile, Action onHit, float projectileTime = 0.2f, float hitTime = 0.5f)
        {
            return new ActionTimeline("ranged_attack")
                .AddMarker(TimelineMarker.Start())
                .AddMarker(TimelineMarker.Projectile(projectileTime, onProjectile))
                .OnHit(hitTime, onHit)
                .AddMarker(TimelineMarker.End(hitTime + 0.2f));
        }

        /// <summary>
        /// Create a spell cast timeline.
        /// </summary>
        public static ActionTimeline SpellCast(Action onCast, float castTime = 1f, float effectTime = 1.2f)
        {
            return new ActionTimeline("spell_cast")
                .AddMarker(TimelineMarker.Start())
                .AddMarker(TimelineMarker.VFX(0f, "cast_start", null))
                .AddMarker(TimelineMarker.Sound(0f, "cast_sound"))
                .OnHit(castTime, onCast)
                .AddMarker(TimelineMarker.End(effectTime));
        }

        /// <summary>
        /// Create movement timeline.
        /// </summary>
        public static ActionTimeline Movement(float duration, Action onComplete = null)
        {
            var timeline = new ActionTimeline("movement")
                .AddMarker(TimelineMarker.Start())
                .AddMarker(TimelineMarker.End(duration));
            
            if (onComplete != null)
            {
                timeline.OnComplete(onComplete);
            }
            
            return timeline;
        }
    }
}
