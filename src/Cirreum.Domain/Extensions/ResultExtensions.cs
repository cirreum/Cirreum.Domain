namespace Cirreum;

using Cirreum.Exceptions;
using FluentValidation;
using FluentValidation.Results;

/// <summary>
/// Provides extension methods for creating failed results that represent common error conditions, such as not found,
/// already exists, bad request, or validation failures.
/// </summary>
/// <remarks>These extension methods simplify the creation of standardized failed results by encapsulating common
/// exception types and error scenarios. They are intended to promote consistent error handling patterns throughout the
/// application. Each method returns a failed result containing an appropriate exception, making it easier to propagate
/// and handle errors in a uniform way.</remarks>
public static class ResultExtensions {

	extension(Result) {

		// ============================ NULLABLE ================================

		/// <summary>
		/// Creates a <see cref="Result{T}"/> representing a successful result if the specified value is not null, or a
		/// failure result with a <see cref="NotFoundException"/> if it is null.
		/// </summary>
		/// <remarks>Use this method for entity lookups where a null response indicates the entity was not found.
		/// This is typically used when the result will eventually map to an HTTP 404 response.</remarks>
		/// <typeparam name="T">The type of the value to wrap in the result.</typeparam>
		/// <param name="valueOrNull">The value to wrap in the result. If null, the result will indicate a not found error.</param>
		/// <param name="key">The key used to look up the entity, included in the <see cref="NotFoundException"/> if the value is null.</param>
		/// <returns>A <see cref="Result{T}"/> containing the value if it is not null; otherwise, a failure result with a
		/// <see cref="NotFoundException"/> referencing the specified key.</returns>
		public static Result<T> FromLookup<T>(T? valueOrNull, object key) {
			return valueOrNull is not null
				? Result.From(valueOrNull)
				: Result<T>.Fail(new NotFoundException(key));
		}

		/// <summary>
		/// Creates a <see cref="Result{T}"/> representing a successful result if the specified value is not null, or a
		/// failure result with the specified exception if it is null.
		/// </summary>
		/// <remarks>Use this method when you need to convert a nullable value into a result with a custom exception type.
		/// For entity lookups that should return a <see cref="NotFoundException"/>, prefer <see cref="FromLookup{T}"/> instead.</remarks>
		/// <typeparam name="T">The type of the value to wrap in the result.</typeparam>
		/// <param name="valueOrNull">The value to wrap in the result. If null, the result will contain the specified exception.</param>
		/// <param name="exception">The exception to include in the failure result if the value is null.</param>
		/// <returns>A <see cref="Result{T}"/> containing the value if it is not null; otherwise, a failure result with the
		/// specified exception.</returns>
		public static Result<T> FromNullable<T>(T? valueOrNull, Exception exception) {
			return valueOrNull is not null
				? Result.From(valueOrNull)
				: Result<T>.Fail(exception);
		}


		// ============================ FORBIDDEN ================================

		/// <summary>
		/// Creates a failed result indicating the action requested for an entity is forbidden.
		/// </summary>
		/// <typeparam name="T">The type of the expected result value.</typeparam>
		/// <param name="reason">The error message describing the reason the action is forbidden. Cannot be null.</param>
		/// <returns>A failed <see cref="Result{T}"/> representing a forbidden access error.</returns>
		public static Result<T> Forbidden<T>(string reason) {
			return Result<T>.Fail(new ForbiddenAccessException(reason));
		}

		/// <summary>
		/// Creates a failed result indicating the action requested for an entity is forbidden.
		/// </summary>
		/// <param name="reason">The error message describing the reason the action is forbidden. Cannot be null.</param>
		/// <returns>A failed <see cref="Result"/> representing a forbidden access error.</returns>
		public static Result Forbidden(string reason) {
			return Result.Fail(new ForbiddenAccessException(reason));
		}


		// ============================ NOT FOUND ================================

		/// <summary>
		/// Creates a failed result indicating that an entity with the specified key was not found.
		/// </summary>
		/// <typeparam name="T">The type of the expected result value.</typeparam>
		/// <param name="key">The key of the entity that was not found. This value is used to identify the missing entity in the result.</param>
		/// <returns>A failed <see cref="Result{T}"/> representing a not found error for the specified key.</returns>
		public static Result<T> NotFound<T>(object key) {
			return Result<T>.Fail(new NotFoundException(key));
		}

