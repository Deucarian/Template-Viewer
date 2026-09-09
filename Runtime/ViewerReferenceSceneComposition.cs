using Deucarian.TemplateViewer.Selection;
using UnityEngine;

namespace Deucarian.TemplateViewer
{
    /// <summary>Creates only the default scene objects absent from a viewer.</summary>
    internal static class ViewerReferenceSceneComposition
    {
        internal static void EnsureDependencies(
            Transform bootstrapRoot,
            ref Transform loadedModelParent,
            ref GameObject embeddedReferenceModel)
        {
            if (loadedModelParent == null)
            {
                GameObject parent = new GameObject("Loaded Model");
                parent.transform.SetParent(bootstrapRoot, false);
                loadedModelParent = parent.transform;
            }

            if (embeddedReferenceModel == null)
            {
                embeddedReferenceModel = CreateEmbeddedReferenceModel(
                    bootstrapRoot);
            }
        }

        private static GameObject CreateEmbeddedReferenceModel(
            Transform bootstrapRoot)
        {
            GameObject root = new GameObject("Embedded Reference Model");
            root.transform.SetParent(bootstrapRoot, false);
            CreateElement(
                root.transform,
                "red",
                PrimitiveType.Cube,
                new Vector3(-2.2f, 0f, 0f));
            CreateElement(
                root.transform,
                "green",
                PrimitiveType.Sphere,
                Vector3.zero);
            CreateElement(
                root.transform,
                "blue",
                PrimitiveType.Capsule,
                new Vector3(2.2f, 0f, 0f));
            return root;
        }

        private static void CreateElement(
            Transform parent,
            string id,
            PrimitiveType primitiveType,
            Vector3 position)
        {
            GameObject element = GameObject.CreatePrimitive(primitiveType);
            element.name = "Element " + id;
            element.transform.SetParent(parent, false);
            element.transform.localPosition = position;
            element.AddComponent<ViewerElement>().Initialize(id);
        }
    }
}
