using System;

namespace tmsserver.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public string? Category { get; set; } // e.g., Racket, Ball, etc.
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Condition { get; set; } // e.g., damaged, cracked, good
    }

    public class InventoryTransaction
    {
        public int Id { get; set; }
        public int InventoryItemId { get; set; }
        public int? IssuedToUserId { get; set; } // null if not issued to a player
        public int QuantityChanged { get; set; } // negative for issue, positive for add
        public string? Comment { get; set; }
        public DateTime Timestamp { get; set; }
        public int? PerformedByAdminId { get; set; } // nullable for player requests
    }
}
