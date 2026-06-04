namespace Cirreum.Authorization;

/// <summary>
/// Default scoped holder for <see cref="AuthorizationContext"/>. Backed by a single field.
/// </summary>
sealed class DefaultAuthorizationContextAccessor : IAuthorizationContextAccessor {

	private AuthorizationContext? _context;

	public AuthorizationContext? Current => this._context;

	public void Set(AuthorizationContext context) {
		ArgumentNullException.ThrowIfNull(context);
		this._context = context;
	}
}
