using System.Collections.Generic;
using Xunit;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    public class RuleEventBusTests
    {
        [Fact]
        public void Dispatch_CancelledEvent_StopsPropagation()
        {
            var bus = new RuleEventBus();
            var handlersCalled = new List<int>();
            
            // Subscribe three handlers with different priorities
            bus.Subscribe(RuleEventType.DamageTaken, _ => handlersCalled.Add(1), priority: 10);
            bus.Subscribe(RuleEventType.DamageTaken, evt => 
            {
                handlersCalled.Add(2);
                evt.IsCancelled = true; // Cancel in the second handler
            }, priority: 20);
            bus.Subscribe(RuleEventType.DamageTaken, _ => handlersCalled.Add(3), priority: 30);
            
            var evt = new RuleEvent { Type = RuleEventType.DamageTaken, IsCancellable = true };
            bus.Dispatch(evt);
            
            // Third handler should not be called due to cancellation
            Assert.Equal(new List<int> { 1, 2 }, handlersCalled);
            Assert.True(evt.IsCancelled);
        }

        [Fact]
        public void Dispatch_NonCancellableEvent_ContinuesAfterCancel()
        {
            var bus = new RuleEventBus();
            var handlersCalled = new List<int>();
            
            bus.Subscribe(RuleEventType.CombatStarted, _ => handlersCalled.Add(1), priority: 10);
            bus.Subscribe(RuleEventType.CombatStarted, evt => 
            {
                handlersCalled.Add(2);
                evt.IsCancelled = true; // Try to cancel non-cancellable event
            }, priority: 20);
            bus.Subscribe(RuleEventType.CombatStarted, _ => handlersCalled.Add(3), priority: 30);
            
            var evt = new RuleEvent { Type = RuleEventType.CombatStarted, IsCancellable = false };
            bus.Dispatch(evt);
            
            // All handlers should be called because event is not cancellable
            Assert.Equal(new List<int> { 1, 2, 3 }, handlersCalled);
        }

        [Fact]
        public void Dispatch_PreCancelledEvent_SkipsAllHandlers()
        {
            var bus = new RuleEventBus();
            bool handlerCalled = false;
            
            bus.Subscribe(RuleEventType.AttackDeclared, _ => handlerCalled = true);
            
            var evt = new RuleEvent 
            { 
                Type = RuleEventType.AttackDeclared, 
                IsCancellable = true,
                IsCancelled = true // Already cancelled before dispatch
            };
            bus.Dispatch(evt);
            
            Assert.False(handlerCalled);
        }
    }
}
