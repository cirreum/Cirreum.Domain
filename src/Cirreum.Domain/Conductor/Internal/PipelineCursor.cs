namespace Cirreum.Conductor.Internal;

using Cirreum.Conductor;

/// <summary>
/// Per-request pipeline walker for void/Unit-response requests. One allocation per request,
/// plus one delegate allocation bound to <see cref="Next"/> (reused for every interceptor
/// in the chain). Replaces the recursive lambda-per-level pattern that allocated a fresh
/// closure at each interceptor.
/// </summary>
/// <remarks>
/// <para>
/// Interceptors MUST call <c>next()</c> at most once per <c>HandleAsync</c> invocation —
/// the cursor's <see cref="_index"/> is mutable shared state and calling next twice advances
/// past the intended interceptor. All built-in interceptors comply. Custom interceptors
/// that need retry/loop/fan-out semantics must snapshot state and build their own cursor.
/// </para>
/// <para>
/// Walking is synchronous; only the terminal step needs an async helper to convert the
/// handler's <see cref="Task{Result}"/> into the <see cref="Task{Result}"/> of
/// <see cref="Unit"/> that the interceptor contract expects.
/// </para>
/// </remarks>
/// <typeparam name="TOperation">The concrete request type.</typeparam>
internal sealed class PipelineCursor<TOperation>
	where TOperation : class, IOperation {

	private readonly IIntercept<TOperation, Unit>[] _intercepts;
	private readonly IOperationHandler<TOperation> _handler;
	private int _index;
	public readonly OperationHandlerDelegate<TOperation, Unit> NextDelegate;

	public PipelineCursor(
		IIntercept<TOperation, Unit>[] intercepts,
		IOperationHandler<TOperation> handler) {

		this._intercepts = intercepts;
		this._handler = handler;
		this.NextDelegate = this.Next;
	}

	private Task<Result<Unit>> Next(
		OperationContext<TOperation> context,
		CancellationToken cancellationToken) {

		if (this._index >= this._intercepts.Length) {
			// Terminal: handler returns Task<Result>, interceptors expect Task<Result<Unit>>.
			// Only the terminal step needs the async conversion — walking is sync.
			return TerminateAsync(this._handler.HandleAsync(context.Operation, cancellationToken));
		}
		var current = this._intercepts[this._index++];
		return current.HandleAsync(context, this.NextDelegate, cancellationToken);
	}

	private static async Task<Result<Unit>> TerminateAsync(Task<Result> handlerTask) {
		var result = await handlerTask.ConfigureAwait(false);
		return result; // Implicit conversion from Result to Result<Unit>
	}
}
