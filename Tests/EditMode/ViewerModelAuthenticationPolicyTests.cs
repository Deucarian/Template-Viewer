using Deucarian.API.Models;
using Deucarian.ObjectLoading.APIIntegration;
using NUnit.Framework;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerModelAuthenticationPolicyTests
    {
        [Test]
        public void RelativeModelResolvesToProviderOptionalSource()
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/v1");

            ApiObjectLoadingRequestResolution request =
                policy.ResolveProviderOptionalRequest(
                    "models/current.bundle");

            Assert.That(
                request.ResolvedUrl,
                Is.EqualTo(
                    "https://api.example.com/v1/models/current.bundle"));
            Assert.That(
                request.Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Optional));
        }

        [Test]
        public void SameOriginAbsoluteModelUsesOptionalLiveProvider()
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/v1");

            Assert.That(
                policy.ResolveProviderOptionalRequest(
                    "https://api.example.com/models/current.bundle")
                    .Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Optional));
        }

        [Test]
        public void UntrustedCrossOriginModelRemainsAnonymous()
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/v1");

            ApiObjectLoadingRequestResolution request =
                policy.ResolveProviderOptionalRequest(
                    "https://cdn.other.example/model.bundle");

            Assert.That(
                request.Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Disabled));
        }

        [Test]
        public void ExplicitExactOriginAllowsPrivateCdn()
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/v1",
                new[] { "https://cdn.example.com" });

            Assert.That(
                policy.ResolveProviderOptionalRequest(
                    "https://cdn.example.com/model.bundle")
                    .Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Optional));
        }

        [Test]
        public void OriginEntryWithPathIsRejectedAsInvalidConfiguration()
        {
            Assert.Throws<System.ArgumentException>(
                () => new ApiObjectLoadingTrustedOriginPolicy(
                    "https://api.example.com/v1",
                    new[] { "https://cdn.example.com/private" }));
        }
    }
}
