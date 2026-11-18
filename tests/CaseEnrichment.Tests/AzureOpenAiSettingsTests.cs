using Microsoft.Xrm.Sdk;
using MM.Dataverse.CaseEnrichment.Configuration;
using Xunit;

namespace MM.Dataverse.CaseEnrichment.Tests
{
    public class AzureOpenAiSettingsTests
    {
        [Fact]
        public void Parse_reads_all_fields_and_builds_the_chat_completions_uri()
        {
            var settings = AzureOpenAiSettings.Parse(TestData.SecureConfiguration);

            Assert.Equal("gpt-4o-mini", settings.Deployment);
            Assert.Equal("test-key-not-real", settings.ApiKey);
            Assert.Equal(
                "https://contoso-test.openai.azure.com/openai/deployments/gpt-4o-mini/chat/completions?api-version=2024-10-21",
                settings.ChatCompletionsUri.ToString());
        }

        [Fact]
        public void Parse_defaults_the_api_version_when_omitted()
        {
            var settings = AzureOpenAiSettings.Parse(
                "{\"endpoint\":\"https://contoso-test.openai.azure.com\",\"deployment\":\"gpt-4o-mini\",\"apiKey\":\"test-key-not-real\"}");

            Assert.Equal(AzureOpenAiSettings.DefaultApiVersion, settings.ApiVersion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_rejects_missing_configuration(string configuration)
        {
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => AzureOpenAiSettings.Parse(configuration));
            Assert.Contains("not configured", ex.Message);
        }

        [Fact]
        public void Parse_rejects_invalid_json_without_echoing_it()
        {
            const string broken = "apiKey=test-key-not-real";

            var ex = Assert.Throws<InvalidPluginExecutionException>(() => AzureOpenAiSettings.Parse(broken));

            Assert.Contains("not valid JSON", ex.Message);
            Assert.DoesNotContain("test-key-not-real", ex.Message);
        }

        [Theory]
        [InlineData("{\"deployment\":\"d\",\"apiKey\":\"k\"}", "endpoint")]
        [InlineData("{\"endpoint\":\"http://contoso-test.openai.azure.com\",\"deployment\":\"d\",\"apiKey\":\"k\"}", "endpoint")]
        [InlineData("{\"endpoint\":\"https://contoso-test.openai.azure.com\",\"apiKey\":\"k\"}", "deployment")]
        [InlineData("{\"endpoint\":\"https://contoso-test.openai.azure.com\",\"deployment\":\"d\"}", "apiKey")]
        public void Parse_names_the_missing_setting(string configuration, string expectedField)
        {
            var ex = Assert.Throws<InvalidPluginExecutionException>(() => AzureOpenAiSettings.Parse(configuration));
            Assert.Contains(expectedField, ex.Message);
        }
    }
}
