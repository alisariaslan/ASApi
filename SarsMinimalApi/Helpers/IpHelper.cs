using SarsMinimalApi.Context;

namespace SarsMinimalApi.Helpers;

public class IpHelper
{
	public static async Task<string> GetClientIp(MyDbContext myDbContext, HttpRequest request)
	{
		try
		{
			string ipAddress = string.Empty;
			if (Program.Builder.Configuration["CloudFlare"].Equals("Enabled"))
			{
				if (request.Headers.TryGetValue("CF-Connecting-IP", out var CF_Connecting_IP))
					ipAddress = CF_Connecting_IP.ToString();
			}
			else
				ipAddress = request.HttpContext.Connection.RemoteIpAddress.ToString();
			return ipAddress;
		}
		catch (Exception ex)
		{
			await DbHelper.SaveLog(myDbContext, "GetClientIp", ex.Message);
			return string.Empty;
		}
	}

}
