using System;
using System.ComponentModel.DataAnnotations;

namespace SharpMUD.Data.Models
{
    public class PlayerAccount
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLogin { get; set; }

        // Game State
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string LocationId { get; set; } = "Alpha"; // Sector or Zone
        public bool IsSpace { get; set; } = true;
        
        public int CurrentHealth { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;

        public int Experience { get; set; }
        public int Level { get; set; } = 1;
        public int Money { get; set; } = 0;

        public ICollection<PlayerItem> Items { get; set; } = new List<PlayerItem>();
    }
}
