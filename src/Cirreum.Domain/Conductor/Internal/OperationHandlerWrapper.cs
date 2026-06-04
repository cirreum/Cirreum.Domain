namespace Cirreum.Conductor.Internal;
/// <summary>
/// Base wrapper class for operations without typed responses.
/// </summary>
internal abstract class OperationHandlerWrapper {
	/// <summary>
	/// Handles the operation by resolving the handler and building the intercept pipeline.
	/// </summary>
	public abstract Task<Result> HandleAsync(
		IOperation request,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken);
}
