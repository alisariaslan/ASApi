using Microsoft.EntityFrameworkCore;
using SarsMinimalApi.Context;
using SarsMinimalApi.Enums;
using SarsMinimalApi.Models;
using System.Diagnostics;

namespace SarsMinimalApi.Helpers;


public class ProtectionHelper
{

	private static async Task<bool> IsFirewallRuleExists(string ip, MyDbContext myDbContext)
	{
		try
		{
			var name = "ip_" + ip.Replace("::", "localhost").Replace(':', '_').Replace("ffff", "").Replace('.', '-');
			Process process = new Process();
			process.StartInfo.FileName = "netsh";
			process.StartInfo.Arguments = $"advfirewall firewall show rule name=\"{name}\"";
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			string output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			await DbHelper.SaveLog(myDbContext, "IsFirewallRuleExists", $"{process.StartInfo.FileName} {process.StartInfo.Arguments}\noutput:{output}\nerror:{string.Empty}");
			return output.Contains("Rule Name:");
		}
		catch (Exception ex)
		{
			await DbHelper.SaveLog(myDbContext, "IsFirewallRuleExists", ex.Message);
			return false;
		}

	}

	public static async Task<bool> CreateFirewallRule(string ip, MyDbContext myDbContext)
	{
		try
		{
			var name = "ip_" + ip.Replace("::", "localhost").Replace(':', '_').Replace("ffff", "").Replace('.', '-');
			var clearip = ip.Replace("::", "").Replace(":", "").Replace("ffff", "");
			Process process = new Process();
			process.StartInfo.FileName = "netsh";
			process.StartInfo.Arguments = $"advfirewall firewall add rule name=\"{name}\" dir=in action=block remoteip={clearip}";
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			string output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			await DbHelper.SaveLog(myDbContext, "CreateFirewallRule", $"{process.StartInfo.FileName} {process.StartInfo.Arguments}\noutput:{output}\nerror:{string.Empty}");
			if (output.Contains("Ok."))
				return true;
			return false;
		}
		catch (Exception ex)
		{
			await DbHelper.SaveLog(myDbContext, "CreateFirewallRule", ex.Message);
			return false;
		}
	}

	public static async Task<SpamLevel> SpamCheckAsync(HttpRequest request, MyDbContext myDbContext, string endpoint, double maxSeconds, int maxRequestCount)
	{
		try
		{
			if (string.IsNullOrEmpty(endpoint))
				endpoint = "Default";
			if (maxSeconds == 0f)
				maxSeconds = 10;
			if (maxRequestCount == 0)
				maxRequestCount = 2;

			var ip = await IpHelper.GetClientIp(myDbContext, request);
			if (Program.Builder.Configuration["IPLogs"].Equals("Enabled"))
				Console.WriteLine(DateTime.Now + ":IP:" + ip);

			if (Program.Builder.Configuration["SpamChecks"].Equals("Disabled"))
				return SpamLevel.Ok;

			if (string.IsNullOrEmpty(ip))
				return SpamLevel.IpError;

			#region Save ip to DB
			var name = "ip_" + ip.Replace("::", "localhost").Replace(':', '_').Replace("ffff", "").Replace('.', '-');
			IpModel model = new IpModel() { Ip = ip, Endpoint = endpoint };
			myDbContext.Requests.Add(model);
			await myDbContext.SaveChangesAsync();
			#endregion

			#region Clear expired ips
			var expiredIps = await myDbContext.Requests.Where(e => e.CreationDate <= DateTime.Now.AddMinutes(-1)).ToListAsync();
			myDbContext.Requests.RemoveRange(expiredIps); // Seçilen tokenleri kaldır
			await myDbContext.SaveChangesAsync();
			#endregion

			var requestCount = await myDbContext.Requests.Where(u => u.Ip == ip && u.Endpoint == endpoint && u.CreationDate >= DateTime.Now.AddSeconds(-maxSeconds)).CountAsync();
			if (requestCount > maxRequestCount * 2)
			{
				if (Program.Builder.Configuration["FireWall"].Equals("Enabled"))
				{
					if (!await IsFirewallRuleExists(ip, myDbContext))
						await CreateFirewallRule(ip, myDbContext);
				}
				return SpamLevel.HardSpam;
			}
			else if (requestCount > maxRequestCount)
				return SpamLevel.Spam;

			return SpamLevel.Ok;
		}
		catch (Exception ex)
		{
			await DbHelper.SaveLog(myDbContext, "SpamCheckAsync", ex.Message);
			return SpamLevel.Spam;
		}
	}
}
