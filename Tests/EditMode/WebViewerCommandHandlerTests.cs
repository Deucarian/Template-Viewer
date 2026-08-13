using System.Linq;
using Deucarian.TemplateViewerWeb.Commands;
using NUnit.Framework;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerCommandHandlerTests
    {
        [Test]
        public void RegistersOnlyTheDocumentedGenericApplicationCommands()
        {
            string[] names = WebViewerCommandHandlers.Create()
                .SelectMany(handler => handler.CommandNames)
                .OrderBy(value => value)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "clear_selection",
                    "dispose_viewer",
                    "initialize_viewer",
                    "select_elements"
                },
                names);
        }

        [Test]
        public void ContainsNoReportOrActivityCommandNames()
        {
            string[] names = WebViewerCommandHandlers.Create()
                .SelectMany(handler => handler.CommandNames)
                .ToArray();

            Assert.That(names.Any(name => name.Contains("report")), Is.False);
            Assert.That(names.Any(name => name.Contains("activity")), Is.False);
        }
    }
}
