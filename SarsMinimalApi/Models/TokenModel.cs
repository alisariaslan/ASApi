using System.ComponentModel.DataAnnotations;

namespace SarsMinimalApi.Models
{
	public class TokenModel
	{
		[Key]
		public int Id { get; set; }

		[Required]
		public required int UserId { get; set; }

		[Required]
		public required string Token { get; set; }

		[Required]
		public DateTime CreationDate { get; set; }

		[Required]
		public required int AppId { get; set; }

		public TokenModel()
		{
			CreationDate = DateTime.UtcNow;
		}
	}
}
