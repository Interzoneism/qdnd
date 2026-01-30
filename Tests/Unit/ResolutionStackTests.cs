using System;
using Xunit;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    public class ResolutionStackTests
    {
        [Fact]
        public void Push_AddsItemToStack()
        {
            var stack = new ResolutionStack();
            
            var item = stack.Push("attack", "attacker", "target");
            
            Assert.NotNull(item);
            Assert.Equal("attack", item.ActionType);
            Assert.Equal(1, stack.CurrentDepth);
            Assert.False(stack.IsEmpty);
        }

        [Fact]
        public void Pop_RemovesAndReturnsItem()
        {
            var stack = new ResolutionStack();
            stack.Push("attack", "attacker");
            
            var item = stack.Pop();
            
            Assert.NotNull(item);
            Assert.Equal("attack", item.ActionType);
            Assert.True(stack.IsEmpty);
        }

        [Fact]
        public void Pop_Empty_ReturnsNull()
        {
            var stack = new ResolutionStack();
            
            var item = stack.Pop();
            
            Assert.Null(item);
        }

        [Fact]
        public void Peek_ReturnsTopWithoutRemoving()
        {
            var stack = new ResolutionStack();
            stack.Push("attack", "attacker");
            
            var item = stack.Peek();
            
            Assert.NotNull(item);
            Assert.Equal(1, stack.CurrentDepth); // Still on stack
        }

        [Fact]
        public void CancelCurrent_SetsCancelledFlag()
        {
            var stack = new ResolutionStack();
            var evt = new RuleEvent { Type = RuleEventType.AttackDeclared, IsCancellable = true };
            stack.Push("attack", "attacker", evt: evt);
            
            bool cancelled = stack.CancelCurrent();
            
            Assert.True(cancelled);
            Assert.True(stack.Peek().IsCancelled);
            Assert.True(evt.IsCancelled);
        }

        [Fact]
        public void CancelCurrent_NonCancellable_ReturnsFalse()
        {
            var stack = new ResolutionStack();
            var evt = new RuleEvent { Type = RuleEventType.AttackDeclared, IsCancellable = false };
            stack.Push("attack", "attacker", evt: evt);
            
            bool cancelled = stack.CancelCurrent();
            
            Assert.False(cancelled);
            Assert.False(stack.Peek().IsCancelled);
        }

        [Fact]
        public void ModifyCurrent_AddsModifier()
        {
            var stack = new ResolutionStack();
            var evt = new RuleEvent { Value = 10 };
            stack.Push("damage", "attacker", evt: evt);
            
            stack.ModifyCurrent("shield", -5);
            
            Assert.Equal(-5, stack.Peek().Modifiers["shield"]);
            Assert.Equal(5, evt.FinalValue); // 10 + (-5)
        }

        [Fact]
        public void Depth_TracksCorrectly()
        {
            var stack = new ResolutionStack();
            
            var item1 = stack.Push("attack", "a");
            var item2 = stack.Push("reaction", "b");
            var item3 = stack.Push("counter", "c");
            
            Assert.Equal(0, item1.Depth);
            Assert.Equal(1, item2.Depth);
            Assert.Equal(2, item3.Depth);
            Assert.Equal(3, stack.CurrentDepth);
        }

        [Fact]
        public void MaxDepth_PreventsOverflow()
        {
            var stack = new ResolutionStack { MaxDepth = 3 };
            
            stack.Push("a", "a");
            stack.Push("b", "b");
            stack.Push("c", "c");
            
            Assert.Throws<InvalidOperationException>(() => stack.Push("d", "d"));
        }

        [Fact]
        public void OnResolve_CalledWhenPopped()
        {
            var stack = new ResolutionStack();
            bool resolved = false;
            
            var item = stack.Push("attack", "attacker");
            item.OnResolve = () => resolved = true;
            
            stack.Pop();
            
            Assert.True(resolved);
        }

        [Fact]
        public void Events_FireCorrectly()
        {
            var stack = new ResolutionStack();
            bool pushed = false, popped = false, cancelled = false;
            
            stack.OnItemPushed += _ => pushed = true;
            stack.OnItemResolved += _ => popped = true;
            stack.OnItemCancelled += _ => cancelled = true;
            
            var evt = new RuleEvent { IsCancellable = true };
            stack.Push("attack", "a", evt: evt);
            stack.CancelCurrent();
            stack.Pop();
            
            Assert.True(pushed);
            Assert.True(popped);
            Assert.True(cancelled);
        }

        [Fact]
        public void GetStackTrace_ReturnsAllItems()
        {
            var stack = new ResolutionStack();
            stack.Push("a", "1");
            stack.Push("b", "2");
            stack.Push("c", "3");
            
            var trace = stack.GetStackTrace();
            
            Assert.Equal(3, trace.Count);
        }

        [Fact]
        public void ResolveAll_ClearsStack()
        {
            var stack = new ResolutionStack();
            stack.Push("a", "1");
            stack.Push("b", "2");
            stack.Push("c", "3");
            
            stack.ResolveAll();
            
            Assert.True(stack.IsEmpty);
        }

        [Fact]
        public void CancelCurrent_NoEvent_StillCancels()
        {
            var stack = new ResolutionStack();
            stack.Push("attack", "attacker");
            
            bool cancelled = stack.CancelCurrent();
            
            Assert.True(cancelled);
            Assert.True(stack.Peek().IsCancelled);
        }

        [Fact]
        public void Find_LocatesItemByPredicate()
        {
            var stack = new ResolutionStack();
            stack.Push("attack", "a");
            stack.Push("reaction", "b");
            stack.Push("counter", "c");
            
            var found = stack.Find(i => i.ActionType == "reaction");
            
            Assert.NotNull(found);
            Assert.Equal("b", found.SourceId);
        }

        [Fact]
        public void CanPush_RespectsMaxDepth()
        {
            var stack = new ResolutionStack { MaxDepth = 2 };
            
            Assert.True(stack.CanPush());
            stack.Push("a", "a");
            Assert.True(stack.CanPush());
            stack.Push("b", "b");
            Assert.False(stack.CanPush());
        }

        [Fact]
        public void Clear_RemovesAllItems()
        {
            var stack = new ResolutionStack();
            stack.Push("a", "1");
            stack.Push("b", "2");
            
            stack.Clear();
            
            Assert.True(stack.IsEmpty);
            Assert.Equal(0, stack.CurrentDepth);
        }
    }
}
