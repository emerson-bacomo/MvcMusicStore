using System;

namespace MvcMusic.Utils
{
    public interface IDemoLockService
    {
        bool TryClaim(string sessionId);
        bool IsLockOwner(string sessionId);
        void RefreshHeartbeat(string sessionId);
        void ReleaseLock(string sessionId);
        DemoLockInfo GetLockInfo();
    }

    public class DemoLockInfo
    {
        public bool IsLocked { get; set; }
        public string OwnerSessionId { get; set; }
        public DateTime? LastHeartbeat { get; set; }
        public int RemainingSeconds { get; set; }
    }
}
