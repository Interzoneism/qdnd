using System;
using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Generic object pool for Node3D-derived targeting visuals.
/// All nodes are parented to a shared root and toggled via Visible rather than
/// spawned/despawned each frame.
/// </summary>
public sealed class TargetingNodePool<T> : IDisposable where T : Node3D
{
    private readonly Func<T> _factory;
    private readonly Node3D _parent;
    private readonly List<T> _active = new();
    private readonly Queue<T> _pooled = new();
    private bool _disposed;

    public int ActiveCount => _active.Count;
    public int PooledCount => _pooled.Count;

    /// <param name="factory">Creates a new T instance. Called when the pool is empty.</param>
    /// <param name="parent">Scene node that owns all pooled children.</param>
    public TargetingNodePool(Func<T> factory, Node3D parent)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    /// <summary>
    /// Gets a node from the pool (or creates one), makes it visible, and returns it.
    /// </summary>
    public T Acquire()
    {
        ThrowIfDisposed();

        T node;
        if (_pooled.Count > 0)
        {
            node = _pooled.Dequeue();
        }
        else
        {
            node = _factory();
            _parent.AddChild(node);
        }

        node.Visible = true;
        _active.Add(node);
        return node;
    }

    /// <summary>
    /// Hides the node, resets its transform, and returns it to the pool.
    /// </summary>
    public void Release(T node)
    {
        ThrowIfDisposed();

        if (node == null)
            return;

        if (!_active.Remove(node))
            return; // not tracked — nothing to do

        ReturnToPool(node);
    }

    /// <summary>
    /// Releases every active node back into the pool.
    /// </summary>
    public void ReleaseAll()
    {
        ThrowIfDisposed();

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ReturnToPool(_active[i]);
        }

        _active.Clear();
    }

    /// <summary>
    /// Pre-creates <paramref name="count"/> nodes so they are ready for instant acquisition.
    /// </summary>
    public void Prewarm(int count)
    {
        ThrowIfDisposed();

        for (int i = 0; i < count; i++)
        {
            T node = _factory();
            _parent.AddChild(node);
            node.Visible = false;
            _pooled.Enqueue(node);
        }
    }

    /// <summary>
    /// QueueFrees all pooled and active nodes and marks the pool as disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (T node in _active)
        {
            if (GodotObject.IsInstanceValid(node))
                node.QueueFree();
        }

        while (_pooled.Count > 0)
        {
            T node = _pooled.Dequeue();
            if (GodotObject.IsInstanceValid(node))
                node.QueueFree();
        }

        _active.Clear();
    }

    private void ReturnToPool(T node)
    {
        node.Visible = false;
        node.Transform = Transform3D.Identity;
        _pooled.Enqueue(node);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TargetingNodePool<T>));
    }
}
