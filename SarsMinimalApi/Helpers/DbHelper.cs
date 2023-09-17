using Microsoft.EntityFrameworkCore;
using SarsMinimalApi.Context;
using SarsMinimalApi.Models;

namespace SarsMinimalApi.Helpers
{
	public  class DbHelper
	{
        public static async Task<bool> SaveLog(MyDbContext dbContext,string processName, string message )
        {
            var log = new LogModel() { Process = processName, Message = message };
            dbContext.Logs.Add(log);
            await dbContext.SaveChangesAsync();
            var expiredlogs = dbContext.Logs.Where( u => u.CreationDate <= DateTime.UtcNow.AddDays(-7) ).ToList();
            dbContext.Logs.RemoveRange(expiredlogs);
            await dbContext.SaveChangesAsync();
            return true;
        }

        public static async Task<UserModel> GetUserFromToken(HttpRequest request, MyDbContext dbContext)
		{
			request.Headers.TryGetValue("Authorization", out var authorization);
			var tokenmodel = await dbContext.Tokens.FirstOrDefaultAsync(u => u.Token == authorization.ToString().Replace("Bearer ",string.Empty));
			return await dbContext.Users.FirstOrDefaultAsync(u => u.Id == tokenmodel.UserId);
		}
	}
}
