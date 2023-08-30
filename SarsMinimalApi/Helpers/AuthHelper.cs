using Azure.Core;

namespace SarsMinimalApi.Helpers
{
	public static class AuthHelper
	{
		public static bool IsFullyAuthorized(HttpRequest request, WebApplicationBuilder builder)
		{
			request.Headers.TryGetValue("Authorization+", out var authorizationPlus);
			string authPlusToken = authorizationPlus.FirstOrDefault();
			if (authPlusToken is null)
				return false;
			var myAuthPlusToken = builder.Configuration["Jwt:Key"];
			if (myAuthPlusToken == null || !authPlusToken.Equals(myAuthPlusToken))
				return false;
			return true;
		}
	}
}
