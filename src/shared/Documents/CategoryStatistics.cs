// Fork addition by 7zgfpgfg65-bit, 2026-11-12: stable category-ID aggregation.
using System;
using System.Collections.Generic;
using System.Linq;

namespace RvtMcp.Plugin.Documents
{
    public sealed class CategoryObservation
    {
        public long? Id { get; }
        public string Name { get; }
        public CategoryObservation(long? id, string name) { Id = id; Name = name; }
    }

    public sealed class CategoryIdCount
    {
        public long categoryId { get; set; }
        public string category { get; set; }
        public long count { get; set; }
    }

    public sealed class CategoryStatistics
    {
        public long Processed { get; private set; }
        public long Uncategorized { get; private set; }
        public bool Truncated { get; private set; }
        public Dictionary<string, int> LegacyCounts { get; } = new Dictionary<string, int>();
        public CategoryIdCount[] Categories { get; private set; }

        public static CategoryStatistics Collect(IEnumerable<CategoryObservation> observations, int cap)
        {
            if (cap <= 0) throw new ArgumentOutOfRangeException(nameof(cap));
            var result = new CategoryStatistics();
            var byId = new Dictionary<long, CategoryIdCount>();
            foreach (var item in observations)
            {
                if (result.Processed == cap) { result.Truncated = true; break; }
                result.Processed++;
                var legacyName = item.Name ?? "Uncategorized";
                result.LegacyCounts.TryGetValue(legacyName, out var count);
                result.LegacyCounts[legacyName] = count + 1;
                if (!item.Id.HasValue) { result.Uncategorized++; continue; }
                if (string.IsNullOrWhiteSpace(item.Name)) throw new InvalidOperationException("A categorized element has no category name.");
                if (!byId.TryGetValue(item.Id.Value, out var row))
                    byId.Add(item.Id.Value, row = new CategoryIdCount { categoryId = item.Id.Value, category = item.Name });
                else if (!string.Equals(row.category, item.Name, StringComparison.Ordinal))
                    throw new InvalidOperationException("Category name changed for the same category ID during collection.");
                row.count++;
            }
            result.Categories = byId.Values.OrderBy(x => x.categoryId).ToArray();
            return result;
        }
    }
}
