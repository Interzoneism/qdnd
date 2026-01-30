using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Rules;

namespace QDND.Combat.Reactions
{
    /// <summary>
    /// An item on the resolution stack.
    /// </summary>
    public class StackItem
    {
        /// <summary>
        /// Unique ID for this stack item.
        /// </summary>
        public string ItemId { get; } = Guid.NewGuid().ToString("N")[..8];
        
        /// <summary>
        /// Type of action being resolved.
        /// </summary>
        public string ActionType { get; set; }
        
        /// <summary>
        /// The event being resolved.
        /// </summary>
        public RuleEvent Event { get; set; }
        
        /// <summary>
        /// Trigger context if this is a reaction.
        /// </summary>
        public ReactionTriggerContext TriggerContext { get; set; }
        
        /// <summary>
        /// Who is performing this action.
        /// </summary>
        public string SourceId { get; set; }
        
        /// <summary>
        /// Who is targeted.
        /// </summary>
        public string TargetId { get; set; }
        
        /// <summary>
        /// Is this item cancelled?
        /// </summary>
        public bool IsCancelled { get; set; }
        
        /// <summary>
        /// Modifiers applied by reactions.
        /// </summary>
        public Dictionary<string, float> Modifiers { get; } = new();
        
        /// <summary>
        /// When this item was pushed.
        /// </summary>
        public long PushedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        /// <summary>
        /// Depth in the stack (0 = top level, 1 = first interrupt, etc).
        /// </summary>
        public int Depth { get; set; }
        
        /// <summary>
        /// Callback to execute when item resolves.
        /// </summary>
        public Action OnResolve { get; set; }

        public override string ToString()
        {
            string status = IsCancelled ? " [CANCELLED]" : "";
            return $"[Stack:{ItemId}] {ActionType} from {SourceId} -> {TargetId}{status}";
        }
    }

    /// <summary>
    /// Manages nested action resolution with interrupt support.
    /// </summary>
    public class ResolutionStack
    {
        private readonly Stack<StackItem> _stack = new();
        private int _maxDepth = 10; // Prevent infinite reaction loops
        
        /// <summary>
        /// Current depth of the stack.
        /// </summary>
        public int CurrentDepth => _stack.Count;
        
        /// <summary>
        /// Is the stack empty?
        /// </summary>
        public bool IsEmpty => _stack.Count == 0;
        
        /// <summary>
        /// Maximum allowed stack depth.
        /// </summary>
        public int MaxDepth 
        { 
            get => _maxDepth; 
            set => _maxDepth = Math.Max(1, value); 
        }
        
        /// <summary>
        /// Fired when an item is pushed.
        /// </summary>
        public event Action<StackItem> OnItemPushed;
        
        /// <summary>
        /// Fired when an item is resolved/popped.
        /// </summary>
        public event Action<StackItem> OnItemResolved;
        
        /// <summary>
        /// Fired when an item is cancelled.
        /// </summary>
        public event Action<StackItem> OnItemCancelled;

        /// <summary>
        /// Push a new action onto the stack.
        /// </summary>
        public StackItem Push(string actionType, string sourceId, string targetId = null, RuleEvent evt = null)
        {
            if (_stack.Count >= _maxDepth)
            {
                throw new InvalidOperationException($"Resolution stack overflow (max depth: {_maxDepth})");
            }

            var item = new StackItem
            {
                ActionType = actionType,
                SourceId = sourceId,
                TargetId = targetId,
                Event = evt,
                Depth = _stack.Count
            };

            _stack.Push(item);
            OnItemPushed?.Invoke(item);
            return item;
        }

        /// <summary>
        /// Get the current top of stack without removing.
        /// </summary>
        public StackItem Peek()
        {
            return _stack.Count > 0 ? _stack.Peek() : null;
        }

        /// <summary>
        /// Pop and resolve the top item.
        /// </summary>
        public StackItem Pop()
        {
            if (_stack.Count == 0)
                return null;

            var item = _stack.Pop();
            item.OnResolve?.Invoke();
            OnItemResolved?.Invoke(item);
            return item;
        }

        /// <summary>
        /// Cancel the current top item.
        /// </summary>
        public bool CancelCurrent()
        {
            if (_stack.Count == 0)
                return false;

            var item = _stack.Peek();
            if (!(item.Event?.IsCancellable ?? true))
                return false;

            item.IsCancelled = true;
            if (item.Event != null)
                item.Event.IsCancelled = true;

            OnItemCancelled?.Invoke(item);
            return true;
        }

        /// <summary>
        /// Cancel a specific item by ID.
        /// </summary>
        public bool CancelItem(string itemId)
        {
            var item = _stack.FirstOrDefault(i => i.ItemId == itemId);
            if (item == null)
                return false;

            if (!(item.Event?.IsCancellable ?? true))
                return false;

            item.IsCancelled = true;
            if (item.Event != null)
                item.Event.IsCancelled = true;

            OnItemCancelled?.Invoke(item);
            return true;
        }

        /// <summary>
        /// Add a modifier to the current item.
        /// </summary>
        public void ModifyCurrent(string key, float value)
        {
            if (_stack.Count == 0)
                return;

            var item = _stack.Peek();
            item.Modifiers[key] = value;
            
            if (item.Event != null)
                item.Event.ValueModifier += value;
        }

        /// <summary>
        /// Get the current execution context (for logging/debugging).
        /// </summary>
        public List<StackItem> GetStackTrace()
        {
            return _stack.ToList();
        }

        /// <summary>
        /// Clear the entire stack.
        /// </summary>
        public void Clear()
        {
            while (_stack.Count > 0)
            {
                _stack.Pop();
            }
        }

        /// <summary>
        /// Resolve all remaining items.
        /// </summary>
        public void ResolveAll()
        {
            while (_stack.Count > 0)
            {
                Pop();
            }
        }

        /// <summary>
        /// Check if we can push more items (not at max depth).
        /// </summary>
        public bool CanPush()
        {
            return _stack.Count < _maxDepth;
        }

        /// <summary>
        /// Find an item in the stack by predicate.
        /// </summary>
        public StackItem Find(Func<StackItem, bool> predicate)
        {
            return _stack.FirstOrDefault(predicate);
        }
    }
}
