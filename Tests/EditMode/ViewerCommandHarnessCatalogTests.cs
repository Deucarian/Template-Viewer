using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
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

        [Test]
        public void CommandsWithoutExamplesRemainVisibleButAreNotAutomated()
        {
            var handlers = new[] { new HarnessCommandHandler("inspect_state") };
            ViewerCommandHarnessCatalog catalog =
                ViewerCommandHarnessCatalogBuilder.Create(
                    handlers,
                    Array.Empty<ViewerCommandHarnessScenario>());

            Assert.That(catalog.Scenarios.Count, Is.EqualTo(1));
            Assert.That(
                catalog.Scenarios[0].CommandName,
                Is.EqualTo("inspect_state"));
            Assert.That(
                catalog.Scenarios[0].Label,
                Is.EqualTo("Inspect state"));
            Assert.That(catalog.Scenarios[0].RunAutomatically, Is.False);
        }

        [Test]
        public void RejectsMultipleDefaultExamples()
        {
            var handlers = new[] { new HarnessCommandHandler("set_focus") };
            var scenarios = new[]
            {
                new ViewerCommandHarnessScenario(
                    "first",
                    "First",
                    "set_focus",
                    isDefault: true),
                new ViewerCommandHarnessScenario(
                    "second",
                    "Second",
                    "set_focus",
                    isDefault: true)
            };

            Assert.Throws<InvalidOperationException>(() =>
                ViewerCommandHarnessCatalogBuilder.Create(
                    handlers,
                    scenarios));
        }

        private sealed class HarnessCommandHandler :
            ICommandHandler<ViewerApplication>
        {
            private readonly IReadOnlyList<string> commandNames;

            public HarnessCommandHandler(string commandName)
            {
                commandNames = new[] { commandName };
            }

            public IReadOnlyList<string> CommandNames => commandNames;

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<ViewerApplication> context,
                CancellationToken cancellationToken) =>
                Task.FromResult(CommandResult.Success());
        }
    }
}
