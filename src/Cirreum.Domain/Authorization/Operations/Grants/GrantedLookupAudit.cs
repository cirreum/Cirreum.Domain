namespace Cirreum.Authorization.Operations.Grants;

using Cirreum.Conductor;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

/// <summary>
/// Pattern C audit intercept. When an <see cref="IGrantableLookupBase"/> operation is
/// invoked with a <see langword="null"/> <c>OwnerId</c>, Stage 1 stashes the resolved
/// grant on <see cref="IOperationGrantAccessor"/> and passes — the framework expects the
/// handler to check the loaded entity's owner against the grant after fetch
/// (existence-hiding pattern). If the handler completes without ever reading
/// <see cref="IOperationGrantAccessor.Current"/>, that is a real authorization bypass:
/// the request was permitted at the gate, the handler returned data, and no ownership
/// check ever ran.
/// </summary>
/// <remarks>
/// <para>
/// This intercept emits a <b>warning log</b> and an OTel activity tag
/// (<see cref="AuthorizationTelemetry.PatternCBypassTag"/>) when this case is detected.
/// It does not deny — by the time the audit runs, the handler has already returned a
/// result. The audit gives operators a runtime signal so they can detect missing checks
/// in test environments, dashboards, and SIEM pipelines before a real bypass ships.
/// </para>
/// <para>
/// To eliminate the warning, the handler must read <see cref="IOperationGrantAccessor.Current"/>
/// and verify <c>grant.Contains(entity.OwnerId)</c> before returning the entity. If the
/// caller doesn't have access, return 404 (not 403) to preserve existence hiding.
/// </para>
/// </remarks>
sealed class GrantedLookupAudit<TOperation, TResultValue>(
	IOperationGrantAccessor grantAccessor,
	ILogger<GrantedLookupAudit<TOperation, TResultValue>> logger
) : IIntercept<TOperation, TResultValue>
	where TOperation : IGrantableLookupBase {

	public async Task<Result<TResultValue>> HandleAsync(
		OperationContext<TOperation> context,
		OperationHandlerDelegate<TOperation, TResultValue> next,
		CancellationToken cancellationToken) {

		// Pattern C entry condition captured BEFORE the handler runs:
		// lookup invoked without an OwnerId → reach is stashed on the accessor and the
		// handler is expected to enforce ownership post-fetch.
		var isPatternC = context.Operation.OwnerId is null;

		var result = await next(context, cancellationToken).ConfigureAwait(false);

		// If the handler completed a Pattern C lookup without ever reading the accessor,
		// no ownership check ran. Surface this so it's visible in traces and logs.
		if (isPatternC && !grantAccessor.WasRead) {
			GrantedLookupAuditLogging.PatternCBypassDetected(logger, typeof(TOperation).Name);
			Activity.Current?.SetTag(AuthorizationTelemetry.PatternCBypassTag, true);
		}

		return result;
	}
}

internal static partial class GrantedLookupAuditLogging {

	[LoggerMessage(
		EventId = 9001,
		Level = LogLevel.Warning,
		Message = "Pattern C bypass detected: {OperationType} completed a null-OwnerId lookup without reading IOperationGrantAccessor.Current. The handler did not perform a post-fetch ownership check. Verify the handler enforces grant.Contains(entity.OwnerId) before returning data.")]
	public static partial void PatternCBypassDetected(ILogger logger, string operationType);
}
