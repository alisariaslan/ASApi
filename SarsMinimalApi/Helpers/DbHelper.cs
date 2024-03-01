using Microsoft.EntityFrameworkCore;
using SarsMinimalApi.Context;
using SarsMinimalApi.Models;

namespace SarsMinimalApi.Helpers;

public class DbHelper
{
	public static async Task<bool> SaveLog(MyDbContext myDbContext, string methodCode, string message)
	{
		try
		{
			Console.WriteLine(DateTime.Now + ":Log:" + methodCode + ":" + message);
			var log = new LogModel() { Method = methodCode, Message = message };
			myDbContext.Logs.Add(log);
			await myDbContext.SaveChangesAsync();
			var expiredlogs = myDbContext.Logs.Where(u => u.CreationDate <= DateTime.Now.AddDays(-7)).ToList();
			myDbContext.Logs.RemoveRange(expiredlogs);
			await myDbContext.SaveChangesAsync();
			return true;
		}
		catch (Exception ex)
		{
			Console.WriteLine("**************************" + DateTime.Now + ":SaveLog ERROR!!!", ex.Message);
			return false;
		}
	}

	public static async Task<UserModel> GetUserFromToken(HttpRequest request, MyDbContext myDbContext)
	{
		try
		{
			request.Headers.TryGetValue("Authorization", out var authorization);
			var tokenmodel = await myDbContext.Tokens.FirstOrDefaultAsync(u => u.Token == authorization.ToString().Replace("Bearer ", string.Empty));
			return await myDbContext.Users.FirstOrDefaultAsync(u => u.Id == tokenmodel.UserId);
		}
		catch (Exception ex)
		{
			await SaveLog(myDbContext, "GetUserFromToken", ex.Message);
			return null;
		}
	}
}
