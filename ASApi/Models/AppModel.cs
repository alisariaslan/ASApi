namespace SarsMinimalApi.Models;

public class AppModel : BaseModel
{
	public int AndroidVersion { get; set; }
	public string AndroidURI { get; set; }

	public int IOSVersion { get; set; }
	public string IOSURI { get; set; }

	public int WindowsVersion { get; set; }
	public string WindowsURI { get; set; }

	public int MacosVersion { get; set; }
	public string MacosURI { get; set; }



}
