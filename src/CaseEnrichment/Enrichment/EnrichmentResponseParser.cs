using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace MM.Dataverse.CaseEnrichment.Enrichment
{
    internal static class EnrichmentResponseParser
    {
        public static CaseEnrichmentResult Parse(string modelReply)
        {
            if (string.IsNullOrWhiteSpace(modelReply))
            {
                throw new FormatException("The model reply was empty.");
            }

            var payload = Deserialize(StripCodeFence(modelReply));
            var summary = payload.Summary?.Trim();

            if (string.IsNullOrEmpty(summary))
            {
                throw new FormatException("The model reply did not include a summary.");
            }

            if (summary.Length > CaseColumns.AiSummaryMaxLength)
            {
                summary = summary.Substring(0, CaseColumns.AiSummaryMaxLength);
            }

            return new CaseEnrichmentResult
            {
                Category = CaseCategories.Normalize(payload.Category),
                Sentiment = NormalizeSentiment(payload.Sentiment),
                SuggestedPriority = CasePriorityParser.Parse(payload.Priority),
                Summary = summary
            };
        }

        private static EnrichmentPayload Deserialize(string json)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(EnrichmentPayload));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return (EnrichmentPayload)serializer.ReadObject(stream);
                }
            }
            catch (Exception ex) when (ex is SerializationException || ex is XmlException)
            {
                throw new FormatException("The model reply was not a valid JSON object.", ex);
            }
        }

        // JSON mode should never wrap the object in a markdown fence, but older
        // deployments without response_format support sometimes do.
        private static string StripCodeFence(string reply)
        {
            var text = reply.Trim();
            if (!text.StartsWith("```", StringComparison.Ordinal))
            {
                return text;
            }

            var firstBrace = text.IndexOf('{');
            var lastBrace = text.LastIndexOf('}');
            return firstBrace >= 0 && lastBrace > firstBrace
                ? text.Substring(firstBrace, lastBrace - firstBrace + 1)
                : text;
        }

        private static string NormalizeSentiment(string sentiment)
        {
            switch (sentiment?.Trim().ToLowerInvariant())
            {
                case "positive":
                    return "Positive";
                case "neutral":
                    return "Neutral";
                case "negative":
                    return "Negative";
                default:
                    return null;
            }
        }

        [DataContract]
        private sealed class EnrichmentPayload
        {
            [DataMember(Name = "category")]
            public string Category { get; set; }

            [DataMember(Name = "sentiment")]
            public string Sentiment { get; set; }

            [DataMember(Name = "priority")]
            public string Priority { get; set; }

            [DataMember(Name = "summary")]
            public string Summary { get; set; }
        }
    }
}
