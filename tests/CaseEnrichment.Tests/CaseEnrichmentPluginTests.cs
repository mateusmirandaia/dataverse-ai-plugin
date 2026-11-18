using System;
using Microsoft.Xrm.Sdk;
using MM.Dataverse.CaseEnrichment.Tests.Fakes;
using Moq;
using Xunit;

namespace MM.Dataverse.CaseEnrichment.Tests
{
    public class CaseEnrichmentPluginTests
    {
        private readonly FakeAiClient _ai = FakeAiClient.Returning("Billing", "Negative", "High", "Charged twice.");

        [Fact]
        public void Enriches_the_target_on_case_create()
        {
            var harness = new PluginHarness().WithTarget(TestData.NewCase());

            Plugin(TestData.SecureConfiguration).Execute(harness.ServiceProvider.Object);

            Assert.Equal(1, _ai.Calls);
            harness.Organization.Verify(
                o => o.Update(It.Is<Entity>(e => (string)e["mm_aicategory"] == "Billing")),
                Times.Once);
        }

        [Fact]
        public void Uses_the_initiating_user_for_the_update()
        {
            var harness = new PluginHarness().WithTarget(TestData.NewCase());
            var factory = new Mock<IOrganizationServiceFactory>();
            factory.Setup(f => f.CreateOrganizationService(harness.UserId)).Returns(harness.Organization.Object);
            harness.ServiceProvider.Setup(s => s.GetService(typeof(IOrganizationServiceFactory))).Returns(factory.Object);

            Plugin(TestData.SecureConfiguration).Execute(harness.ServiceProvider.Object);

            factory.Verify(f => f.CreateOrganizationService(harness.UserId), Times.Once);
        }

        [Theory]
        [InlineData("Update", "incident")]
        [InlineData("Create", "account")]
        public void Ignores_other_messages_and_entities(string message, string entity)
        {
            var harness = new PluginHarness(message, entity).WithTarget(TestData.NewCase());

            Plugin(TestData.SecureConfiguration).Execute(harness.ServiceProvider.Object);

            Assert.Equal(0, _ai.Calls);
            Assert.Contains("only handles Create of incident", harness.Trace.All);
        }

        [Fact]
        public void Stops_when_depth_is_too_high()
        {
            var harness = new PluginHarness(depth: 3).WithTarget(TestData.NewCase());

            Plugin(TestData.SecureConfiguration).Execute(harness.ServiceProvider.Object);

            Assert.Equal(0, _ai.Calls);
            harness.Organization.Verify(o => o.Update(It.IsAny<Entity>()), Times.Never);
        }

        [Fact]
        public void Runs_for_cases_created_by_another_plugin()
        {
            var harness = new PluginHarness(depth: 2).WithTarget(TestData.NewCase());

            Plugin(TestData.SecureConfiguration).Execute(harness.ServiceProvider.Object);

            Assert.Equal(1, _ai.Calls);
        }

        [Fact]
        public void Throws_when_target_is_missing()
        {
            var harness = new PluginHarness();

            var ex = Assert.Throws<InvalidPluginExecutionException>(
                () => Plugin(TestData.SecureConfiguration).Execute(harness.ServiceProvider.Object));

            Assert.Contains("Target", ex.Message);
        }

        [Fact]
        public void Throws_when_target_is_not_an_entity()
        {
            var harness = new PluginHarness();
            harness.InputParameters["Target"] = new EntityReference("incident", Guid.NewGuid());

            Assert.Throws<InvalidPluginExecutionException>(
                () => Plugin(TestData.SecureConfiguration).Execute(harness.ServiceProvider.Object));
        }

        [Fact]
        public void Throws_a_readable_error_when_secure_configuration_is_missing()
        {
            var harness = new PluginHarness().WithTarget(TestData.NewCase());

            var ex = Assert.Throws<InvalidPluginExecutionException>(
                () => Plugin(null).Execute(harness.ServiceProvider.Object));

            Assert.Contains("secure configuration", ex.Message);
            Assert.Equal(0, _ai.Calls);
        }

        [Fact]
        public void Public_constructor_accepts_the_platform_signature()
        {
            var plugin = new CaseEnrichmentPlugin("unsecure", TestData.SecureConfiguration);

            Assert.NotNull(plugin);
        }

        private CaseEnrichmentPlugin Plugin(string secureConfiguration)
        {
            return new CaseEnrichmentPlugin(secureConfiguration, (settings, tracing) => _ai);
        }
    }
}
