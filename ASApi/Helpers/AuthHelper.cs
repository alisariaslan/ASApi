using SarsMinimalApi.Context;

namespace SarsMinimalApi.Helpers;

public static class AuthHelper
{
	public async static Task<bool> IsFullyAuthorized(MyDbContext myDbContext, string adminToken)
	{
		try
		{
			if (Program.Builder.Configuration["FullAuthProtection"].Equals("Disabled"))
				return true;
			var realAdminToken = Program.Builder.Configuration["Jwt:AdminToken"];
			if (realAdminToken.Equals(adminToken))
				return true;
			else
				return false;
		}
		catch (Exception ex)
		{
			await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "IsFullyAuthorized", ex.Message);
			return false;
		}
	}
}
