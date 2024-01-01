namespace SarsMinimalApi.Models;

public class VerificationModel : BaseModel
{
    public int UserId { get; set; }

    public int Code { get; set; }

    public VerificationModel()
    {
        Code = new Random().Next(1000, 9999);
    }
}
