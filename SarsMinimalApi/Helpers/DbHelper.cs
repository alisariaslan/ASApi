using Microsoft.EntityFrameworkCore;
using SarsMinimalApi.Context;
using SarsMinimalApi.Models;

namespace SarsMinimalApi.Helpers
{
	public static class DbHelper
	{

		public static async Task<UserModel> GetUserFromToken(HttpRequest request, MyDbContext dbContext)
		{
			request.Headers.TryGetValue("Authorization", out var authorization);
			var tokenmodel = await dbContext.Tokens.FirstOrDefaultAsync(u => u.Token == authorization.ToString().Replace("Bearer ",string.Empty));
			return await dbContext.Users.FirstOrDefaultAsync(u => u.Id == tokenmodel.UserId);
		}
	}
}
