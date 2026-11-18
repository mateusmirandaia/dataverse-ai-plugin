using System;
using Microsoft.Xrm.Sdk;
using MM.Dataverse.CaseEnrichment.Ai;

namespace MM.Dataverse.CaseEnrichment.Enrichment
{
    public sealed class CaseEnrichmentService
    {
        private readonly IAiClient _ai;
        private readonly IOrganizationService _organization;
        private readonly ITracingService _tracing;

        public CaseEnrichmentService(IAiClient ai, IOrganizationService organization, ITracingService tracing)
        {
            _ai = ai ?? throw new ArgumentNullException(nameof(ai));
            _organization = organization ?? throw new ArgumentNullException(nameof(organization));
            _tracing = tracing ?? throw new ArgumentNullException(nameof(tracing));
        }

        /// <returns>True when the case was updated with enrichment data.</returns>
        public bool Enrich(Entity incident)
        {
            if (incident == null)
            {
                throw new ArgumentNullException(nameof(incident));
            }

            var description = incident.GetAttributeValue<string>(CaseColumns.Description);
            if (string.IsNullOrWhiteSpace(description))
            {
                // Common for phone cases logged with a title only. Not an error, and
                // a title alone is too thin to classify reliably.
                _tracing.Trace("Case {0} has no description; skipping enrichment.", incident.Id);
                return false;
            }

            var title = incident.GetAttributeValue<string>(CaseColumns.Title);
            var result = RequestEnrichment(title, description);

            _tracing.Trace(
                "Case {0} enriched: category={1}, sentiment={2}, priority={3}.",
                incident.Id,
                result.Category,
                result.Sentiment ?? "(unknown)",
                result.SuggestedPriority?.ToString() ?? "(unknown)");

            _organization.Update(BuildUpdate(incident.Id, result));
            return true;
        }

        private CaseEnrichmentResult RequestEnrichment(string title, string description)
        {
            string reply;
            try
            {
                reply = _ai.CompleteJson(EnrichmentPrompt.SystemPrompt, EnrichmentPrompt.BuildUserPrompt(title, description));
            }
            catch (AiClientException ex)
            {
                _tracing.Trace("Azure OpenAI call failed: {0}", ex.Message);
                throw new InvalidPluginExecutionException(
                    "The case was saved, but AI enrichment could not reach Azure OpenAI. " + ex.Message, ex);
            }

            try
            {
                return EnrichmentResponseParser.Parse(reply);
            }
            catch (FormatException ex)
            {
                _tracing.Trace("Unexpected model reply: {0}", ex.Message);
                throw new InvalidPluginExecutionException(
                    "The case was saved, but the AI enrichment reply was not in the expected format.", ex);
            }
        }

        // Only the AI columns go into the update. Sending the whole Target back would
        // overwrite anything an agent changed since the create, and would fire every
        // update plugin and audit entry registered on those columns.
        private static Entity BuildUpdate(Guid incidentId, CaseEnrichmentResult result)
        {
            var update = new Entity(CaseColumns.EntityName, incidentId);
            update[CaseColumns.AiCategory] = result.Category;
            update[CaseColumns.AiSummary] = result.Summary;

            if (result.Sentiment != null)
            {
                update[CaseColumns.AiSentiment] = result.Sentiment;
            }

            if (result.SuggestedPriority.HasValue)
            {
                update[CaseColumns.AiSuggestedPriority] = new OptionSetValue((int)result.SuggestedPriority.Value);
            }

            return update;
        }
    }
}
