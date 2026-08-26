using System;
using System.Collections.Generic;
using Deucarian.API.Core;
using Deucarian.Session.APIIntegration;
using Deucarian.ViewerAuthentication;

namespace Deucarian.TemplateViewer
{
    public abstract partial class ViewerBootstrap
    {
        private void ComposeAuthentication(
            out IApiClient apiClient,
            out string apiBaseUrl,
            out IReadOnlyCollection<string> effectiveAuthenticatedOrigins)
        {
            ViewerRuntimeConnectionResolution resolution =
                ViewerRuntimeConnectionProviderRegistry.Resolve();
            if (resolution == null)
            {
                throw new InvalidOperationException(
                    "Runtime connection resolution returned no result.");
            }

            if (ShouldUseLocalAuthentication(resolution.Status))
            {
                var localSession = new ViewerAuthenticationSession();
                IApiClient localClient = ApiClientFactory.Create(
                    apiClientConfig,
                    localSession.ApiAuthProvider);
                IViewerAuthenticationAcquisitionProvider localProvider =
                    CreateAuthenticationAcquisitionProvider(localClient);
                IDisposable localRegistration =
                    ViewerAuthenticationTargetRegistry.Register(
                        "viewer-" + GetInstanceID(),
                        "Viewer",
                        localSession,
                        localProvider);

                authenticationSession = localSession;
                authenticationAcquisitionProvider = localProvider;
                authenticationTargetRegistration = localRegistration;
                apiClient = localClient;
                apiBaseUrl = apiClientConfig != null
                    ? apiClientConfig.BaseUrl
                    : null;
                effectiveAuthenticatedOrigins =
                    MergeAuthenticatedOrigins(null);
                return;
            }

            ViewerRuntimeConnection connection = resolution.Connection;
            if (!IsValidRuntimeConnection(connection))
            {
                connection?.Dispose();
                throw new InvalidOperationException(
                    "The resolved runtime connection is incomplete.");
            }

            try
            {
                ViewerAuthenticationTargetRegistry.TryGet(
                    connection.TargetId,
                    out ViewerAuthenticationTarget target);
                authenticationSession = connection.Session;
                authenticationAcquisitionProvider =
                    target?.AcquisitionProvider;
                authenticationTargetRegistration = null;
                apiClient = connection.ApiClient;
                apiBaseUrl = connection.ApiBaseUrl;
                effectiveAuthenticatedOrigins = MergeAuthenticatedOrigins(
                    connection.AuthenticatedOrigins);
                runtimeConnection = connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static bool ShouldUseLocalAuthentication(
            ViewerRuntimeConnectionResolutionStatus status)
        {
            switch (status)
            {
                case ViewerRuntimeConnectionResolutionStatus.None:
                    return true;
                case ViewerRuntimeConnectionResolutionStatus.Resolved:
                    return false;
                case ViewerRuntimeConnectionResolutionStatus.Failed:
                    throw new InvalidOperationException(
                        "The optional runtime connection provider failed.");
                case ViewerRuntimeConnectionResolutionStatus.Ambiguous:
                    throw new InvalidOperationException(
                        "Multiple runtime connection providers are active.");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status),
                        status,
                        "Unknown runtime connection resolution status.");
            }
        }

        private static bool IsValidRuntimeConnection(
            ViewerRuntimeConnection connection)
        {
            if (connection == null ||
                string.IsNullOrWhiteSpace(connection.TargetId) ||
                connection.Session == null ||
                connection.ApiClient == null)
            {
                return false;
            }

            if (!ViewerAuthenticationTargetRegistry.TryGet(
                    connection.TargetId,
                    out ViewerAuthenticationTarget target) ||
                !ReferenceEquals(target.Session, connection.Session))
            {
                return false;
            }

            return Uri.TryCreate(
                       connection.ApiBaseUrl,
                       UriKind.Absolute,
                       out Uri baseUri) &&
                   (baseUri.Scheme == Uri.UriSchemeHttp ||
                    baseUri.Scheme == Uri.UriSchemeHttps) &&
                   string.IsNullOrEmpty(baseUri.UserInfo) &&
                   string.IsNullOrEmpty(baseUri.Query) &&
                   string.IsNullOrEmpty(baseUri.Fragment);
        }

        private IReadOnlyCollection<string> MergeAuthenticatedOrigins(
            IEnumerable<string> connectionOrigins)
        {
            var merged = new List<string>();
            AddOrigins(merged, connectionOrigins);
            AddOrigins(merged, authenticatedModelOrigins);
            return merged;
        }

        private static void AddOrigins(
            ICollection<string> destination,
            IEnumerable<string> origins)
        {
            if (origins == null)
            {
                return;
            }

            foreach (string origin in origins)
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    continue;
                }

                string normalized = origin.Trim();
                bool exists = false;
                foreach (string current in destination)
                {
                    if (string.Equals(
                            current,
                            normalized,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    destination.Add(normalized);
                }
            }
        }

        private IViewerAuthenticationAcquisitionProvider
            CreateAuthenticationAcquisitionProvider(IApiClient apiClient)
        {
            SessionTokenEndpointProfile profile =
                ResolvedAuthenticationTokenEndpointProfile;
            return profile == null
                ? null
                : ViewerAuthenticationEndpointProviderFactory.Create(
                    profile,
                    apiClient);
        }

        private void ReleaseAuthenticationComposition()
        {
            IDisposable targetRegistration =
                authenticationTargetRegistration;
            authenticationTargetRegistration = null;
            TryCleanup(() => targetRegistration?.Dispose());

            IDisposable connection = runtimeConnection;
            runtimeConnection = null;
            TryCleanup(() => connection?.Dispose());

            authenticationAcquisitionProvider = null;
            authenticationSession = null;
        }
    }
}
