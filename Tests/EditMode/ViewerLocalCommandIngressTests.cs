using System.Reflection;
using Deucarian.CommandRouting;
using NUnit.Framework;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerLocalCommandIngressTests
    {
        [Test]
        public void BootstrapExposesPackageNeutralSceneCommandPort()
        {
            PropertyInfo property = typeof(ViewerBootstrap).GetProperty(
                nameof(ViewerBootstrap.LocalCommandPort));
            FieldInfo field = typeof(ViewerBootstrap).GetField(
                "localCommandPort",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(CommandRoutePortBehaviour)));
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(CommandRoutePortBehaviour)));
        }
    }
}
