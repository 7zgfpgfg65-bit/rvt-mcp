// Modified by 7zgfpgfg65-bit, 2026-11-12: additive category-ID counts and scope evidence.
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RvtMcp.Plugin.Documents;

namespace RvtMcp.Plugin.Handlers
{
    public class AnalyzeModelStatisticsHandler : IRevitCommand
    {
        private const int DefaultCap = 100_000;

        public string Name => "analyze_model_statistics";
        public string Description => "Analyze model complexity with element counts by category. Iteration is capped at maxElements (default 100000) to keep the Revit UI responsive on large models.";
        public string ParametersSchema => @"{""type"":""object"",""properties"":{""maxElements"":{""type"":""integer"",""description"":""Max elements to iterate. Default 100000. Set higher only if you need full accuracy on a very large model and accept a longer UI freeze.""}}}";

        public CommandResult Execute(UIApplication app, string paramsJson)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
                return CommandResult.Fail("No document is open.");

            int maxElements = DefaultCap;
            if (!string.IsNullOrWhiteSpace(paramsJson))
            {
                var request = JObject.Parse(paramsJson);
                var requested = request.Value<int?>("maxElements");
                if (requested.HasValue && requested.Value > 0)
                    maxElements = requested.Value;
            }

            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            var stats = CategoryStatistics.Collect(collector.Select(el =>
            {
                var category = el.Category;
                return new CategoryObservation(category == null ? (long?)null : RevitCompat.GetId(category.Id), category?.Name);
            }), maxElements);

            var categories = stats.LegacyCounts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new { category = kv.Key, count = kv.Value })
                .ToArray();

            return CommandResult.Ok(new
            {
                projectName = doc.Title,
                elementsCounted = stats.Processed,
                totalCategories = categories.Length,
                truncated = stats.Truncated,
                cap = maxElements,
                note = stats.Truncated
                    ? $"Model exceeds {maxElements} elements; stats reflect the first {stats.Processed} enumerated. Raise maxElements for full accuracy."
                    : null,
                categories,
                // Legacy categories remain name-grouped for compatibility. New clients must use this block.
                categoryIdCountsSchemaVersion = 1,
                categoryIdCounts = stats.Categories,
                categorizedElementsCounted = stats.Processed - stats.Uncategorized,
                uncategorizedElementsCounted = stats.Uncategorized,
                categoryIdCountsComplete = !stats.Truncated,
                categoryIdCountsScope = new
                {
                    hostDocumentOnly = true, instancesOnly = true, categorizedElementsOnly = true,
                    includesModelAndAnnotationCategories = true, linkedDocumentContentsIncluded = false
                },
                document = new
                {
                    documentId = DocumentTargetGuard.GetOrCreateToken(doc),
                    path = doc.PathName, title = doc.Title, revitVersion = app.Application.VersionNumber
                }
            });
        }
    }
}
