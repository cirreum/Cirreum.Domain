namespace Cirreum.Conductor.Intercepts;

using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

sealed class HandlerPerformance<TOperation, TResultValue>(
	ILogger<HandlerPerformance<TOperation, TResultValue>> logger
) : IIntercept<TOperation, TResultValue>
	where TOperation : notnull {

	private const int LongRunningThresholdMs = 500;

	public async Task<Result<TResultValue>> HandleAsync(
		OperationContext<TOperation> context,
		OperationHandlerDelegate<TOperation, TResultValue> next,
		CancellationToken cancellationToken) {

		var startTime = Timing.Start();
		try {
			return await next(context, cancellationToken).ConfigureAwait(false);
		} finally {
			var elapsedMs = (long)Math.Round(Timing.GetElapsedMilliseconds(startTime));
			if (elapsedMs > LongRunningThresholdMs) {
				logger.LogLongRunningOperation(context.OperationType, elapsedMs);
			}
		}

	}

}