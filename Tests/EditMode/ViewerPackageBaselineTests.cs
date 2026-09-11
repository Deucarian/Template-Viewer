using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerPackageBaselineTests
    {
        [TestCase("com.deucarian.camera-navigation", "0.3.0")]
        [TestCase("com.deucarian.viewer-navigation", "0.2.0")]
        [TestCase("com.deucarian.theming", "1.7.0")]
        public void TemplateRequiresTheSharedNavigationAndThemeBaseline(string id, string minimum)
        {
            var package = PackageInfo.FindForAssembly(typeof(ViewerBootstrap).Assembly);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(package.resolvedPath, "package.json")));
            var version = new System.Version(manifest["dependencies"][id].Value<string>());
            Assert.That(version, Is.GreaterThanOrEqualTo(new System.Version(minimum)));
        }
    }
}
