using System;
using System.Collections.Generic;
using System.Linq;
using Deucarian.CommandRouting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewerWeb.Commands
{
    [Serializable]
    public sealed class WebViewerCommandHarnessScenario
    {
        public WebViewerCommandHarnessScenario(
            string id,
            string label,
            string commandName,
            JObject payload = null,
            bool runAutomatically = true,
            bool expectedSuccess = true,
            bool isDefault = false)
        {
            Id = Require(id, nameof(id));
            Label = Require(label, nameof(label));
            CommandName = Require(commandName, nameof(commandName));
            Payload = payload == null
                ? new JObject()
                : (JObject)payload.DeepClone();
            RunAutomatically = runAutomatically;
            ExpectedSuccess = expectedSuccess;
            IsDefault = isDefault;
        }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("label")]
        public string Label { get; }

        [JsonProperty("command")]
        public string CommandName { get; }

        [JsonProperty("payload")]
        public JObject Payload { get; }

        [JsonProperty("run_automatically")]
        public bool RunAutomatically { get; }

        [JsonProperty("expected_success")]
        public bool ExpectedSuccess { get; }

        [JsonIgnore]
        public bool IsDefault { get; }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty value is required.",
                    parameterName);
            }

            return value.Trim();
        }
    }

    [Serializable]
    public sealed class WebViewerCommandHarnessCatalog
    {
        internal WebViewerCommandHarnessCatalog(
            IReadOnlyList<WebViewerCommandHarnessScenario> scenarios,
            string defaultScenarioId)
        {
            Scenarios = scenarios ??
                throw new ArgumentNullException(nameof(scenarios));
            DefaultScenarioId = defaultScenarioId ?? string.Empty;
        }

        [JsonProperty("schema_version")]
        public int SchemaVersion => 1;

        [JsonProperty("default_scenario_id")]
        public string DefaultScenarioId { get; }

        [JsonProperty("scenarios")]
        public IReadOnlyList<WebViewerCommandHarnessScenario> Scenarios
        {
            get;
        }
    }

    public static class WebViewerCommandHarnessCatalogBuilder
    {
        public static WebViewerCommandHarnessCatalog Create(
            IEnumerable<ICommandHandler<WebViewerApplication>> handlers,
            IEnumerable<WebViewerCommandHarnessScenario> scenarios)
        {
            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            var commandNames = new SortedSet<string>(StringComparer.Ordinal);
            foreach (ICommandHandler<WebViewerApplication> handler in handlers)
            {
                if (handler == null || handler.CommandNames == null)
                {
                    throw new ArgumentException(
                        "Command handlers and their names cannot be null.",
                        nameof(handlers));
                }

                for (int index = 0; index < handler.CommandNames.Count; index++)
                {
                    string commandName = Normalize(
                        handler.CommandNames[index],
                        nameof(handlers));
                    if (!commandNames.Add(commandName))
                    {
                        throw new InvalidOperationException(
                            "Duplicate command handler registration for '" +
                            commandName + "'.");
                    }
                }
            }

            var scenarioIds = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<WebViewerCommandHarnessScenario>();
            string defaultScenarioId = string.Empty;
            if (scenarios != null)
            {
                foreach (WebViewerCommandHarnessScenario scenario in scenarios)
                {
                    if (scenario == null)
                    {
                        throw new ArgumentException(
                            "Harness scenarios cannot contain null entries.",
                            nameof(scenarios));
                    }

                    if (!commandNames.Contains(scenario.CommandName))
                    {
                        throw new InvalidOperationException(
                            "Harness scenario '" + scenario.Id +
                            "' targets unregistered command '" +
                            scenario.CommandName + "'.");
                    }

                    if (!scenarioIds.Add(scenario.Id))
                    {
                        throw new InvalidOperationException(
                            "Duplicate harness scenario id '" +
                            scenario.Id + "'.");
                    }

                    if (scenario.IsDefault)
                    {
                        if (defaultScenarioId.Length > 0)
                        {
                            throw new InvalidOperationException(
                                "Only one harness scenario can be the default.");
                        }

                        defaultScenarioId = scenario.Id;
                    }

                    result.Add(scenario);
                }
            }

            var representedCommands = new HashSet<string>(
                result.Select(value => value.CommandName),
                StringComparer.Ordinal);
            foreach (string commandName in commandNames)
            {
                if (representedCommands.Contains(commandName))
                {
                    continue;
                }

                string id = "command-" + commandName.Replace('_', '-');
                if (!scenarioIds.Add(id))
                {
                    throw new InvalidOperationException(
                        "Generated harness scenario id '" + id +
                        "' is already in use.");
                }

                result.Add(new WebViewerCommandHarnessScenario(
                    id,
                    Humanize(commandName),
                    commandName,
                    runAutomatically: false));
            }

            return new WebViewerCommandHarnessCatalog(
                result,
                defaultScenarioId);
        }

        public static IReadOnlyList<WebViewerCommandHarnessScenario>
            CreateGenericScenarios(bool includeGenericVisibilityCommands = true)
        {
            var scenarios = new List<WebViewerCommandHarnessScenario>
            {
                Scenario(
                    "initialize",
                    "Initialize viewer",
                    "initialize_viewer",
                    new JObject { ["revision"] = "$revision" }),
                Scenario(
                    "dispose",
                    "Dispose viewer",
                    "dispose_viewer",
                    new JObject { ["revision"] = "$revision" }),
                Scenario(
                    "update-access-token",
                    "Update access token (safe invalid example)",
                    "update_access_token",
                    new JObject { ["access_token"] = string.Empty },
                    runAutomatically: false,
                    expectedSuccess: false),
                Scenario(
                    "update-access-token-legacy",
                    "Update access token compatibility alias",
                    "updateaccesstoken",
                    new JObject { ["access_token"] = string.Empty },
                    runAutomatically: false,
                    expectedSuccess: false),
                Scenario(
                    "refresh-access-token",
                    "Refresh access token",
                    "refresh_access_token",
                    runAutomatically: false),
                Scenario(
                    "clear-access-token",
                    "Clear access token",
                    "clear_access_token",
                    runAutomatically: false)
            };

            if (includeGenericVisibilityCommands)
            {
                scenarios.Insert(1, Scenario(
                    "select-red",
                    "Select red",
                    "select_elements",
                    new JObject
                    {
                        ["revision"] = "$revision",
                        ["element_ids"] = new JArray("red")
                    }));
                scenarios.Insert(2, Scenario(
                    "select-green-blue",
                    "Select green and blue",
                    "select_elements",
                    new JObject
                    {
                        ["revision"] = "$revision",
                        ["element_ids"] = new JArray("green", "blue")
                    }));
                scenarios.Insert(3, Scenario(
                    "clear-selection",
                    "Clear selection",
                    "clear_selection",
                    new JObject { ["revision"] = "$revision" }));
                scenarios.Insert(4, Scenario(
                    "invalid-selection",
                    "Reject unknown element",
                    "select_elements",
                    new JObject
                    {
                        ["revision"] = "$revision",
                        ["element_ids"] = new JArray("missing")
                    },
                    expectedSuccess: false));
                scenarios.Insert(5, Scenario(
                    "stale-selection",
                    "Reject stale revision",
                    "select_elements",
                    new JObject
                    {
                        ["revision"] = "$stale_revision",
                        ["element_ids"] = new JArray("blue")
                    },
                    expectedSuccess: false));
            }

            return scenarios;
        }

        private static WebViewerCommandHarnessScenario Scenario(
            string id,
            string label,
            string commandName,
            JObject payload = null,
            bool runAutomatically = true,
            bool expectedSuccess = true) =>
                new WebViewerCommandHarnessScenario(
                    id,
                    label,
                    commandName,
                    payload,
                    runAutomatically,
                    expectedSuccess);

        private static string Normalize(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Command names cannot be empty.",
                    parameterName);
            }

            return value.Trim();
        }

        private static string Humanize(string commandName)
        {
            string value = commandName.Replace('_', ' ');
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
