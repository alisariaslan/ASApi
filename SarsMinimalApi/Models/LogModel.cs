namespace SarsMinimalApi.Models;

public class LogModel : BaseModel
{
    public string Method { get; set; }

    public string Message { get; set; }

	public LogModel(string method, string message)
	{
		this.Method = method;
		this.Message = message;
	}

	public LogModel() {
    
    }

}
