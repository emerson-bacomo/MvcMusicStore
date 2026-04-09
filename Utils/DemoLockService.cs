using System;

namespace MvcMusic.Utils
{
    public class DemoLockService : IDemoLockService
    {
        private string _ownerSessionId;
        private DateTime _lastHeartbeat;
        private readonly int _timeoutSeconds = 105; // 60s interval + 45s grace period
        private readonly object _lock = new object();

        public bool TryClaim(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return false;

            lock (_lock)
            {
                Cleanup();
                if (string.IsNullOrEmpty(_ownerSessionId))
                {
                    _ownerSessionId = sessionId;
                    _lastHeartbeat = DateTime.Now;
                    return true;
                }
                return _ownerSessionId == sessionId;
            }
        }

        public bool IsLockOwner(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return false;

            lock (_lock)
            {
                Cleanup();
                return _ownerSessionId == sessionId;
            }
        }

        public void RefreshHeartbeat(string sessionId)
        {
            lock (_lock)
            {
                if (IsLockOwner(sessionId))
                {
                    // Only update if it's been at least 30s since last heartbeat 
                    // to prevent jumpy countdowns from focus spamming
                    if ((DateTime.Now - _lastHeartbeat).TotalSeconds > 30)
                    {
                        _lastHeartbeat = DateTime.Now;
                    }
                }
            }
        }

        public void ReleaseLock(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            lock (_lock)
            {
                if (_ownerSessionId == sessionId)
                {
                    _ownerSessionId = null;
                }
            }
        }

        public DemoLockInfo GetLockInfo()
        {
            lock (_lock)
            {
                Cleanup();
                var remaining = 0;
                if (!string.IsNullOrEmpty(_ownerSessionId))
                {
                    remaining = (int)(_lastHeartbeat.AddSeconds(_timeoutSeconds) - DateTime.Now).TotalSeconds;
                }

                return new DemoLockInfo
                {
                    IsLocked = !string.IsNullOrEmpty(_ownerSessionId),
                    OwnerSessionId = _ownerSessionId,
                    RemainingSeconds = Math.Max(0, remaining)
                };
            }
        }

        private void Cleanup()
        {
            if (!string.IsNullOrEmpty(_ownerSessionId) && 
                DateTime.Now > _lastHeartbeat.AddSeconds(_timeoutSeconds))
            {
                _ownerSessionId = null;
            }
        }
    }
}
