using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Pub/sub bus for presentation requests (VFX, SFX, camera).
    /// Supports multiple subscribers and preserves publish order.
    /// </summary>
    public class PresentationRequestBus
    {
        private readonly List<PresentationRequest> _requests = new();

        /// <summary>
        /// Event fired when a presentation request is published.
        /// </summary>
        public event Action<PresentationRequest> OnRequestPublished;

        /// <summary>
        /// All published requests (read-only).
        /// </summary>
        public IReadOnlyList<PresentationRequest> AllRequests => _requests;

        /// <summary>
        /// Publish a presentation request to all subscribers.
        /// </summary>
        public void Publish(PresentationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Assign timestamp
            request.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Track request
            _requests.Add(request);

            // Notify subscribers
            OnRequestPublished?.Invoke(request);
        }

        /// <summary>
        /// Get all requests for a specific correlation ID.
        /// </summary>
        public List<PresentationRequest> GetRequestsByCorrelation(string correlationId)
        {
            return _requests.Where(r => r.CorrelationId == correlationId).ToList();
        }

        /// <summary>
        /// Get all requests of a specific type.
        /// </summary>
        public List<PresentationRequest> GetRequestsByType(PresentationRequestType type)
        {
            return _requests.Where(r => r.Type == type).ToList();
        }

        /// <summary>
        /// Clear all tracked requests.
        /// </summary>
        public void Clear()
        {
            _requests.Clear();
        }
    }
}
