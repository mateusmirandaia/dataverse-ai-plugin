using System;

namespace MM.Dataverse.CaseEnrichment.Enrichment
{
    /// <summary>
    /// Same values as the out-of-the-box incident prioritycode choice, so a suggestion
    /// can be copied straight into Priority by a flow or a business rule.
    /// </summary>
    public enum CasePriority
    {
        High = 1,
        Normal = 2,
        Low = 3
    }

    internal static class CasePriorityParser
    {
        public static CasePriority? Parse(string value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "high":
                case "urgent":
                case "critical":
                    return CasePriority.High;
                case "normal":
                case "medium":
                    return CasePriority.Normal;
                case "low":
                    return CasePriority.Low;
                default:
                    return null;
            }
        }
    }
}
