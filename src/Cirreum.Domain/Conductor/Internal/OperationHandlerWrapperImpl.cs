namespace Cirreum.Conductor.Internal;

using Cirreum.Conductor;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// Concrete wrapper implementation for operations without typed responses.
/// </summary>
/// <typeparam name="TOperation">The type of operation being handled.</typeparam>
internal sealed class OperationHandlerWrapperImpl<TOperation>
	: OperationHandlerWrapper
	where TOperation : class, IOperation {

	private static readonly string operationTypeName = typeof(TOperation).Name;

	public override Task<Result> HandleAsync(
		IOperation request,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken) {

		// ----- 1. RESOLVE HANDLER -----
		var handler = serviceProvider.GetService<IOperationHandler<TOperation>>();
		if (handler is null) {
			return Task.FromResult(Result.Fail(new InvalidOperationException(
				$"No handler registered for operation type '{operationTypeName}'")));
		}

		// ----- 2. RESOLVE INTERCEPTS -----
		var intercepts = serviceProvider.GetServices<IIntercept<TOperation, Unit>>();

		// ----- 3. FAST PATH: zero intercepts — no telemetry, no context, no cursor, no async -----
		if (intercepts is ICollection<IIntercept<TOperation, Unit>> { Count: 0 }) {
			try {
				return handler.HandleAsync(Unsafe.As<TOperation>(request), cancellationToken);
			} catch (Exception ex) when (!ex.IsFatal()) {
				return Task.FromResult(Result.Fail(ex));
			}
		}

		// ----- 4. PIPELINE PATH: intercepts present — full telemetry + context -----
		return OperationHandlerWrapperImpl<TOperation>.HandleWithPipelineAsync(request, serviceProvider, handler, intercepts, cancellationToken);
	}

	/// <summary>
	/// Pipeline path: intercepts are present. This method carries the full async state machine,
	/// telemetry, context creation, and exception handling — none of which is paid on the fast
	/// (zero-intercept) path above.
	/// </summary>
	private static async Task<Result> HandleWithPipelineAsync(
		IOperation request,
		IServiceProvider serviceProvider,
		IOperationHandler<TOperation> handler,
		IEnumerable<IIntercept<TOperation, Unit>> intercepts,
		CancellationToken cancellationToken) {

		// ----- 0. START ACTIVITY & TIMING -----
		using var activity = OperationTelemetry.StartActivity(
			operationTypeName,
			hasResponse: false);

		var startTimestamp = Timing.Start();

		try {

			// Materialize array (cast if DI already returned one) and walk
			// the pipeline via a single-alloc cursor.
			var interceptArray = intercepts as IIntercept<TOperation, Unit>[]
				?? [.. intercepts];

			var operationContext = await serviceProvider.CreateOperationContext(
				activity,
				startTimestamp,
				Unsafe.As<TOperation>(request),
				operationTypeName);

			var cursor = new PipelineCursor<TOperation>(interceptArray, handler);
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
			var finalResult = Result.Fail(ex);
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
			OperationTelemetry.RecordCanceled(operationTypeName, null, elapsed, (OperationCanceledException)error!);
		} else if (success) {
			OperationTelemetry.SetActivitySuccess(activity);
			OperationTelemetry.RecordSuccess(operationTypeName, null, elapsed);
		} else {
			OperationTelemetry.SetActivityError(activity, error!);
			OperationTelemetry.RecordFailure(operationTypeName, null, elapsed, error!);
		}
	}

}
