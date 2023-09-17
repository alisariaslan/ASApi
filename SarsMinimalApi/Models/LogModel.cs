using System.ComponentModel.DataAnnotations;

namespace SarsMinimalApi.Models
{
	public class LogModel
	{
		[Key]
		public int Id { get; set; }

        [Required]
        public required string Process { get; set; }

        [Required]
		public required string Message { get; set; }

        [Required]
		public DateTime CreationDate { get; set; }

		public LogModel()
		{
			CreationDate = DateTime.UtcNow;
		}
	}
}
