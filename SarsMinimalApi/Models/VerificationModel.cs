using System.ComponentModel.DataAnnotations;

namespace SarsMinimalApi.Models
{
	public class VerificationModel
	{
		[Key]
		public int Id { get; set; }

		[Required]
		public int UserId { get; set; }

		[Required]
		public int Code { get; set; }

		[Required]
		public DateTime CreationDate { get; set; }

		public VerificationModel()
		{
			CreationDate = DateTime.UtcNow;
			Code = new Random().Next(1000,9999);
		}
	}
}
