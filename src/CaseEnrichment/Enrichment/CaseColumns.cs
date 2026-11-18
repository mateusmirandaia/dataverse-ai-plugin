namespace MM.Dataverse.CaseEnrichment.Enrichment
{
    internal static class CaseColumns
    {
        public const string EntityName = "incident";

        public const string Title = "title";
        public const string Description = "description";

        public const string AiCategory = "mm_aicategory";
        public const string AiSentiment = "mm_aisentiment";
        public const string AiSuggestedPriority = "mm_aisuggestedpriority";
        public const string AiSummary = "mm_aisummary";

        // Matches the max length configured on the mm_aisummary column.
        public const int AiSummaryMaxLength = 2000;
    }
}
