using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.CommandRouting.Editor;
using Deucarian.TemplateViewerWeb.Commands;
using Deucarian.TemplateViewerWeb.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerCommandHarnessCatalogTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GenericCatalogCoversEveryRegisteredCommand()
        {
            root = new GameObject("Harness Catalog");
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();

            WebViewerCommandHarnessCatalog catalog =
                WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                    bootstrap);
            string[] registered = WebViewerCommandHandlers.Create()
                .SelectMany(handler => handler.CommandNames)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] represented = catalog.Scenarios
                .Select(value => value.CommandName)
                .Distinct()
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(registered, represented);
            Assert.That(
                catalog.Scenarios.Count(value => value.RunAutomatically),
                Is.EqualTo(7));
            Assert.That(
                catalog.Scenarios.Single(
                    value => value.Id == "update-access-token")
                    .Payload.Value<string>("access_token"),
                Is.Empty);
        }

        [Test]
        public void CheckedInBrowserCatalogMatchesUnityGeneration()
        {
            root = new GameObject("Checked Browser Catalog");
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();
            WebViewerCommandHarnessCatalog generated =
                WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                    bootstrap);
            PackageInfo package = PackageInfo.FindForAssembly(
                typeof(WebViewerBootstrap).Assembly);
            string path = Path.Combine(
                package.resolvedPath,
                "Browser~",
                "commands.generated.json");
            JToken checkedIn = JToken.Parse(File.ReadAllText(path));
            JToken expected = JToken.FromObject(generated);

            Assert.That(
                JToken.DeepEquals(checkedIn, expected),
                Is.True,
                "Regenerate Browser~/commands.generated.json from the Unity " +
                "command composition.");
        }

        [Test]
        public void ProductFeatureReplacesVisibilityAndAddsItsOwnExamples()
        {
            root = new GameObject("Product Harness Catalog");
            WebViewerBootstrap bootstrap =
                root.AddComponent<WebViewerBootstrap>();
            root.AddComponent<HarnessFeature>();

            WebViewerCommandHarnessCatalog catalog =
                WebViewerCommandHarnessCatalogGenerator.CreateCatalog(
                    bootstrap);
            string[] commands = catalog.Scenarios
                .Select(value => value.CommandName)
                .Distinct()
                .ToArray();

            Assert.That(commands, Does.Contain("set_focus"));
            Assert.That(commands, Does.Not.Contain("select_elements"));
            Assert.That(commands, Does.Not.Contain("clear_selection"));
            WebViewerCommandHarnessScenario scenario =
                catalog.Scenarios.Single(value => value.Id == "set-focus");
            Assert.That(scenario.RunAutomatically, Is.True);
            Assert.That(scenario.Payload.Value<long>("revision"), Is.EqualTo(4));
            List<WebViewerCommandHarnessScenario> orderedScenarios =
                catalog.Scenarios.ToList();
            Assert.That(
                orderedScenarios.IndexOf(scenario),
                Is.LessThan(orderedScenarios.FindIndex(
                    value => value.Id == "dispose")));
        }

        [Test]
        public void CommandsWithoutExamplesRemainVisibleButAreNotAutomated()
        {
            var handlers = new[] { new HarnessCommandHandler("inspect_state") };
            WebViewerCommandHarnessCatalog catalog =
                WebViewerCommandHarnessCatalogBuilder.Create(
                    handlers,
                    Array.Empty<WebViewerCommandHarnessScenario>());

            Assert.That(catalog.Scenarios.Count, Is.EqualTo(1));
            Assert.That(catalog.Scenarios[0].CommandName, Is.EqualTo("inspect_state"));
            Assert.That(catalog.Scenarios[0].Label, Is.EqualTo("Inspect state"));
            Assert.That(catalog.Scenarios[0].RunAutomatically, Is.False);
        }

        [Test]
        public void EditorTesterConsumesTheLiveViewerCatalog()
        {
            root = new GameObject("Live Tester Catalog");
            root.AddComponent<WebViewerBootstrap>();
            var source = new WebViewerCommandTestCatalogSource();

            Assert.That(
                source.TryGetCatalogJson(out string json, out string error),
                Is.True,
                error);
            Assert.That(
                CommandTestCatalog.TryParse(
                    json,
                    out CommandTestCatalog catalog,
                    out error),
                Is.True,
                error);
            Assert.That(
                catalog.Scenarios.Select(value => value.CommandName),
                Does.Contain("initialize_viewer"));
            Assert.That(catalog.RemoteEndpoint, Is.EqualTo("direct"));
        }

        public sealed class HarnessFeature :
            WebViewerFeatureBehaviour,
            IWebViewerVisibilityFeatureFactory
        {
            public override IWebViewerVisibilityFeatureFactory
                VisibilityFeatureFactory => this;

            public override IReadOnlyList<ICommandHandler<WebViewerApplication>>
                CreateCommandHandlers() =>
                    new[] { new HarnessCommandHandler("set_focus") };

            public override IReadOnlyList<WebViewerCommandHarnessScenario>
                CreateCommandHarnessScenarios() =>
                    new[]
                    {
                        new WebViewerCommandHarnessScenario(
                            "set-focus",
                            "Set focus",
                            "set_focus",
                            new JObject { ["revision"] = 4 })
                    };

            public bool TryCreate(
                WebViewerModelContext context,
                out IWebViewerVisibilityFeature feature,
                out string error)
            {
                feature = null;
                error = "Not used by this catalog test.";
                return false;
            }
        }

        private sealed class HarnessCommandHandler :
            ICommandHandler<WebViewerApplication>
        {
            private readonly IReadOnlyList<string> commandNames;

            public HarnessCommandHandler(string commandName)
            {
                commandNames = new[] { commandName };
            }

            public IReadOnlyList<string> CommandNames => commandNames;

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<WebViewerApplication> context,
                CancellationToken cancellationToken) =>
                    Task.FromResult(CommandResult.Success());
        }
    }
}
