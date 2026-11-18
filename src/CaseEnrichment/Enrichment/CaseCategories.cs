using System;
using System.Collections.Generic;
using System.Linq;

namespace MM.Dataverse.CaseEnrichment.Enrichment
{
    public static class CaseCategories
    {
        public const string Other = "Other";

        // Keep this list short and mutually exclusive; the model classifies far more
        // consistently against a handful of clear buckets than against a long taxonomy.
        public static readonly IReadOnlyList<string> All = new[]
        {
            "Billing",
            "Technical Issue",
            "Account Access",
            "Product Question",
            "Complaint",
            "Feature Request",
            Other
        };

        public static string Normalize(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return Other;
            }

            var match = All.FirstOrDefault(c => string.Equals(c, category.Trim(), StringComparison.OrdinalIgnoreCase));
            return match ?? Other;
        }
    }
}
