// Fork addition by 7zgfpgfg65-bit, 2026-11-12.
using System;
using System.Linq;
using RvtMcp.Plugin.Documents;
using Xunit;

namespace RvtMcp.Tests.Shared.Documents;

public class CategoryStatisticsTests
{
    [Fact] public void Same_name_categories_keep_distinct_ids_and_legacy_shape()
    {
        var result = CategoryStatistics.Collect(new[] {
            new CategoryObservation(-2, "Same"), new CategoryObservation(-1, "Same"), new CategoryObservation(-2, "Same") }, 10);
        Assert.Equal(new long[] { -2, -1 }, result.Categories.Select(x => x.categoryId));
        Assert.Equal(new long[] { 2, 1 }, result.Categories.Select(x => x.count));
        Assert.Equal(3, result.LegacyCounts["Same"]);
        Assert.False(result.Truncated);
    }
    [Fact] public void Null_category_never_merges_with_real_uncategorized_name_in_id_counts()
    {
        var result = CategoryStatistics.Collect(new[] { new CategoryObservation(null, null), new CategoryObservation(-9, "Uncategorized") }, 10);
        Assert.Equal(1, result.Uncategorized);
        Assert.Equal(1, Assert.Single(result.Categories).count);
        Assert.Equal(2, result.LegacyCounts["Uncategorized"]);
    }
    [Theory] [InlineData(0, 10, false)] [InlineData(10, 10, false)] [InlineData(11, 10, true)] [InlineData(1201, 2000, false)]
    public void Cap_and_empty_completion_are_explicit(int count, int cap, bool truncated)
    {
        var result = CategoryStatistics.Collect(Enumerable.Range(0, count).Select(_ => new CategoryObservation(-1, "Category")), cap);
        Assert.Equal(Math.Min(count, cap), result.Processed);
        Assert.Equal(truncated, result.Truncated);
        Assert.Equal(result.Processed, result.Categories.Sum(x => x.count));
    }
    [Fact] public void Category_ids_are_not_narrowed_to_int32()
    {
        var result = CategoryStatistics.Collect(new[] { new CategoryObservation(5000000000L, "Custom") }, 10);
        Assert.Equal(5000000000L, Assert.Single(result.Categories).categoryId);
    }
    [Fact] public void Inconsistent_same_id_names_fail_instead_of_publishing()
    {
        Assert.Throws<InvalidOperationException>(() => CategoryStatistics.Collect(new[] { new CategoryObservation(-1, "A"), new CategoryObservation(-1, "B") }, 10));
    }
}
