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
            Assert.That(message, Does.Contain(
                "Sensitive configuration details were omitted."));
        }

        [TestCase("Authorization: Bearer SYNTHETIC_ONLY")]
        [TestCase("access_token=SYNTHETIC_ONLY")]
        [TestCase("refreshToken = 'SYNTHETIC_ONLY more words'")]
        [TestCase("\"access_token\":\"SYNTHETIC_ONLY\"")]
        [TestCase("password:\nSYNTHETIC_ONLY more words")]
        [TestCase("Set-Cookie: session=SYNTHETIC_ONLY")]
        [TestCase("Bearer SYNTHETIC_ONLY")]
        public void CredentialShapedDetailsAreOmittedCompletely(string detail)
        {
            string message = ViewerCompositionDiagnostic.Sanitize(detail);
            Assert.That(message, Is.EqualTo(
                "Sensitive configuration details were omitted."));
            Assert.That(message, Does.Not.Contain("SYNTHETIC_ONLY"));
        }

        [Test]
        public void UrlsIncludingUserInfoAndQueriesAreRedacted()
        {
            string message = ViewerCompositionDiagnostic.Sanitize(
                "Rejected https://user:SYNTHETIC_ONLY@private.invalid/model?access_token=SYNTHETIC_ONLY");
            Assert.That(message, Is.EqualTo("Rejected <redacted-url>"));
        }

        [Test]
        public void LongNonSensitiveDetailsRemainBounded()
        {
            Assert.That(ViewerCompositionDiagnostic.Sanitize(new string('x', 800)).Length,
                Is.LessThanOrEqualTo(601));
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
