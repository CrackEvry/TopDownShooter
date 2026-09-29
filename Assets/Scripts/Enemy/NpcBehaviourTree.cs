using System;

namespace Shooter.AI
{
    public enum NodeStatus { NotVisited, Success, Failure, Running }
    public abstract class BehaviourNode
    {
        public readonly string Name;
        public readonly BehaviourNode[] Children;
        public NodeStatus Status { get; private set; }
        protected BehaviourNode(string name, params BehaviourNode[] children) { Name = name; Children = children; }
        public NodeStatus Tick() => Status = Evaluate();
        public void ResetStatus() { Status = NodeStatus.NotVisited; foreach (var child in Children) child.ResetStatus(); }
        protected abstract NodeStatus Evaluate();
    }
    // Reactive composites restart at the highest priority each tick. Injury and
    // new threats can interrupt a running route without waiting for it to finish.
    public sealed class Selector : BehaviourNode
    {
        public Selector(string name, params BehaviourNode[] children) : base(name, children) { }
        protected override NodeStatus Evaluate()
        { foreach (var child in Children) { var result = child.Tick(); if (result != NodeStatus.Failure) return result; } return NodeStatus.Failure; }
    }
    public sealed class Sequence : BehaviourNode
    {
        public Sequence(string name, params BehaviourNode[] children) : base(name, children) { }
        protected override NodeStatus Evaluate()
        { foreach (var child in Children) { var result = child.Tick(); if (result != NodeStatus.Success) return result; } return NodeStatus.Success; }
    }
    public sealed class Condition : BehaviourNode
    {
        readonly Func<bool> condition;
        public Condition(string name, Func<bool> condition) : base(name) { this.condition = condition; }
        protected override NodeStatus Evaluate() => condition() ? NodeStatus.Success : NodeStatus.Failure;
    }
    public sealed class TaskNode : BehaviourNode
    {
        readonly Func<NodeStatus> action;
        public TaskNode(string name, Func<NodeStatus> action) : base(name) { this.action = action; }
        protected override NodeStatus Evaluate() => action();
    }
}
