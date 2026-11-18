namespace MM.Dataverse.CaseEnrichment.Enrichment
{
    public sealed class CaseEnrichmentResult
    {
        public string Category { get; set; }

        /// <summary>Positive, Neutral or Negative; null when the model gave something else.</summary>
        public string Sentiment { get; set; }

        public CasePriority? SuggestedPriority { get; set; }

        public string Summary { get; set; }
    }
}
