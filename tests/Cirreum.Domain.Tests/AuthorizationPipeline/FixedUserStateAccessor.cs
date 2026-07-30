namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Security;

internal sealed class FixedUserStateAccessor(IUserState userState) : IUserStateAccessor {

	public ValueTask<IUserState> GetUserState() => new(userState);

}
