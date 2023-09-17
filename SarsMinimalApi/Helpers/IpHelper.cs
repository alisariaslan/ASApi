using Microsoft.EntityFrameworkCore;
using SarsMinimalApi.Context;
using SarsMinimalApi.Enums;
using SarsMinimalApi.Models;
using System.Diagnostics;

namespace SarsMinimalApi.Helpers;

public class IpHelper
{
    public static string GetClientIp(HttpRequest request)
    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        string ipAddress = request.HttpContext.Connection.RemoteIpAddress.ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        return ipAddress;
    }

    #region EXPERIMENTAL
    private static async Task<bool> IsFirewallRuleExists(string ip, MyDbContext dbContext)
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
        await DbHelper.SaveLog(dbContext, "IsFirewallRuleExists", $"{process.StartInfo.FileName} {process.StartInfo.Arguments}\noutput:{output}\nerror:{string.Empty}");
        return output.Contains("Rule Name:");

    }

    public static async Task<bool> CreateFirewallRule(string ip, MyDbContext dbContext)
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
        await DbHelper.SaveLog(dbContext, "CreateFirewallRule", $"{process.StartInfo.FileName} {process.StartInfo.Arguments}\noutput:{output}\nerror:{string.Empty}");
        if (output.Contains("Ok."))
            return true;
        return false;
    }
    #endregion

    public static async Task<SpamLevel> SpamCheckAsync(HttpRequest request, MyDbContext dbContext, string endpoint, double maxSeconds, int maxRequestCount)
    {
        if (string.IsNullOrEmpty(endpoint))
            endpoint = "Default";
        if (maxSeconds == 0f)
            maxSeconds = 10;
        if (maxRequestCount == 0)
            maxRequestCount = 2;

        var ip = GetClientIp(request);
        if (string.IsNullOrEmpty(ip))
            return SpamLevel.IpError;

        #region Save ip to DB
        var name = "ip_" + ip.Replace("::", "localhost").Replace(':', '_').Replace("ffff", "").Replace('.', '-');
        IpModel model = new IpModel() { Ip = ip, Endpoint = endpoint };
        dbContext.Requests.Add(model);
        await dbContext.SaveChangesAsync();
        #endregion

        #region Clear expired ips
        var expiredIps = await dbContext.Requests.Where(e => e.CreationDate <= DateTime.UtcNow.AddMinutes(-1)).ToListAsync();
        dbContext.Requests.RemoveRange(expiredIps); // Seçilen tokenleri kaldır
        await dbContext.SaveChangesAsync();
        #endregion

        var requestCount = await dbContext.Requests.Where(u => u.Ip == ip && u.Endpoint == endpoint && u.CreationDate >= DateTime.UtcNow.AddSeconds(-maxSeconds)).CountAsync();
        if (requestCount > maxRequestCount * 2)
        {
            #region EXPERIMENTAL
            if (! await IsFirewallRuleExists(ip, dbContext))
                await CreateFirewallRule(ip, dbContext);
            #endregion
            return SpamLevel.HardSpam;
        }
        else if (requestCount > maxRequestCount)
            return SpamLevel.Spam;

        return SpamLevel.Ok;
    }

}
