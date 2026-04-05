using System.Collections.Generic;

namespace MvcMusic.Models
{
    public class ChatSystemViewModel
    {
        public bool IsAdmin { get; set; }
        public IEnumerable<ChatRoom> InitialRooms { get; set; }
        public Dictionary<int, int> UnreadCounts { get; set; }
        
        // Optional draft data for admin
        public string? DraftCustomerId { get; set; }
        public string? DraftCustomerName { get; set; }
        public string? DraftSubject { get; set; }
        public int? ActiveRoomId { get; set; }
    }
}
