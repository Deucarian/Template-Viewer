using System;
using Deucarian.TemplateViewer.Diagnostics;
using NUnit.Framework;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerCompositionDiagnosticTests
    {
        [Test]
        public void FormatIncludesStageCauseAndRepairInstruction()
        {
            string message = ViewerCompositionDiagnostic.Format(
                "attaching product feature ExampleFeature",
                new ArgumentException("A required profile is empty."));

            Assert.That(message, Does.Contain(
                "attaching product feature ExampleFeature"));
            Assert.That(message, Does.Contain("ArgumentException"));
            Assert.That(message, Does.Contain(
                "A required profile is empty."));
            Assert.That(message, Does.Contain("enter Play Mode again"));
        }

        [Test]
        public void FormatRedactsUrlsAndCredentialAssignments()
        {
            string message = ViewerCompositionDiagnostic.Format(
                "creating the model loader",
                new InvalidOperationException(
                    "Rejected https://private.example/model?token=abc " +
                    "because token=secret-value."));

            Assert.That(message, Does.Not.Contain("private.example"));
            Assert.That(message, Does.Not.Contain("secret-value"));
            Assert.That(message, Does.Contain("<redacted-url>"));
            Assert.That(message, Does.Contain("token=<redacted>"));
        }

        [Test]
        public void FormatHandlesMissingDetails()
        {
            string message = ViewerCompositionDiagnostic.Format(
                null,
                new Exception(string.Empty));

            Assert.That(message, Does.Contain("starting the viewer"));
            Assert.That(message, Does.Contain(
                "No additional configuration details were provided."));
        }
    }
}
