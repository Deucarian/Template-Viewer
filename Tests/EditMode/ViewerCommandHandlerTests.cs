using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.TemplateViewer.Commands;
using Deucarian.Authentication;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerCommandHandlerTests
    {
        [Test]
        public void EstablishedCreateSignatureRemainsSourceCompatible()
        {
            MethodInfo[] methods = typeof(ViewerCommandHandlers)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == nameof(ViewerCommandHandlers.Create))
                .ToArray();

            Assert.That(methods, Has.Length.EqualTo(1));
            ParameterInfo[] parameters = methods[0].GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(3));
            Assert.That(
                parameters.Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[]
                {
                    typeof(IAuthenticationEventPublisher),
                    typeof(bool),
                    typeof(ICommandHandler<ViewerApplication>)
                }));
            Assert.That(parameters.All(parameter => parameter.IsOptional), Is.True);
        }

        [Test]
        public void AuthenticationPublisherRetainsItsSolePublicConstructor()
        {
            ConstructorInfo[] constructors =
                typeof(ViewerAuthenticationEventPublisher).GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance);

            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(
                constructors[0].GetParameters()
                    .Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[]
                {
                    typeof(IViewerEventPublisher),
                    typeof(string)
                }));
        }

        [Test]
        public void EstablishedCreateBehaviorRemainsCompositionCompatible()
        {
            IReadOnlyList<ICommandHandler<ViewerApplication>> legacy =
                ViewerCommandHandlers.Create();
            string[] names = legacy
                .SelectMany(handler => handler.CommandNames)
                .OrderBy(value => value)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "clear_access_token",
                    "clear_selection",
                    "dispose_viewer",
                    "initialize_viewer",
                    "refresh_access_token",
                    "select_elements",
                    "update_access_token",
                    "updateaccesstoken"
                },
                names);
            Assert.DoesNotThrow(() =>
                new CommandHandlerRegistry<ViewerApplication>(
                    legacy.Concat(new[]
                    {
                        new DomainOnlyHandler(
                            "navigation",
                            "set_display_settings")
                    })));
        }

        [Test]
        public void RegistersOnlyTheDocumentedGenericApplicationCommands()
        {
            string[] names = ViewerCommandHandlers.CreateDefault()
                .SelectMany(handler => handler.CommandNames)
                .OrderBy(value => value)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "clear_access_token",
                    "clear_selection",
                    "dispose_viewer",
                    "fly",
                    "home",
                    "initialize_viewer",
                    "nav",
                    "navigate",
                    "navigation",
                    "navigation_mode",
                    "navigation_sensitivity",
                    "navigationmode",
                    "navigationsensitivity",
                    "orbit",
                    "origin",
                    "refresh_access_token",
                    "reset_camera",
                    "resetcamera",
                    "return_to_origin",
                    "returntoorigin",
                    "select_elements",
                    "set_display_settings",
                    "set_navigation_mode",
                    "set_navigation_sensitivity",
                    "setdisplaysettings",
                    "setnavigationmode",
                    "setnavigationsensitivity",
                    "toggle_top",
                    "toggle_top_down",
                    "toggletop",
                    "toggletopdown",
                    "top_down",
                    "top_view",
                    "topdown",
                    "topview",
                    "update_access_token",
                    "updateaccesstoken"
                },
                names);
        }

        [Test]
        public void RegistersEveryGenericWireNameExactlyOnce()
        {
            string[] duplicates = ViewerCommandHandlers.CreateDefault()
                .SelectMany(handler => handler.CommandNames)
                .GroupBy(value => value, StringComparer.Ordinal)
                .Where(group => group.Count() != 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.That(duplicates, Is.Empty);
        }

        [Test]
        public void DefaultAndNullPresentationFactoriesExposeTheSameWireSet()
        {
            IReadOnlyList<ICommandHandler<ViewerApplication>> defaults =
                ViewerCommandHandlers.CreateDefault();
            IReadOnlyList<ICommandHandler<ViewerApplication>> explicitNulls =
                ViewerCommandHandlers.CreateWithPresentation(
                    navigationController: null,
                    renderingController: null);

            CollectionAssert.AreEqual(
                defaults.SelectMany(handler => handler.CommandNames),
                explicitNulls.SelectMany(handler => handler.CommandNames));
            CollectionAssert.AreEqual(
                defaults.Select(handler => handler.GetType()),
                explicitNulls.Select(handler => handler.GetType()));
            Assert.That(explicitNulls, Has.Count.EqualTo(defaults.Count));
            for (int index = 0; index < defaults.Count; index++)
            {
                CollectionAssert.AreEqual(
                    defaults[index].CommandNames,
                    explicitNulls[index].CommandNames);
            }

            Assert.DoesNotThrow(() =>
                new CommandHandlerRegistry<ViewerApplication>(defaults));
            Assert.DoesNotThrow(() =>
                new CommandHandlerRegistry<ViewerApplication>(explicitNulls));
        }

        [Test]
        public void PackageOwnedPresentationAliasRejectsAProductCollision()
        {
            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    new CommandHandlerRegistry<ViewerApplication>(
                        ViewerCommandHandlers.CreateDefault().Concat(
                            new[]
                            {
                                new DomainOnlyHandler(" Navigation ")
                            })));

            Assert.That(
                exception.Message,
                Does.Contain("Duplicate command handler registration"));
            Assert.That(exception.Message, Does.Contain("navigation"));
        }

        [Test]
        public void ContainsNoReportOrActivityCommandNames()
        {
            string[] names = ViewerCommandHandlers.CreateDefault()
                .SelectMany(handler => handler.CommandNames)
                .ToArray();

            Assert.That(names.Any(name => name.Contains("report")), Is.False);
            Assert.That(names.Any(name => name.Contains("activity")), Is.False);
        }

        [Test]
        public void GenericAndDomainOnlyHandlersBuildOneCollisionFreeRegistry()
        {
            ICommandHandler<ViewerApplication>[] productHandlers =
            {
                new DomainOnlyHandler(
                    "select_report",
                    "select_activity")
            };
            ICommandHandler<ViewerApplication>[] aggregate =
                ViewerCommandHandlers.CreateDefault()
                    .Concat(productHandlers)
                    .ToArray();

            var registry = new CommandHandlerRegistry<ViewerApplication>(
                aggregate);

            Assert.That(registry.HandlerCount, Is.EqualTo(aggregate.Length));
            Assert.That(
                registry.CommandNames,
                Does.Contain("set_display_settings"));
            Assert.That(registry.CommandNames, Does.Contain("navigation"));
            Assert.That(registry.CommandNames, Does.Contain("select_report"));
            Assert.That(registry.CommandNames, Does.Contain("select_activity"));
        }

        [Test]
        public void ProductVisibilityCanReplaceGenericSelectionCommands()
        {
            string[] names = ViewerCommandHandlers.CreateDefault(
                    includeGenericVisibilityCommands: false)
                .SelectMany(handler => handler.CommandNames)
                .ToArray();

            Assert.That(names, Does.Not.Contain("select_elements"));
            Assert.That(names, Does.Not.Contain("clear_selection"));
            Assert.That(names, Does.Contain("initialize_viewer"));
            Assert.That(names, Does.Contain("dispose_viewer"));
            Assert.That(names, Does.Contain("navigation"));
            Assert.That(names, Does.Contain("set_display_settings"));
        }

        [Test]
        public void ProductInitializationCanReplaceGenericInitializer()
        {
            var productHandler = new ProductInitializationHandler();

            ICommandHandler<ViewerApplication>[] handlers =
                ViewerCommandHandlers.CreateDefault(
                        initializationHandler: productHandler)
                    .ToArray();

            Assert.That(handlers[0], Is.SameAs(productHandler));
            Assert.That(
                handlers.SelectMany(handler => handler.CommandNames)
                    .Count(name => name == "initialize_viewer"),
                Is.EqualTo(1));
        }

        [Test]
        public async Task AuthenticationOutcomesUseTheExistingSanitizedPublisher()
        {
            var publisher = new RecordingEventPublisher();
            var adapter = new ViewerAuthenticationEventPublisher(
                publisher,
                "parent:https://host.example");
            var expiry = new DateTimeOffset(
                2026,
                8,
                18,
                10,
                30,
                0,
                TimeSpan.Zero);

            await adapter.PublishAsync(
                AuthenticationEventNames.AccessTokenUpdated,
                new AuthenticationStatusSnapshot(
                    AuthenticationStatus.Active,
                    true,
                    true,
                    expiry),
                CancellationToken.None);

            Assert.That(
                publisher.EventName,
                Is.EqualTo(AuthenticationEventNames.AccessTokenUpdated));
            Assert.That(
                publisher.RemoteEndpoint,
                Is.EqualTo("parent:https://host.example"));
            Assert.That(publisher.Payload.Value<string>("status"), Is.EqualTo("Active"));
            Assert.That(publisher.Payload.Value<bool>("has_access_token"), Is.True);
            Assert.That(publisher.Payload.Value<bool>("can_refresh"), Is.True);
            Assert.That(publisher.Payload.Value<bool>("expiry_known"), Is.True);
            Assert.That(
                publisher.Payload.Value<string>("expires_at_utc"),
                Is.EqualTo("2026-08-18T10:30:00.0000000+00:00"));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "status",
                    "has_access_token",
                    "can_refresh",
                    "expiry_known",
                    "expires_at_utc"
                },
                publisher.Payload.Properties()
                    .Select(property => property.Name)
                    .ToArray());
        }

        [Test]
        public void AuthenticationOutcomePayloadIsDefensiveAndTokenFree()
        {
            var expiry = new DateTimeOffset(
                2026,
                8,
                18,
                10,
                30,
                0,
                TimeSpan.Zero);
            var status = new AuthenticationStatusSnapshot(
                AuthenticationStatus.Active,
                true,
                true,
                expiry);
            ViewerAuthenticationOutcomeEventArgs outcome =
                ViewerAuthenticationOutcomeEventArgs.Create(
                    AuthenticationEventNames.AccessTokenUpdated,
                    status);

            JObject first = outcome.Payload;
            first["status"] = "mutated";
            first["access_token"] = "observer-only-test-value";
            first.Remove("can_refresh");
            JObject second = outcome.Payload;

            Assert.That(outcome.Status, Is.SameAs(status));
            Assert.That(
                outcome.EventName,
                Is.EqualTo(AuthenticationEventNames.AccessTokenUpdated));
            Assert.That(second.Value<string>("status"), Is.EqualTo("Active"));
            Assert.That(second.Value<bool>("can_refresh"), Is.True);
            Assert.That(second.Property("access_token"), Is.Null);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "status",
                    "has_access_token",
                    "can_refresh",
                    "expiry_known",
                    "expires_at_utc"
                },
                second.Properties()
                    .Select(property => property.Name)
                    .ToArray());
        }

        [TestCase(AuthenticationEventNames.AccessTokenUpdated)]
        [TestCase(AuthenticationEventNames.AccessTokenRefreshed)]
        [TestCase(AuthenticationEventNames.AccessTokenCleared)]
        public async Task AuthenticationObserverCannotSuppressOrDuplicateRemote(
            string eventName)
        {
            var publisher = new RecordingEventPublisher();
            int observerCount = 0;
            var adapter = new ViewerAuthenticationEventPublisher(
                publisher,
                "parent:https://host.example",
                outcome =>
                {
                    observerCount++;
                    outcome.Payload["access_token"] =
                        "observer-only-test-value";
                    throw new InvalidOperationException(
                        "Expected local observer failure.");
                });

            await adapter.PublishAsync(
                eventName,
                new AuthenticationStatusSnapshot(
                    AuthenticationStatus.Active,
                    true,
                    false,
                    null),
                CancellationToken.None);

            Assert.That(observerCount, Is.EqualTo(1));
            Assert.That(publisher.PublishCount, Is.EqualTo(1));
            Assert.That(publisher.EventName, Is.EqualTo(eventName));
            Assert.That(
                publisher.RemoteEndpoint,
                Is.EqualTo("parent:https://host.example"));
            Assert.That(publisher.Payload.Property("access_token"), Is.Null);
        }

        private sealed class RecordingEventPublisher :
            IViewerEventPublisher
        {
            public string EventName { get; private set; }
            public JObject Payload { get; private set; }
            public string RemoteEndpoint { get; private set; }
            public int PublishCount { get; private set; }

            public Task PublishAsync(
                string eventName,
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken = default)
            {
                PublishCount++;
                EventName = eventName;
                Payload = (JObject)payload.DeepClone();
                RemoteEndpoint = remoteEndpoint;
                return Task.CompletedTask;
            }
        }

        private sealed class ProductInitializationHandler :
            ICommandHandler<ViewerApplication>
        {
            public System.Collections.Generic.IReadOnlyList<string>
                CommandNames { get; } = new[] { "initialize_viewer" };

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<ViewerApplication> context,
                CancellationToken cancellationToken) =>
                Task.FromResult(CommandResult.Success());
        }

        private sealed class DomainOnlyHandler :
            ICommandHandler<ViewerApplication>
        {
            public DomainOnlyHandler(params string[] names)
            {
                CommandNames = names;
            }

            public System.Collections.Generic.IReadOnlyList<string>
                CommandNames { get; }

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<ViewerApplication> context,
                CancellationToken cancellationToken) =>
                Task.FromResult(CommandResult.Success());
        }
    }
}
