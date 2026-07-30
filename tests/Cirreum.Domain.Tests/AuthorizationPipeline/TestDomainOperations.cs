namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Authorization.Operations;
using Cirreum.Conductor;

// Test operations with their authorizers and handlers (Commands/Queries may include
// their Validator and Authorization classes in the same file).

// Role-gated operation — only AppAdmin may dispatch.
public sealed record AdminOnlyCommand : IAuthorizableOperation<string>;

public sealed class AdminOnlyCommandAuthorizer : AuthorizerBase<AdminOnlyCommand> {
	public AdminOnlyCommandAuthorizer() {
		this.HasRole(ApplicationRoles.AppAdminRole);
	}
}

public sealed class AdminOnlyCommandHandler : IOperationHandler<AdminOnlyCommand, string> {
	public Task<Result<string>> HandleAsync(AdminOnlyCommand operation, CancellationToken cancellationToken)
		=> Task.FromResult(Result<string>.Success("admin-data"));
}

// Grant-gated mutate — the grant gate enforces owner scope and auto-stamps OwnerId.
// Feature resolves to "tests" from this namespace (Cirreum.Domain.Tests.*).
[RequiresGrant("write")]
public sealed record WriteWidgetCommand : IOwnerMutateOperation<string> {
	public string? OwnerId { get; set; }
}

public sealed class WriteWidgetCommandHandler : IOperationHandler<WriteWidgetCommand, string> {
	public Task<Result<string>> HandleAsync(WriteWidgetCommand operation, CancellationToken cancellationToken)
		=> Task.FromResult(Result<string>.Success(operation.OwnerId ?? "owner-was-null"));
}

// Grant-gated lookup — exists to prove GrantedLookupAudit<,> composes for lookups.
[RequiresGrant("read")]
public sealed record ReadWidgetQuery : IOwnerLookupOperation<string> {
	public string? OwnerId { get; set; }
}

public sealed class ReadWidgetQueryHandler : IOperationHandler<ReadWidgetQuery, string> {
	public Task<Result<string>> HandleAsync(ReadWidgetQuery operation, CancellationToken cancellationToken)
		=> Task.FromResult(Result<string>.Success("widget"));
}

// Authorizable operation with NO authorizer, grant surface, constraint, or policy —
// the evaluator must deny it on every dispatch (fail-closed), and the startup
// validator must flag it as dead.
public sealed record NakedOperation : IAuthorizableOperation<string>;

public sealed class NakedOperationHandler : IOperationHandler<NakedOperation, string> {
	public Task<Result<string>> HandleAsync(NakedOperation operation, CancellationToken cancellationToken)
		=> Task.FromResult(Result<string>.Success("should-never-run"));
}