		/// <summary>
		/// Creates a failed result indicating that one or more items with the specified keys were not found.
		/// </summary>
		/// <typeparam name="T">The type of the value that would have been returned if the item was found.</typeparam>
		/// <param name="keys">One or more keys used to identify the items that were not found.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing a <see cref="NotFoundException"/> with the specified keys.</returns>
		public static Result<T> NotFound<T>(object[] keys) {
			return Result<T>.Fail(new NotFoundException(keys));
		}

		/// <summary>
		/// Creates a failed result indicating that an entity with the specified key was not found.
		/// </summary>
		/// <param name="key">The key of the entity that was not found. This value is used to identify the missing entity in the result.</param>
		/// <returns>A failed <see cref="Result"/> representing a not found error for the specified key.</returns>
		public static Result NotFound(object key) {
			return Result.Fail(new NotFoundException(key));
		}

		/// <summary>
		/// Creates a failed result indicating that one or more items with the specified keys were not found.
		/// </summary>
		/// <param name="keys">One or more keys used to identify the items that were not found.</param>
		/// <returns>A failed <see cref="Result"/> containing a <see cref="NotFoundException"/> with the specified keys.</returns>
		public static Result NotFound(object[] keys) {
			return Result.Fail(new NotFoundException(keys));
		}


		// ============================ ALREADY EXISTS ================================

		/// <summary>
		/// Creates a failed result indicating that an entity already exists, using the specified error message.
		/// </summary>
		/// <typeparam name="T">The type of the value associated with the result.</typeparam>
		/// <param name="message">The error message describing the reason the entity is considered to already exist. Cannot be null.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing an <see cref="AlreadyExistsException"/> with the specified message.</returns>
		public static Result<T> AlreadyExist<T>(string message) {
			return Result<T>.Fail(new AlreadyExistsException(message));
		}

		/// <summary>
		/// Creates a failed result indicating that an entity already exists, using the specified error message.
		/// </summary>
		/// <param name="message">The error message describing the reason the entity is considered to already exist.</param>
		/// <returns>A failed <see cref="Result"/> containing an <see cref="AlreadyExistsException"/> with the specified message.</returns>
		public static Result AlreadyExist(string message) {
			return Result.Fail(new AlreadyExistsException(message));
		}


		// ============================ BAD REQUEST ================================

		/// <summary>
		/// Creates a failed result that represents a bad request error with the specified message.
		/// </summary>
		/// <typeparam name="T">The type of the value that would be returned on success.</typeparam>
		/// <param name="message">The error message that describes the reason for the bad request.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing a <see cref="BadRequestException"/> with the specified message.</returns>
		public static Result<T> BadRequest<T>(string message) {
			return Result<T>.Fail(new BadRequestException(message));
		}

		/// <summary>
		/// Creates a failed result that represents a bad request error with the specified message.
		/// </summary>
		/// <param name="message">The error message that describes the reason for the bad request.</param>
		/// <returns>A failed <see cref="Result"/> containing a <see cref="BadRequestException"/> with the specified message.</returns>
		public static Result BadRequest(string message) {
			return Result.Fail(new BadRequestException(message));
		}


		// ============================ CONFLICT ================================

		/// <summary>
		/// Creates a failed result indicating a conflict with the current state of the resource.
		/// </summary>
		/// <typeparam name="T">The type of the value that would be returned on success.</typeparam>
		/// <param name="message">The error message describing the conflict.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing a <see cref="ConflictException"/> with the specified message.</returns>
		public static Result<T> Conflict<T>(string message) {
			return Result<T>.Fail(new ConflictException(message));
		}

		/// <summary>
		/// Creates a failed result indicating a conflict with the current state of the resource.
		/// </summary>
		/// <param name="message">The error message describing the conflict.</param>
		/// <returns>A failed <see cref="Result"/> containing a <see cref="ConflictException"/> with the specified message.</returns>
		public static Result Conflict(string message) {
			return Result.Fail(new ConflictException(message));
		}

