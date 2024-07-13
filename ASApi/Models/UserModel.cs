using System.ComponentModel.DataAnnotations;

namespace SarsMinimalApi.Models;

public class UserModel : BaseModel
{

    [StringLength(20)]
    public string Username { get; set; }

    [StringLength(30)]
    public string Password { get; set; }

    [StringLength(50)]
    public string Email { get; set; }

    public bool IsEmailVerified { get; set; }

    [StringLength(15)]
    public string Phone { get; set; }

    public bool IsPhoneVerified { get; set; }

    public int PermissionLevel { get; set; }

    public UserModel()
    {
        PermissionLevel = 1;
    }
}
