using Newtonsoft.Json.Linq;
using System.Globalization;

namespace SarsMinimalApi.Helpers
{
	public static class LangHelper
	{
		public static string ConvertToResponse(HttpRequest request, int endpointID,int rowID)
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
	}
}
