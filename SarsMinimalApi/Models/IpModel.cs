using System.ComponentModel.DataAnnotations;

namespace SarsMinimalApi.Models
{
	public class IpModel
	{
		[Key]
		public int Id { get; set; }

		[Required]
		public required string Ip { get; set; }

        [Required]
        public required string Endpoint { get; set; }

        [Required]
		public DateTime CreationDate { get; set; }

		public IpModel()
		{
			CreationDate = DateTime.UtcNow;
		}
	}
}
