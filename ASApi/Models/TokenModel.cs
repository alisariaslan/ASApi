namespace SarsMinimalApi.Models;

public class TokenModel : BaseModel
{
	public string TokenId { get; set; }

	public int UserId { get; set; }

	public int AppId { get; set; }

	public string Token { get; set; }

}
