using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;
using QDND.Combat.Services;

namespace QDND.Tests.Unit
{
    public class PresentationRequestBusTests
    {
        [Fact]
        public void Bus_CanSubscribeToRequests()
        {
            var bus = new PresentationRequestBus();
            var receivedRequests = new List<PresentationRequest>();
            
            bus.OnRequestPublished += req => receivedRequests.Add(req);
            
            var request = new VfxRequest("action_123", "vfx_fireball", new Vector3(1, 2, 3));
            bus.Publish(request);
            
            Assert.Single(receivedRequests);
            Assert.Equal(request, receivedRequests[0]);
        }

        [Fact]
        public void Bus_SupportsMultipleSubscribers()
        {
            var bus = new PresentationRequestBus();
            var subscriber1 = new List<PresentationRequest>();
            var subscriber2 = new List<PresentationRequest>();
            
            bus.OnRequestPublished += req => subscriber1.Add(req);
            bus.OnRequestPublished += req => subscriber2.Add(req);
            
            var request = new SfxRequest("action_456", "sfx_slash");
            bus.Publish(request);
            
            Assert.Single(subscriber1);
            Assert.Single(subscriber2);
            Assert.Equal(request, subscriber1[0]);
            Assert.Equal(request, subscriber2[0]);
        }

        [Fact]
        public void Bus_PreservesPublishOrder()
        {
            var bus = new PresentationRequestBus();
            var receivedRequests = new List<PresentationRequest>();
            
            bus.OnRequestPublished += req => receivedRequests.Add(req);
            
            var req1 = new VfxRequest("action_1", "vfx_1", new Vector3(0, 0, 0));
            var req2 = new SfxRequest("action_1", "sfx_1");
            var req3 = new CameraFocusRequest("action_1", "target_1");
            
            bus.Publish(req1);
            bus.Publish(req2);
            bus.Publish(req3);
            
            Assert.Equal(3, receivedRequests.Count);
            Assert.Equal(req1, receivedRequests[0]);
            Assert.Equal(req2, receivedRequests[1]);
            Assert.Equal(req3, receivedRequests[2]);
        }

        [Fact]
        public void Bus_TracksPublishedRequests()
        {
            var bus = new PresentationRequestBus();
            
            var req1 = new VfxRequest("action_1", "vfx_1", new Vector3(0, 0, 0));
            var req2 = new SfxRequest("action_1", "sfx_1");
            
            bus.Publish(req1);
            bus.Publish(req2);
            
            Assert.Equal(2, bus.AllRequests.Count);
            Assert.Contains(req1, bus.AllRequests);
            Assert.Contains(req2, bus.AllRequests);
        }

        [Fact]
        public void VfxRequest_StoresRequiredData()
        {
            var position = new Vector3(10, 20, 30);
            var request = new VfxRequest("correlation_123", "vfx_explosion", position);
            
            Assert.Equal(PresentationRequestType.VFX, request.Type);
            Assert.Equal("correlation_123", request.CorrelationId);
            Assert.Equal("vfx_explosion", request.EffectId);
            Assert.Equal(position, request.Position);
        }

        [Fact]
        public void SfxRequest_StoresRequiredData()
        {
            var request = new SfxRequest("correlation_456", "sfx_thunder");
            
            Assert.Equal(PresentationRequestType.SFX, request.Type);
            Assert.Equal("correlation_456", request.CorrelationId);
            Assert.Equal("sfx_thunder", request.SoundId);
        }

        [Fact]
        public void CameraFocusRequest_StoresRequiredData()
        {
            var request = new CameraFocusRequest("correlation_789", "target_boss");
            
            Assert.Equal(PresentationRequestType.CameraFocus, request.Type);
            Assert.Equal("correlation_789", request.CorrelationId);
            Assert.Equal("target_boss", request.TargetId);
        }

        [Fact]
        public void CameraReleaseRequest_StoresRequiredData()
        {
            var request = new CameraReleaseRequest("correlation_abc");
            
            Assert.Equal(PresentationRequestType.CameraRelease, request.Type);
            Assert.Equal("correlation_abc", request.CorrelationId);
        }

