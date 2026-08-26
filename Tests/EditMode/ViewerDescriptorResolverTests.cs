using Deucarian.TemplateViewer.Loading;
using NUnit.Framework;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerDescriptorResolverTests
    {
        [Test]
        public void PreservesExplicitVersionAndCacheMetadata()
        {
            var resolver = new DirectViewerModelDescriptorResolver();
            var request = new ViewerInitializeRequest
            {
                Revision = 4,
                ModelUrl = "https://cdn.example.test/model.bundle",
                ModelId = "model-a",
                ModelVersion = "version-7",
                CacheVersion = 12,
                CacheHash = "0123456789abcdef"
            };

            Assert.That(
                resolver.TryResolve(request, out ViewerModelDescriptor value, out string error),
                Is.True,
                error);
            Assert.That(value.ModelVersion, Is.EqualTo("version-7"));
            Assert.That(value.CacheVersion, Is.EqualTo(12));
            Assert.That(value.CacheHash, Is.EqualTo("0123456789abcdef"));
        }

        [TestCase("javascript:alert(1)")]
        [TestCase("file:///secret.bundle")]
        public void RejectsUnsafeAbsoluteSchemes(string source)
        {
            var resolver = new DirectViewerModelDescriptorResolver();
            Assert.That(
                resolver.TryResolve(
                    new ViewerInitializeRequest { Revision = 1, ModelUrl = source },
                    out _,
                    out _),
                Is.False);
        }
    }
}
