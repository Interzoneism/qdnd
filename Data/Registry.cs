using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Data;

public abstract class Registry<TItem> where TItem : class
{
    private readonly Dictionary<string, TItem> _items;
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    protected Registry(StringComparer comparer = null)
    {
        _items = new Dictionary<string, TItem>(comparer ?? StringComparer.OrdinalIgnoreCase);
    }

    protected abstract string GetId(TItem item);

    public int Count => _items.Count;
    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;

    protected bool BaseRegister(TItem item, bool overwrite, string _)
    {
        var id = GetId(item);
        if (_items.TryGetValue(id, out var existingItem))
        {
            if (overwrite)
            {
                AfterUnregister(existingItem);
                _items[id] = item;
                AfterRegister(item);
                return true;
            }
            // Not overwriting - skip silently (BG3 data takes precedence pattern)
            return false;
        }

        _items[id] = item;
        AfterRegister(item);
        return true;
    }

    public TItem Get(string id)
    {
        if (_items.TryGetValue(id, out var item))
            return item;
        return null;
    }

    public bool TryGet(string id, out TItem item)
    {
        return _items.TryGetValue(id, out item!);
    }

    public bool Has(string id) => _items.ContainsKey(id);

    public IReadOnlyList<TItem> GetAll() => _items.Values.ToList();

    public IReadOnlyDictionary<string, TItem> GetAllById() => _items;

    public void Clear()
    {
        _items.Clear();
        OnClear();
    }

    protected void AddError(string message) => _errors.Add(message);
    protected void AddWarning(string message) => _warnings.Add(message);

    protected virtual void AfterRegister(TItem item) { }
    protected virtual void AfterUnregister(TItem item) { }
    protected virtual void OnClear() { }

    // For subclasses that need direct dictionary access for secondary indexes
    protected IReadOnlyDictionary<string, TItem> Items => _items;
}