        [Fact]
        public void Bus_CanFilterRequestsByCorrelationId()
        {
            var bus = new PresentationRequestBus();
            
            bus.Publish(new VfxRequest("action_1", "vfx_1", Vector3.Zero));
            bus.Publish(new SfxRequest("action_1", "sfx_1"));
            bus.Publish(new VfxRequest("action_2", "vfx_2", Vector3.Zero));
            bus.Publish(new SfxRequest("action_2", "sfx_2"));
            
            var action1Requests = bus.GetRequestsByCorrelation("action_1");
            var action2Requests = bus.GetRequestsByCorrelation("action_2");
            
            Assert.Equal(2, action1Requests.Count);
            Assert.Equal(2, action2Requests.Count);
            Assert.All(action1Requests, req => Assert.Equal("action_1", req.CorrelationId));
            Assert.All(action2Requests, req => Assert.Equal("action_2", req.CorrelationId));
        }

        [Fact]
        public void Bus_CanFilterRequestsByType()
        {
            var bus = new PresentationRequestBus();
            
            bus.Publish(new VfxRequest("action_1", "vfx_1", Vector3.Zero));
            bus.Publish(new SfxRequest("action_1", "sfx_1"));
            bus.Publish(new VfxRequest("action_1", "vfx_2", Vector3.Zero));
            bus.Publish(new CameraFocusRequest("action_1", "target_1"));
            
            var vfxRequests = bus.GetRequestsByType(PresentationRequestType.VFX);
            var sfxRequests = bus.GetRequestsByType(PresentationRequestType.SFX);
            var cameraRequests = bus.GetRequestsByType(PresentationRequestType.CameraFocus);
            
            Assert.Equal(2, vfxRequests.Count);
            Assert.Single(sfxRequests);
            Assert.Single(cameraRequests);
        }

        [Fact]
        public void Bus_CanBeCleared()
        {
            var bus = new PresentationRequestBus();
            
            bus.Publish(new VfxRequest("action_1", "vfx_1", Vector3.Zero));
            bus.Publish(new SfxRequest("action_1", "sfx_1"));
            
            Assert.Equal(2, bus.AllRequests.Count);
            
            bus.Clear();
            
            Assert.Empty(bus.AllRequests);
        }

        [Fact]
        public void Bus_AssignsTimestampToRequests()
        {
            var bus = new PresentationRequestBus();
            var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            var request = new VfxRequest("action_1", "vfx_1", Vector3.Zero);
            bus.Publish(request);
            
            var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            Assert.True(request.Timestamp >= before);
            Assert.True(request.Timestamp <= after);
        }

        [Fact]
        public void VfxRequest_CanHaveOptionalTargetId()
        {
            var requestWithTarget = new VfxRequest("action_1", "vfx_buff", Vector3.Zero, "target_123");
            
            Assert.Equal("target_123", requestWithTarget.TargetId);
            
            var requestWithoutTarget = new VfxRequest("action_2", "vfx_aoe", new Vector3(5, 0, 5));
            
            Assert.Null(requestWithoutTarget.TargetId);
        }

        [Fact]
        public void SfxRequest_CanHaveOptionalPosition()
        {
            var position = new Vector3(1, 2, 3);
            var requestWithPosition = new SfxRequest("action_1", "sfx_footstep", position);
            
            Assert.Equal(position, requestWithPosition.Position);
            
            var requestWithoutPosition = new SfxRequest("action_2", "sfx_music");
            
            Assert.Null(requestWithoutPosition.Position);
        }

        [Fact]
        public void CameraFocusRequest_CanHaveOptionalPosition()
        {
            var position = new Vector3(10, 5, 10);
            var requestWithPosition = new CameraFocusRequest("action_1", null, position);
            
            Assert.Equal(position, requestWithPosition.Position);
            Assert.Null(requestWithPosition.TargetId);
            
            var requestWithTarget = new CameraFocusRequest("action_2", "target_1");
            
            Assert.Equal("target_1", requestWithTarget.TargetId);
            Assert.Null(requestWithTarget.Position);
        }
    }
}
