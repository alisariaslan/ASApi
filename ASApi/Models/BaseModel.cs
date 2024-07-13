using System.ComponentModel.DataAnnotations;

namespace SarsMinimalApi.Models;

public class BaseModel
{
    [Key]
    public int Id { get; set; }

    public string Key { get; set; }

    public DateTime CreationDate { get; set; }

    public BaseModel()
    {
        CreationDate = DateTime.Now;
    }
}
