namespace SarsMinimalApi.Models;

public class TokenModel : BaseModel
{
    public int UserId { get; set; }

    public string Token { get; set; }

    public int AppId { get; set; }

}
