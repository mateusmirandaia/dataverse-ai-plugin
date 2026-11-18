using Microsoft.Xrm.Sdk;
using MM.Dataverse.CaseEnrichment.Ai;
using MM.Dataverse.CaseEnrichment.Enrichment;
using MM.Dataverse.CaseEnrichment.Tests.Fakes;
using Moq;
using Xunit;

namespace MM.Dataverse.CaseEnrichment.Tests
{
    public class CaseEnrichmentServiceTests
    {
        private readonly Mock<IOrganizationService> _organization = new Mock<IOrganizationService>();
        private readonly TraceLog _trace = new TraceLog();

        [Fact]
        public void Updates_only_the_ai_columns_on_the_created_case()
        {
            var incident = TestData.NewCase();
            Entity updated = null;
            _organization.Setup(o => o.Update(It.IsAny<Entity>())).Callback<Entity>(e => updated = e);
            var ai = FakeAiClient.Returning("Billing", "Negative", "High", "Charged twice. Wants a refund.");

            var changed = Service(ai).Enrich(incident);

            Assert.True(changed);
            _organization.Verify(o => o.Update(It.IsAny<Entity>()), Times.Once);
            Assert.Equal("incident", updated.LogicalName);
            Assert.Equal(incident.Id, updated.Id);
            Assert.Equal(4, updated.Attributes.Count);
            Assert.Equal("Billing", updated["mm_aicategory"]);
            Assert.Equal("Negative", updated["mm_aisentiment"]);
            Assert.Equal(1, updated.GetAttributeValue<OptionSetValue>("mm_aisuggestedpriority").Value);
            Assert.Equal("Charged twice. Wants a refund.", updated["mm_aisummary"]);
        }

        [Fact]
        public void Omits_sentiment_and_priority_when_the_model_returns_unknown_values()
        {
            Entity updated = null;
            _organization.Setup(o => o.Update(It.IsAny<Entity>())).Callback<Entity>(e => updated = e);
            var ai = FakeAiClient.Returning("Billing", "mixed", "whenever", "Refund request.");

            Service(ai).Enrich(TestData.NewCase());

            Assert.False(updated.Contains("mm_aisentiment"));
            Assert.False(updated.Contains("mm_aisuggestedpriority"));
            Assert.True(updated.Contains("mm_aicategory"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Skips_cases_without_a_description(string description)
        {
            var ai = FakeAiClient.Returning("Billing", "Neutral", "Low", "x");

            var changed = Service(ai).Enrich(TestData.NewCase(description: description));

            Assert.False(changed);
            Assert.Equal(0, ai.Calls);
            _organization.Verify(o => o.Update(It.IsAny<Entity>()), Times.Never);
        }

        [Fact]
        public void Sends_title_and_description_inside_case_delimiters()
        {
            var ai = FakeAiClient.Returning("Account Access", "Neutral", "Normal", "Locked out.");

            Service(ai).Enrich(TestData.NewCase("Cannot log in", "My account is locked after a password reset."));

            Assert.Contains("<case>", ai.LastUserPrompt);
            Assert.Contains("Title: Cannot log in", ai.LastUserPrompt);
            Assert.Contains("My account is locked after a password reset.", ai.LastUserPrompt);
            Assert.Contains("\"Account Access\"", ai.LastSystemPrompt);
        }

        [Fact]
        public void Truncates_very_long_descriptions_before_calling_the_model()
        {
            var ai = FakeAiClient.Returning("Other", "Neutral", "Low", "Long thread.");
            var description = new string('x', EnrichmentPrompt.MaxDescriptionLength * 3);

            Service(ai).Enrich(TestData.NewCase(description: description));

            Assert.True(ai.LastUserPrompt.Length < EnrichmentPrompt.MaxDescriptionLength + 200);
            Assert.Contains("[truncated]", ai.LastUserPrompt);
        }

        [Fact]
        public void Wraps_ai_failures_in_a_friendly_plugin_exception()
        {
            var ai = new FakeAiClient(() => throw new AiClientException("Azure OpenAI returned HTTP 401 (Unauthorized)."));

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => Service(ai).Enrich(TestData.NewCase()));

            Assert.Contains("could not reach Azure OpenAI", ex.Message);
            Assert.Contains("401", ex.Message);
            _organization.Verify(o => o.Update(It.IsAny<Entity>()), Times.Never);
        }

        [Fact]
        public void Rejects_malformed_model_replies_without_updating()
        {
            var ai = new FakeAiClient("Sure! Here is the classification you asked for.");

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => Service(ai).Enrich(TestData.NewCase()));

            Assert.Contains("not in the expected format", ex.Message);
            _organization.Verify(o => o.Update(It.IsAny<Entity>()), Times.Never);
        }

        private CaseEnrichmentService Service(IAiClient ai)
        {
            return new CaseEnrichmentService(ai, _organization.Object, _trace);
        }
    }
}
