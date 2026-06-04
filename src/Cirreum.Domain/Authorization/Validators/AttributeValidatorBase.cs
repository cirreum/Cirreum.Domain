namespace Cirreum.Authorization;

using FluentValidation.Results;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Provides a base implementation for authorization policy validators that operate on
/// <see cref="IAuthorizableObject"/> types decorated with specific attributes.
/// </summary>
/// <typeparam name="TAttribute">The type of attribute that this validator operates on. Must inherit from <see cref="Attribute"/>.</typeparam>
/// <remarks>
/// This abstract class serves as a foundation for creating authorization validators that determine
/// their applicability based on the presence of custom attributes on the authorizable object's type.
/// It provides common functionality for attribute detection while allowing derived classes to
/// implement specific validation logic.
/// </remarks>
public abstract class AttributeValidatorBase<TAttribute>
	: IPolicyValidator where TAttribute : Attribute {

	// Cache attributes per object type to avoid repeated reflection
	private static readonly ConcurrentDictionary<Type, TAttribute?> _attributeCache = new();


	/// <inheritdoc/>
	public abstract string PolicyName { get; }

	/// <inheritdoc/>
	public abstract int Order { get; }

	/// <inheritdoc/>
	public abstract DomainRuntimeType[] SupportedRuntimeTypes { get; }

	/// <inheritdoc/>
	public virtual bool AppliesTo<TAuthorizableObject>(
		TAuthorizableObject authorizableObject,
		DomainRuntimeType runtimeType,
		DateTimeOffset timestamp)
		where TAuthorizableObject : notnull, IAuthorizableObject =>
		GetAttributeCached(authorizableObject.GetType()) != null;

	/// <summary>
	/// Retrieves a custom attribute of the specified type from the provided authorizable object.
	/// </summary>
	/// <remarks>
	/// This method uses reflection to inspect the type of the provided object for the specified custom
	/// attribute. If the attribute is not present, the method returns <see langword="null"/>.
	/// </remarks>
	/// <typeparam name="TAuthorizableObject">The type of the authorizable object from which the attribute is retrieved. Must be a non-nullable type.</typeparam>
	/// <param name="authorizableObject">The authorizable object whose type is inspected for the custom attribute. Cannot be <see langword="null"/>.</param>
	/// <returns>An instance of the specified attribute type if found; otherwise, <see langword="null"/>.</returns>
	protected virtual TAttribute? GetAttribute<TAuthorizableObject>(TAuthorizableObject authorizableObject)
		where TAuthorizableObject : notnull =>
		GetAttributeCached(authorizableObject.GetType());

	/// <inheritdoc/>
	public abstract Task<ValidationResult> ValidateAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		CancellationToken cancellationToken = default)
		where TAuthorizableObject : IAuthorizableObject;

	/// <summary>
	/// Gets the attribute from cache or performs reflection and caches the result.
	/// </summary>
	private static TAttribute? GetAttributeCached(Type objectType) =>
		_attributeCache.GetOrAdd(
			objectType,
			static type => type.GetCustomAttribute<TAttribute>());

}