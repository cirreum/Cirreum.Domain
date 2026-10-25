namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Authorization.Operations.Grants.Caching;

public class OperationGrantCacheKeysTests {

	private static PermissionSet Set(params string[] operations) =>
		new([.. operations.Select(o => new Permission("issues", o))]);

	[Fact]
	public void Signature_IgnoresDeclarationOrder() {
		OperationGrantCacheKeys.SignatureOf(Set("write", "delete"))
			.Should().Be(OperationGrantCacheKeys.SignatureOf(Set("delete", "write")))
			.And.Be("delete+write");
	}

	[Fact]
	public void Signature_OfOnePermission_IsTheOperation() {
		OperationGrantCacheKeys.SignatureOf(Set("delete")).Should().Be("delete");
	}

	[Fact]
	public void Signature_OfNoPermissions_IsEmpty() {
		OperationGrantCacheKeys.SignatureOf(PermissionSet.Empty).Should().BeEmpty();
	}

	[Fact]
	public void Key_EncodesVersionCallerFeatureAndSignature() {
		OperationGrantCacheKeys.BuildKey(2, "user-123", "issues", Set("write", "delete"))
			.Should().Be("grant:v2:user-123:issues:delete+write");
	}

	[Fact]
	public void Tags_AreTheCallerAndFeatureInvalidationTags() {
		OperationGrantCacheKeys.BuildTags("user-123", "issues")
			.Should().Equal("grant:caller:user-123", "grant:feature:issues");
	}

}
