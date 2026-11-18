using System;
using Microsoft.Xrm.Sdk;

namespace MM.Dataverse.CaseEnrichment.Tests
{
    internal static class TestData
    {
        public const string SecureConfiguration =
            "{\"endpoint\":\"https://contoso-test.openai.azure.com/\",\"deployment\":\"gpt-4o-mini\",\"apiKey\":\"test-key-not-real\",\"apiVersion\":\"2024-10-21\"}";

        public static Entity NewCase(string title = "Invoice charged twice", string description = "I was billed twice for my October invoice and need a refund.")
        {
            var incident = new Entity("incident", Guid.NewGuid());
            if (title != null)
            {
                incident["title"] = title;
            }

            if (description != null)
            {
                incident["description"] = description;
            }

            return incident;
        }
    }
}
