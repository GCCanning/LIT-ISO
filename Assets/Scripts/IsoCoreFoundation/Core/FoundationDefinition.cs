using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Common base for every data-driven definition (id + display name).</summary>
    public abstract class FoundationDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public string Id => id;
        public string Display => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    /// <summary>Generic id -&gt; definition lookup. Plain C#, not a ScriptableObject.</summary>
    public class Database<T> where T : FoundationDefinition
    {
        readonly Dictionary<string, T> _byId = new();
        readonly List<T> _ordered = new();

        public IReadOnlyList<T> All => _ordered;
        public int Count => _ordered.Count;
        public T this[int index] => _ordered[index];

        public void Add(T def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            if (_byId.ContainsKey(def.id)) { _byId[def.id] = def; return; }
            _byId.Add(def.id, def);
            _ordered.Add(def);
        }

        public bool TryGet(string id, out T def) => _byId.TryGetValue(id ?? string.Empty, out def);

        public T Get(string id)
        {
            _byId.TryGetValue(id ?? string.Empty, out var def);
            return def;
        }

        public bool Has(string id) => !string.IsNullOrEmpty(id) && _byId.ContainsKey(id);
    }
}
