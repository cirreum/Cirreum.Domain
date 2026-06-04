namespace Cirreum.Conductor.Internal;

using Cirreum.Security;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

internal static class OperationContextFactory {

	public static Task<OperationContext<TOperation>> CreateOperationContext<TOperation>(
		this IServiceProvider serviceProvider,
		Activity? activity,
		long startTimestamp,
		TOperation typedOperation,
		string operationTypeName
	) where TOperation : notnull {

		// GetService<T>()! — IUserStateAccessor is registered by Cirreum bootstrap; skip
		// GetRequiredService's null-guard + throw-helper overhead on the hot path.
		var userStateVt = serviceProvider
			.GetService<IUserStateAccessor>()!
			.GetUserState();

		// Hot path: ValueTask completed synchronously (cached user per-request) —
		// skip async state machine entirely (~120B + ~40-60ns saved).
		if (userStateVt.IsCompletedSuccessfully) {
			return Task.FromResult(BuildContext(
				userStateVt.Result, activity, startTimestamp, typedOperation, operationTypeName));
		}

		// Cold path: first call per request, actually async (user enrichment).
		return CreateOperationContextAsync(
			userStateVt, activity, startTimestamp, typedOperation, operationTypeName);
	}

	private static async Task<OperationContext<TOperation>> CreateOperationContextAsync<TOperation>(
		ValueTask<IUserState> userStateVt,
		Activity? activity,
		long startTimestamp,
		TOperation typedOperation,
		string operationTypeName
	) where TOperation : notnull {
		var userState = await userStateVt;
		return BuildContext(userState, activity, startTimestamp, typedOperation, operationTypeName);
	}

	private static OperationContext<TOperation> BuildContext<TOperation>(
		IUserState userState,
		Activity? activity,
		long startTimestamp,
		TOperation typedOperation,
		string operationTypeName
	) where TOperation : notnull {

		var operationId = activity?.SpanId.ToString()
			?? ActivitySpanId.CreateRandom().ToHexString();
		var correlationId = activity?.TraceId.ToString()
			?? ActivityTraceId.CreateRandom().ToHexString();

		return OperationContext<TOperation>.Create(
			userState,
			typedOperation,
			operationTypeName,
			operationId,
			correlationId,
			startTimestamp);
	}

}
