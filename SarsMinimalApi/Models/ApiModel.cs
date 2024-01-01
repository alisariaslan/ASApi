namespace SarsMinimalApi.Models;

public class ApiModel
{
	public bool Success { get; set; } = true;
	private string Error_ { get; set; }
	public string Error { get { return Error_; } set { Error_ = value; Success = false; } }
	public string Message { get; set; }
	public object Data { get; set; }
}
