using System;
using System.Collections.Generic;
using Deucarian.CommandRouting;
using Deucarian.TemplateViewer.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deucarian.TemplateViewer
{
    public sealed class ViewerModelContext
    {
        public ViewerModelContext(
            GameObject referenceRoot,
            ViewerModelDescriptor descriptor,
            long initialRevision)
        {
            ReferenceRoot = referenceRoot ??
                throw new ArgumentNullException(nameof(referenceRoot));
            Descriptor = descriptor;
            InitialRevision = initialRevision;
        }

        public GameObject ReferenceRoot { get; }
        public ViewerModelDescriptor Descriptor { get; }
        public long InitialRevision { get; }
    }

    /// <summary>
    /// Owns every visibility change for one loaded model. A product may replace
    /// the template's generic element selection by supplying one factory.
    /// </summary>
    public interface IViewerVisibilityFeature : IDisposable
    {
        int IndexedElementCount { get; }
        int SelectedElementCount { get; }
    }

    public interface IViewerVisibilityFeatureFactory
    {
        bool TryCreate(
            ViewerModelContext context,
            out IViewerVisibilityFeature feature,
            out string error);
    }

    /// <summary>
    /// Scene-local extension point for product commands and product visibility.
    /// Add derived components beside the platform-specific ViewerBootstrap or
    /// assign same-scene components through its explicit feature list.
    /// </summary>
    public abstract class ViewerFeatureBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Replaces the generic initialize_viewer handler when a product needs
        /// to resolve a typed project/model context before model loading.
        /// </summary>
        public virtual ICommandHandler<ViewerApplication>
            InitializationCommandHandler => null;

        public virtual IViewerVisibilityFeatureFactory
            VisibilityFeatureFactory => null;

        public virtual IReadOnlyList<ICommandHandler<ViewerApplication>>
            CreateCommandHandlers() =>
                Array.Empty<ICommandHandler<ViewerApplication>>();

        /// <summary>
        /// Supplies safe interactive-harness examples for commands contributed by
        /// this feature. The harness catalog still includes every registered
        /// command when no example is supplied, but leaves it out of the
        /// automatic run until a representative payload is available.
        /// </summary>
        public virtual IReadOnlyList<ViewerCommandHarnessScenario>
            CreateCommandHarnessScenarios() =>
                Array.Empty<ViewerCommandHarnessScenario>();

        public virtual void Attach(ViewerApplication application)
        {
        }

        public virtual void Detach(ViewerApplication application)
        {
        }
    }

    public static class ViewerFeatureComposition
    {
        internal static ViewerFeatureBehaviour[] ResolveBehaviours(
            ViewerBootstrap bootstrap,
            IReadOnlyList<ViewerFeatureBehaviour> explicitFeatures)
        {
            if (bootstrap == null)
            {
                throw new ArgumentNullException(nameof(bootstrap));
            }

            ViewerFeatureBehaviour[] localFeatures =
                bootstrap.GetComponents<ViewerFeatureBehaviour>();
            int explicitCount = explicitFeatures?.Count ?? 0;
            var resolved = new List<ViewerFeatureBehaviour>(
                localFeatures.Length + explicitCount);
            var instanceIds = new HashSet<int>();

            for (int index = 0; index < localFeatures.Length; index++)
            {
                AddUnique(localFeatures[index], resolved, instanceIds);
            }

            Scene bootstrapScene = bootstrap.gameObject.scene;
            for (int index = 0; index < explicitCount; index++)
            {
                ViewerFeatureBehaviour feature = explicitFeatures[index];
                ValidateExplicitFeature(
                    bootstrap,
                    bootstrapScene,
                    feature,
                    index);
                AddUnique(feature, resolved, instanceIds);
            }

            return resolved.ToArray();
        }

        public static ICommandHandler<ViewerApplication>
            ResolveInitializationCommandHandler(
                IReadOnlyList<ViewerFeatureBehaviour> features)
        {
            ICommandHandler<ViewerApplication> result = null;
            if (features == null)
            {
                return null;
            }

            for (int index = 0; index < features.Count; index++)
            {
                ICommandHandler<ViewerApplication> candidate =
                    features[index]?.InitializationCommandHandler;
                if (candidate == null)
                {
                    continue;
                }

                if (result != null && !ReferenceEquals(result, candidate))
                {
                    throw new InvalidOperationException(
                        "Only one viewer feature may own initialization.");
                }

                if (!HandlesInitialization(candidate))
                {
                    throw new InvalidOperationException(
                        "A product initialization handler must handle only " +
                        InitializeViewerCommandHandler.CommandName + ".");
                }

                result = candidate;
            }

            return result;
        }

        private static void ValidateExplicitFeature(
            ViewerBootstrap bootstrap,
            Scene bootstrapScene,
            ViewerFeatureBehaviour feature,
            int index)
        {
            if (feature == null)
            {
                throw new InvalidOperationException(
                    "Explicit viewer feature at index " + index +
                    " on bootstrap '" + bootstrap.name +
                    "' is null or destroyed. Assign a ViewerFeatureBehaviour " +
                    "from the bootstrap scene or remove the entry.");
            }

            Scene featureScene = feature.gameObject.scene;
            if (!featureScene.IsValid() || !featureScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Explicit viewer feature '" + feature.name +
                    "' at index " + index +
                    " is not part of a valid loaded scene. Assign a scene " +
                    "instance rather than a prefab asset.");
            }

            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Viewer bootstrap '" + bootstrap.name +
                    "' is not part of a valid loaded scene and cannot " +
                    "compose explicit features.");
            }

            if (featureScene.handle != bootstrapScene.handle)
            {
                throw new InvalidOperationException(
                    "Explicit viewer feature '" + feature.name +
                    "' at index " + index + " belongs to scene '" +
                    SceneLabel(featureScene) + "', but bootstrap '" +
                    bootstrap.name + "' belongs to scene '" +
                    SceneLabel(bootstrapScene) +
                    "'. Move the feature into the bootstrap scene or " +
                    "remove the reference.");
            }
        }

        private static void AddUnique(
            ViewerFeatureBehaviour feature,
            ICollection<ViewerFeatureBehaviour> resolved,
            ISet<int> instanceIds)
        {
            if (feature != null && instanceIds.Add(feature.GetInstanceID()))
            {
                resolved.Add(feature);
            }
        }

        private static string SceneLabel(Scene scene) =>
            string.IsNullOrWhiteSpace(scene.name)
                ? "<unnamed>"
                : scene.name;

        private static bool HandlesInitialization(
            ICommandHandler<ViewerApplication> handler)
        {
            IReadOnlyList<string> names = handler.CommandNames;
            return names != null &&
                   names.Count == 1 &&
                   string.Equals(
                       names[0],
                       InitializeViewerCommandHandler.CommandName,
                       StringComparison.Ordinal);
        }
    }
}
