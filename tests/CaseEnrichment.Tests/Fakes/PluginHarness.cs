using System;
using Microsoft.Xrm.Sdk;
using Moq;

namespace MM.Dataverse.CaseEnrichment.Tests.Fakes
{
    /// <summary>
    /// Wires the services the platform hands a plugin through IServiceProvider.
    /// </summary>
    internal sealed class PluginHarness
    {
        public PluginHarness(string messageName = "Create", string entityName = "incident", int depth = 1)
        {
            Context.SetupGet(c => c.MessageName).Returns(messageName);
            Context.SetupGet(c => c.PrimaryEntityName).Returns(entityName);
            Context.SetupGet(c => c.Depth).Returns(depth);
            Context.SetupGet(c => c.UserId).Returns(UserId);
            Context.SetupGet(c => c.InputParameters).Returns(InputParameters);

            var factory = new Mock<IOrganizationServiceFactory>();
            factory.Setup(f => f.CreateOrganizationService(It.IsAny<Guid?>())).Returns(Organization.Object);

            ServiceProvider.Setup(s => s.GetService(typeof(ITracingService))).Returns(Trace);
            ServiceProvider.Setup(s => s.GetService(typeof(IPluginExecutionContext))).Returns(Context.Object);
            ServiceProvider.Setup(s => s.GetService(typeof(IOrganizationServiceFactory))).Returns(factory.Object);
        }

        public Guid UserId { get; } = Guid.NewGuid();

        public TraceLog Trace { get; } = new TraceLog();

        public ParameterCollection InputParameters { get; } = new ParameterCollection();

        public Mock<IPluginExecutionContext> Context { get; } = new Mock<IPluginExecutionContext>();

        public Mock<IOrganizationService> Organization { get; } = new Mock<IOrganizationService>();

        public Mock<IServiceProvider> ServiceProvider { get; } = new Mock<IServiceProvider>();

        public PluginHarness WithTarget(Entity target)
        {
            InputParameters["Target"] = target;
            return this;
        }
    }
}
