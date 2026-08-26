using System;
using System.Linq;
using Deucarian.TemplateViewer.Commands;
using NUnit.Framework;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerCommandHarnessCatalogTests
    {
        [Test]
        public void GenericCatalogPreservesCommandWireNames()
        {
            var handlers = ViewerCommandHandlers.Create();
            ViewerCommandHarnessCatalog catalog =
                ViewerCommandHarnessCatalogBuilder.Create(
                    handlers,
                    ViewerCommandHarnessCatalogBuilder
                        .CreateGenericScenarios());

            string[] commands = catalog.Scenarios
                .Select(value => value.CommandName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.That(commands, Does.Contain("initialize_viewer"));
            Assert.That(commands, Does.Contain("select_elements"));
            Assert.That(commands, Does.Contain("clear_selection"));
            Assert.That(commands, Does.Contain("dispose_viewer"));
            Assert.That(commands, Does.Contain("update_access_token"));
        }

        [Test]
        public void CatalogRejectsScenarioForUnregisteredCommand()
        {
            var scenario = new ViewerCommandHarnessScenario(
                "missing",
                "Missing",
                "missing_command");

            Assert.Throws<InvalidOperationException>(
                () => ViewerCommandHarnessCatalogBuilder.Create(
                    ViewerCommandHandlers.Create(),
                    new[] { scenario }));
        }
    }
}
