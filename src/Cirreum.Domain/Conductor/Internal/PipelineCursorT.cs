namespace Cirreum.Conductor.Internal;

using Cirreum.Conductor;

/// <summary>
/// Per-request pipeline walker for typed-response requests. One allocation per request,
/// plus one delegate allocation bound to <see cref="Next"/> (reused for every interceptor
/// in the chain). Replaces the recursive lambda-per-level pattern that allocated a fresh
/// closure at each interceptor.
/// </summary>
/// <remarks>
/// Interceptors MUST call <c>next()</c> at most once per <c>HandleAsync</c> invocation —
/// the cursor's <see cref="_index"/> is mutable shared state and calling next twice advances
/// past the intended interceptor. All built-in interceptors comply. Custom interceptors
/// that need retry/loop/fan-out semantics must snapshot state and build their own cursor.
/// </remarks>
/// <typeparam name="TOperation">The concrete request type.</typeparam>
/// <typeparam name="TResultValue">The typed response.</typeparam>
internal sealed class PipelineCursor<TOperation, TResultValue>
	where TOperation : class, IOperation<TResultValue> {

	private readonly IIntercept<TOperation, TResultValue>[] _intercepts;
	private readonly IOperationHandler<TOperation, TResultValue> _handler;
	private int _index;
	public readonly OperationHandlerDelegate<TOperation, TResultValue> NextDelegate;

	public PipelineCursor(
		IIntercept<TOperation, TResultValue>[] intercepts,
		IOperationHandler<TOperation, TResultValue> handler) {

		this._intercepts = intercepts;
		this._handler = handler;
		this.NextDelegate = this.Next;
	}

	private Task<Result<TResultValue>> Next(
		OperationContext<TOperation> context,
		CancellationToken cancellationToken) {

		if (this._index >= this._intercepts.Length) {
			return this._handler.HandleAsync(context.Operation, cancellationToken);
		}
		var current = this._intercepts[this._index++];
		return current.HandleAsync(context, this.NextDelegate, cancellationToken);
	}
}
