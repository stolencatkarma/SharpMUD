using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharpMUD.Data.Models
{
    public class PlayerItem
    {
        [Key]
        public int Id { get; set; }

        public int PlayerAccountId { get; set; }
        [ForeignKey("PlayerAccountId")]
        public PlayerAccount PlayerAccount { get; set; }

        public string Name { get; set; }
        public int Value { get; set; }
        public float Weight { get; set; }
    }
}