		// ============================ NOT VALID ================================

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified message.
		/// </summary>
		/// <typeparam name="T">The type of the value that would be returned on success.</typeparam>
		/// <param name="message">The error message describing the validation failure.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing a <see cref="ValidationException"/> with the specified message.</returns>
		public static Result<T> NotValid<T>(string message) {
			return Result<T>.Fail(new ValidationException(message));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified message.
		/// </summary>
		/// <param name="message">The error message describing the validation failure.</param>
		/// <returns>A failed <see cref="Result"/> containing a <see cref="ValidationException"/> with the specified message.</returns>
		public static Result NotValid(string message) {
			return Result.Fail(new ValidationException(message));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified validation failures.
		/// </summary>
		/// <typeparam name="T">The type of the value that would be returned on success.</typeparam>
		/// <param name="failures">One or more validation failures that describe what failed validation.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing a <see cref="ValidationException"/> with the specified failures.</returns>
		public static Result<T> NotValid<T>(params ReadOnlySpan<ValidationFailure> failures) {
			return Result<T>.Fail(new ValidationException([.. failures]));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified validation failures.
		/// </summary>
		/// <typeparam name="T">The type of the value that would be returned on success.</typeparam>
		/// <param name="failures">The collection of validation failures that describe what failed validation.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing a <see cref="ValidationException"/> with the specified failures.</returns>
		public static Result<T> NotValid<T>(IEnumerable<ValidationFailure> failures) {
			return Result<T>.Fail(new ValidationException(failures));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified validation failures.
		/// </summary>
		/// <param name="failures">One or more validation failures that describe what failed validation.</param>
		/// <returns>A failed <see cref="Result"/> containing a <see cref="ValidationException"/> with the specified failures.</returns>
		public static Result NotValid(params ReadOnlySpan<ValidationFailure> failures) {
			return Result.Fail(new ValidationException([.. failures]));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified validation failures.
		/// </summary>
		/// <param name="failures">The collection of validation failures that describe what failed validation.</param>
		/// <returns>A failed <see cref="Result"/> containing a <see cref="ValidationException"/> with the specified failures.</returns>
		public static Result NotValid(IEnumerable<ValidationFailure> failures) {
			return Result.Fail(new ValidationException(failures));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified message and validation failures.
		/// </summary>
		/// <typeparam name="T">The type of the value that would be returned on success.</typeparam>
		/// <param name="message">The error message describing the validation failure.</param>
		/// <param name="failures">One or more validation failures that describe what failed validation.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing a <see cref="ValidationException"/> with the specified message and failures.</returns>
		public static Result<T> NotValid<T>(string message, params ReadOnlySpan<ValidationFailure> failures) {
			return Result<T>.Fail(new ValidationException(message, [.. failures]));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified message and validation failures.
		/// </summary>
		/// <typeparam name="T">The type of the value that would be returned on success.</typeparam>
		/// <param name="message">The error message describing the validation failure.</param>
		/// <param name="failures">The collection of validation failures that describe what failed validation.</param>
		/// <returns>A failed <see cref="Result{T}"/> containing a <see cref="ValidationException"/> with the specified message and failures.</returns>
		public static Result<T> NotValid<T>(string message, IEnumerable<ValidationFailure> failures) {
			return Result<T>.Fail(new ValidationException(message, failures));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified message and validation failures.
		/// </summary>
		/// <param name="message">The error message describing the validation failure.</param>
		/// <param name="failures">One or more validation failures that describe what failed validation.</param>
		/// <returns>A failed <see cref="Result"/> containing a <see cref="ValidationException"/> with the specified message and failures.</returns>
		public static Result NotValid(string message, params ReadOnlySpan<ValidationFailure> failures) {
			return Result.Fail(new ValidationException(message, [.. failures]));
		}

		/// <summary>
		/// Creates a failed result indicating that validation failed with the specified message and validation failures.
		/// </summary>
		/// <param name="message">The error message describing the validation failure.</param>
		/// <param name="failures">The collection of validation failures that describe what failed validation.</param>
		/// <returns>A failed <see cref="Result"/> containing a <see cref="ValidationException"/> with the specified message and failures.</returns>
		public static Result NotValid(string message, IEnumerable<ValidationFailure> failures) {
			return Result.Fail(new ValidationException(message, failures));
		}

	}

}