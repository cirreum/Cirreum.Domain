namespace Cirreum.Conductor.Internal;

using Cirreum.Conductor;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// Concrete wrapper implementation for operations with typed responses.
/// </summary>
/// <typeparam name="TOperation">The type of operation being handled.</typeparam>
/// <typeparam name="TResultValue">The type of response returned by the operation.</typeparam>
internal sealed class OperationHandlerWrapperImpl<TOperation, TResultValue>
	: OperationHandlerWrapper<TResultValue>
	where TOperation : class, IOperation<TResultValue> {

	private static readonly string operationTypeName = typeof(TOperation).Name;
	private static readonly string responseTypeName = typeof(TResultValue).Name;

	public override Task<Result<TResultValue>> HandleAsync(
		IOperation<TResultValue> request,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken) {

		// ----- 1. RESOLVE HANDLER -----
		var handler = serviceProvider.GetService<IOperationHandler<TOperation, TResultValue>>();
		if (handler is null) {
			return Task.FromResult(Result<TResultValue>.Fail(new InvalidOperationException(
				$"No handler registered for operation type '{operationTypeName}'")));
		}

		// ----- 2. RESOLVE INTERCEPTS -----
		var intercepts = serviceProvider.GetServices<IIntercept<TOperation, TResultValue>>();

		// ----- 3. FAST PATH: zero intercepts — no telemetry, no context, no cursor, no async -----
		// Returns the handler's Task directly when possible: zero async state machine, zero
		// closure allocation, zero telemetry overhead. Handler exceptions are still caught
		// and converted to Result.Fail to preserve the dispatcher's "no-throw" contract.
		if (intercepts is ICollection<IIntercept<TOperation, TResultValue>> { Count: 0 }) {
			try {
				return handler.HandleAsync(Unsafe.As<TOperation>(request), cancellationToken);
			} catch (Exception ex) when (!ex.IsFatal()) {
				return Task.FromResult(Result<TResultValue>.Fail(ex));
			}
		}

		// ----- 4. PIPELINE PATH: intercepts present — full telemetry + context -----
		return OperationHandlerWrapperImpl<TOperation, TResultValue>.HandleWithPipelineAsync(request, serviceProvider, handler, intercepts, cancellationToken);
	}

	/// <summary>
	/// Pipeline path: intercepts are present (Cirreum ships 4 by default: Validation,
	/// Authorization, HandlerPerformance, QueryCaching). This method carries the full
	/// async state machine, telemetry, context creation, and exception handling — none
	/// of which is paid on the fast (zero-intercept) path above.
	/// </summary>
	private static async Task<Result<TResultValue>> HandleWithPipelineAsync(
		IOperation<TResultValue> request,
		IServiceProvider serviceProvider,
		IOperationHandler<TOperation, TResultValue> handler,
		IEnumerable<IIntercept<TOperation, TResultValue>> intercepts,
		CancellationToken cancellationToken) {

		// ----- 0. START ACTIVITY & TIMING -----
		using var activity = OperationTelemetry.StartActivity(
			operationTypeName,
			hasResponse: true,
			responseTypeName);

		var startTimestamp = Timing.Start();

		try {

			// Materialize array (cast if DI already returned one) and walk
			// the pipeline via a single-alloc cursor.
			var interceptArray = intercepts as IIntercept<TOperation, TResultValue>[]
				?? [.. intercepts];

			var operationContext = await serviceProvider.CreateOperationContext(
				activity,
				startTimestamp,
				Unsafe.As<TOperation>(request),
				operationTypeName);

			var cursor = new PipelineCursor<TOperation, TResultValue>(interceptArray, handler);
			var finalResult = await cursor.NextDelegate(operationContext, cancellationToken);

			// ----- POST-PROCESSING (TELEMETRY) -----
			RecordTelemetry(activity, startTimestamp, finalResult.IsSuccess, finalResult.Error);
			return finalResult;

		} catch (OperationCanceledException oce) {
			RecordTelemetry(activity, startTimestamp, success: false, error: oce, canceled: true);
			throw;

		} catch (Exception fex) when (fex.IsFatal()) {
			throw;

		} catch (Exception ex) {
			var finalResult = Result<TResultValue>.Fail(ex);
			RecordTelemetry(activity, startTimestamp, finalResult.IsSuccess, finalResult.Error);
			return finalResult;
		}
	}

	private static void RecordTelemetry(
		Activity? activity,
		long startTimestamp,
		bool success,
		Exception? error = null,
		bool canceled = false) {

		if (activity is null) {
			return;
		}

		var elapsed = Timing.GetElapsedMilliseconds(startTimestamp);

		if (canceled) {
			OperationTelemetry.SetActivityCanceled(activity, (OperationCanceledException)error!);
			OperationTelemetry.RecordCanceled(operationTypeName, responseTypeName, elapsed, (OperationCanceledException)error!);
		} else if (success) {
			OperationTelemetry.SetActivitySuccess(activity);
			OperationTelemetry.RecordSuccess(operationTypeName, responseTypeName, elapsed);
		} else {
			OperationTelemetry.SetActivityError(activity, error!);
			OperationTelemetry.RecordFailure(operationTypeName, responseTypeName, elapsed, error!);
		}
	}

}
