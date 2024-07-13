using Newtonsoft.Json.Linq;
using SarsMinimalApi.Context;
using System.Globalization;

namespace SarsMinimalApi.Helpers;

public static class LangHelper
{
	public static async Task<string> ConvertToResponse(MyDbContext myDbContext, HttpRequest request, int endpointID, int rowID)
	{
		try
		{
			request.Headers.TryGetValue("Accept-Language", out var preferredLang);
			string pl = preferredLang.FirstOrDefault();
			if (pl == null)
				pl = "en-US";
			try
			{
				var culture = CultureInfo.GetCultureInfo(pl);
			}
			catch
			{
				pl = "en-US";
			}
			pl += ".json";
			string json = File.ReadAllText("Translations/" + pl);
			JObject jsonObject = JObject.Parse(json);
			string response = (string)jsonObject[endpointID.ToString()][rowID.ToString()];
			return response;
		}
		catch (Exception ex)
		{
			await DbHelper.SaveLog(myDbContext, Program.Builder.Configuration["AppID"], "ConvertToResponse", ex.Message);
			return string.Empty;
		}
	}
}
