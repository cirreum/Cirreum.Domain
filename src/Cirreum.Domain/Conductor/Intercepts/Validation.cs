namespace Cirreum.Conductor.Intercepts;

using FluentValidation;
using FluentValidation.Results;
using System.Collections.Generic;
using System.Threading;

sealed class Validation<TOperation, TResultValue>(
	IEnumerable<IValidator<TOperation>> validators
) : IIntercept<TOperation, TResultValue>
	where TOperation : notnull {

	private readonly IReadOnlyList<IValidator<TOperation>> _validators =
		validators as IReadOnlyList<IValidator<TOperation>>
		?? [.. validators];

	public Task<Result<TResultValue>> HandleAsync(
		OperationContext<TOperation> context,
		OperationHandlerDelegate<TOperation, TResultValue> next,
		CancellationToken cancellationToken) {

		if (this._validators.Count == 0) {
			return next(context, cancellationToken);
		}

		return this.HandleCoreAsync(context, next, cancellationToken);

	}

	private async Task<Result<TResultValue>> HandleCoreAsync(
		OperationContext<TOperation> context,
		OperationHandlerDelegate<TOperation, TResultValue> next,
		CancellationToken cancellationToken) {

		var validationContext = new ValidationContext<TOperation>(context.Operation);

		List<ValidationFailure>? failures = null;

		foreach (var validator in this._validators) {
			var result = await validator.ValidateAsync(validationContext, cancellationToken)
				.ConfigureAwait(false);

			if (result.Errors.Count > 0) {
				(failures ??= []).AddRange(result.Errors);
			}
		}

		if (failures is { Count: > 0 }) {
			return Result<TResultValue>.Fail(new ValidationException(failures));
		}

		return await next(context, cancellationToken).ConfigureAwait(false);

	}

}