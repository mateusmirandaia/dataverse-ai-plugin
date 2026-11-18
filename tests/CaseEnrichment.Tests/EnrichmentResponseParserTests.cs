using System;
using MM.Dataverse.CaseEnrichment.Enrichment;
using Xunit;

namespace MM.Dataverse.CaseEnrichment.Tests
{
    public class EnrichmentResponseParserTests
    {
        [Fact]
        public void Parses_a_well_formed_reply()
        {
            var result = EnrichmentResponseParser.Parse(
                "{\"category\":\"Billing\",\"sentiment\":\"Negative\",\"priority\":\"High\",\"summary\":\"Customer was charged twice. Wants a refund.\"}");

            Assert.Equal("Billing", result.Category);
            Assert.Equal("Negative", result.Sentiment);
            Assert.Equal(CasePriority.High, result.SuggestedPriority);
            Assert.Equal("Customer was charged twice. Wants a refund.", result.Summary);
        }

        [Fact]
        public void Normalizes_casing_and_ignores_extra_properties()
        {
            var result = EnrichmentResponseParser.Parse(
                "{\"category\":\"technical issue\",\"sentiment\":\"NEUTRAL\",\"priority\":\"medium\",\"summary\":\" Login fails. \",\"confidence\":0.9}");

            Assert.Equal("Technical Issue", result.Category);
            Assert.Equal("Neutral", result.Sentiment);
            Assert.Equal(CasePriority.Normal, result.SuggestedPriority);
            Assert.Equal("Login fails.", result.Summary);
        }

        [Fact]
        public void Maps_unknown_category_to_other()
        {
            var result = EnrichmentResponseParser.Parse("{\"category\":\"Shipping\",\"summary\":\"Parcel is late.\"}");

            Assert.Equal(CaseCategories.Other, result.Category);
        }

        [Fact]
        public void Leaves_unrecognised_sentiment_and_priority_empty()
        {
            var result = EnrichmentResponseParser.Parse(
                "{\"category\":\"Complaint\",\"sentiment\":\"furious\",\"priority\":\"P0\",\"summary\":\"Unhappy customer.\"}");

            Assert.Null(result.Sentiment);
            Assert.Null(result.SuggestedPriority);
        }

        [Fact]
        public void Strips_a_markdown_code_fence()
        {
            var result = EnrichmentResponseParser.Parse(
                "```json\n{\"category\":\"Billing\",\"summary\":\"Refund request.\"}\n```");

            Assert.Equal("Billing", result.Category);
        }

        [Fact]
        public void Truncates_summary_to_the_column_length()
        {
            var longSummary = new string('a', CaseColumns.AiSummaryMaxLength + 50);

            var result = EnrichmentResponseParser.Parse("{\"category\":\"Other\",\"summary\":\"" + longSummary + "\"}");

            Assert.Equal(CaseColumns.AiSummaryMaxLength, result.Summary.Length);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not json at all")]
        [InlineData("{\"category\":\"Billing\"")]
        [InlineData("{\"category\":\"Billing\",\"summary\":\"  \"}")]
        public void Rejects_unusable_replies(string reply)
        {
            Assert.Throws<FormatException>(() => EnrichmentResponseParser.Parse(reply));
        }
    }
}
