using Deucarian.TemplateViewer.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerReferenceSceneCompositionTests
    {
        private GameObject root;
        private Transform loadedModelParent;
        private GameObject embeddedReferenceModel;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Reference scene composition test");
            root.transform.position = new Vector3(8f, 3f, -5f);
            root.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            root.transform.localScale = Vector3.one * 2f;
            loadedModelParent = null;
            embeddedReferenceModel = null;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void MissingDependenciesKeepReferenceNamesShapesAndLocalPositions()
        {
            EnsureDependencies();

            Assert.That(root.transform.childCount, Is.EqualTo(2));
            Assert.That(loadedModelParent.name, Is.EqualTo("Loaded Model"));
            Assert.That(loadedModelParent.parent, Is.EqualTo(root.transform));
            Assert.That(loadedModelParent.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(loadedModelParent.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(loadedModelParent.localScale, Is.EqualTo(Vector3.one));
            Assert.That(embeddedReferenceModel.name, Is.EqualTo("Embedded Reference Model"));
            Assert.That(embeddedReferenceModel.transform.parent, Is.EqualTo(root.transform));
            Assert.That(embeddedReferenceModel.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(embeddedReferenceModel.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(embeddedReferenceModel.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(embeddedReferenceModel.transform.childCount, Is.EqualTo(3));
            AssertElement<BoxCollider>(0, "red", new Vector3(-2.2f, 0f, 0f));
            AssertElement<SphereCollider>(1, "green", Vector3.zero);
            AssertElement<CapsuleCollider>(2, "blue", new Vector3(2.2f, 0f, 0f));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void OnlyMissingDependenciesAreCreated(bool hasParent, bool hasReference)
        {
            Transform authoredParent = hasParent ? CreateAuthored("Authored parent").transform : null;
            GameObject authoredReference = hasReference ? CreateAuthored("Authored reference") : null;
            loadedModelParent = authoredParent;
            embeddedReferenceModel = authoredReference;
            Quaternion parentRotation = hasParent ? authoredParent.localRotation : Quaternion.identity;
            Quaternion referenceRotation = hasReference ? authoredReference.transform.localRotation : Quaternion.identity;

            EnsureDependencies();

            Assert.That(root.transform.childCount, Is.EqualTo(2));
            if (hasParent)
            {
                Assert.That(loadedModelParent, Is.SameAs(authoredParent));
                AssertAuthoredTransform(loadedModelParent, parentRotation);
            }
            if (hasReference)
            {
                Assert.That(embeddedReferenceModel, Is.SameAs(authoredReference));
                AssertAuthoredTransform(embeddedReferenceModel.transform, referenceRotation);
                Assert.That(embeddedReferenceModel.transform.childCount, Is.Zero);
            }
        }

        [Test]
        public void RepeatedCompositionDoesNotDuplicateSceneObjects()
        {
            EnsureDependencies();
            Transform firstParent = loadedModelParent;
            GameObject firstReference = embeddedReferenceModel;

            EnsureDependencies();

            Assert.That(loadedModelParent, Is.SameAs(firstParent));
            Assert.That(embeddedReferenceModel, Is.SameAs(firstReference));
            Assert.That(root.transform.childCount, Is.EqualTo(2));
            Assert.That(embeddedReferenceModel.transform.childCount, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestroyedUnityReferencesAreRecreatedWithoutReplacingLiveObjects(bool destroyParent)
        {
            EnsureDependencies();
            Transform firstParent = loadedModelParent;
            GameObject firstReference = embeddedReferenceModel;
            Object.DestroyImmediate(destroyParent ? loadedModelParent.gameObject : embeddedReferenceModel);

            EnsureDependencies();

            Assert.That(loadedModelParent != null, Is.True);
            Assert.That(embeddedReferenceModel != null, Is.True);
            Assert.That(root.transform.childCount, Is.EqualTo(2));
            Assert.That(embeddedReferenceModel.transform.childCount, Is.EqualTo(3));
            if (destroyParent)
            {
                Assert.That(embeddedReferenceModel, Is.SameAs(firstReference));
                Assert.That(loadedModelParent, Is.Not.SameAs(firstParent));
            }
            else
            {
                Assert.That(loadedModelParent, Is.SameAs(firstParent));
                Assert.That(embeddedReferenceModel, Is.Not.SameAs(firstReference));
            }
        }

        private void EnsureDependencies() =>
            ViewerReferenceSceneComposition.EnsureDependencies(
                root.transform, ref loadedModelParent, ref embeddedReferenceModel);

        private GameObject CreateAuthored(string name)
        {
            var value = new GameObject(name);
            value.transform.SetParent(root.transform, false);
            value.transform.localPosition = Vector3.one;
            value.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            value.transform.localScale = Vector3.one * 3f;
            return value;
        }

        private static void AssertAuthoredTransform(Transform value, Quaternion rotation)
        {
            Assert.That(value.localPosition, Is.EqualTo(Vector3.one));
            Assert.That(value.localRotation, Is.EqualTo(rotation));
            Assert.That(value.localScale, Is.EqualTo(Vector3.one * 3f));
        }

        private void AssertElement<TCollider>(int index, string id, Vector3 position)
            where TCollider : Collider
        {
            Transform element = embeddedReferenceModel.transform.GetChild(index);
            Assert.That(element.name, Is.EqualTo("Element " + id));
            Assert.That(element.localPosition, Is.EqualTo(position));
            Assert.That(element.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(element.localScale, Is.EqualTo(Vector3.one));
            Assert.That(element.GetComponent<ViewerElement>().ElementId, Is.EqualTo(id));
            Assert.That(element.GetComponent<TCollider>(), Is.Not.Null);
            Assert.That(element.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        }
    }
}
