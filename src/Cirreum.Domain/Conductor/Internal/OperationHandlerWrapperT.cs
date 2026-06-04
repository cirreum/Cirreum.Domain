namespace Cirreum.Conductor.Internal;
/// <summary>
/// Base wrapper class for operations with typed responses.
/// </summary>
/// <typeparam name="TResultValue">The type of response returned by the operation.</typeparam>
internal abstract class OperationHandlerWrapper<TResultValue> {

	/// <summary>
	/// Handles the operation by resolving the handler and building the intercept pipeline.
	/// </summary>
	public abstract Task<Result<TResultValue>> HandleAsync(
		IOperation<TResultValue> request,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken);
}
