using System.ComponentModel.DataAnnotations;

namespace SarsMinimalApi.Models;

public class UserModel
{
	[Key]
	public int Id { get; set; }

	[Required]
	[StringLength(20)]
	public required string Username { get; set; }

	[Required]
	[StringLength(30)]
	public required string Password { get; set; }

	[StringLength(50)]
	public string? Email { get; set; }
	public bool? IsEmailVerified { get; set; }

	[StringLength(15)]
	public string? Phone { get; set; }
	public bool? IsPhoneVerified { get; set; }

	[Required]
	public DateTime RegisterDate { get; set; }

	[Required]
	public int PermissionLevel { get; set; }

	public UserModel()
	{
		RegisterDate = DateTime.UtcNow;
		PermissionLevel = 1;
	}
}
