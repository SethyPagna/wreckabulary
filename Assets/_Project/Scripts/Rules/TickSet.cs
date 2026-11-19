using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class TickSet
    {
        readonly string[] options;
        readonly HashSet<string> ticked;

        public TickSet(IEnumerable<string> options, IEnumerable<string> ticked = null)
        {
            this.options = (options ?? throw new ArgumentNullException(nameof(options))).ToArray();
            if (this.options.Length == 0 || this.options.Any(string.IsNullOrEmpty) ||
                this.options.Distinct(StringComparer.Ordinal).Count() != this.options.Length)
                throw new ArgumentException("A tick set needs one or more distinct options.", nameof(options));
            this.ticked = new HashSet<string>((ticked ?? Array.Empty<string>()).Where(Has), StringComparer.Ordinal);
            if (this.ticked.Count == 0) this.ticked.UnionWith(this.options);
        }

        public IReadOnlyList<string> Options => options;
        public IReadOnlyList<string> Ticked => options.Where(ticked.Contains).ToArray();
        public int Count => ticked.Count;
        public bool All => ticked.Count == options.Length;
        public bool Has(string id) => id != null && Array.IndexOf(options, id) >= 0;
        public bool IsTicked(string id) => id != null && ticked.Contains(id);
        public bool CanUntick(string id) => IsTicked(id) && ticked.Count > 1;

        public bool Toggle(string id)
        {
            if (!Has(id)) throw new ArgumentException("Choose one of the listed options.", nameof(id));
            if (!ticked.Contains(id)) return ticked.Add(id);
            return ticked.Count > 1 && ticked.Remove(id);
        }

        public void TickAll() => ticked.UnionWith(options);

        public string Save() => string.Join("+", Ticked);

        public static TickSet Load(IEnumerable<string> options, string saved) =>
            new TickSet(options, string.IsNullOrEmpty(saved) ? null : saved.Split('+'));

        public string Summary(Func<string, string> name, string all)
        {
            if (All) return all;
            return string.Join(", ", Ticked.Select(name ?? (id => id)));
        }
    }
}
