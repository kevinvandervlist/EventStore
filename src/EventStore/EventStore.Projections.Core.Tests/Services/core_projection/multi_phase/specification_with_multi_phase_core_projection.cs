﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.core_projection.multi_phase
{
    abstract class specification_with_multi_phase_core_projection: TestFixtureWithCoreProjection
    {
        private FakeCheckpointManager _phase1checkpointManager;
        private FakeCheckpointManager _phase2checkpointManager;
        private FakeProjectionProcessingPhase _phase1;
        private FakeProjectionProcessingPhase _phase2;
        private IReaderStrategy _phase1readerStrategy;
        private IReaderStrategy _phase2readerStrategy;

        class FakeProjectionProcessingStrategy : ProjectionProcessingStrategy
        {
            private readonly FakeProjectionProcessingPhase _phase1;
            private readonly FakeProjectionProcessingPhase _phase2;

            public FakeProjectionProcessingStrategy(
                string name, ProjectionVersion projectionVersion, ILogger logger, FakeProjectionProcessingPhase phase1,
                FakeProjectionProcessingPhase phase2)
                : base(name, projectionVersion, logger)
            {
                _phase1 = phase1;
                _phase2 = phase2;
            }

            protected override IQuerySources GetSourceDefinition()
            {
                return new QuerySourcesDefinition()
                {
                    AllStreams = true,
                    AllEvents = true,
                    ByStreams = true,
                    Options = new QuerySourcesDefinitionOptions { }
                };
            }

            public override bool GetStopOnEof()
            {
                return true;
            }

            public override IProjectionProcessingPhase[] CreateProcessingPhases(
                IPublisher publisher, Guid projectionCorrelationId, PartitionStateCache partitionStateCache,
                Action updateStatistics, CoreProjection coreProjection, ProjectionNamesBuilder namingBuilder,
                ITimeProvider timeProvider, IODispatcher ioDispatcher)
            {
                return new[]
                {
                    _phase1,
                    _phase2
                };
            }
        }

        internal class FakeProjectionProcessingPhase : IProjectionProcessingPhase
        {
            private readonly specification_with_multi_phase_core_projection _specification;
            private readonly ICoreProjectionCheckpointManager _checkpointManager;
            private readonly IReaderStrategy _readerStrategy;

            private bool _initialized;
            private bool _initializedFromCheckpoint;
            private CheckpointTag _initializedFromCheckpointAt;
            private PhaseState _state;
            private Guid _subscriptionId;
            private bool _unsubscribed;

            public FakeProjectionProcessingPhase(specification_with_multi_phase_core_projection specification,
                ICoreProjectionCheckpointManager checkpointManager, IReaderStrategy readerStrategy)
            {
                _specification = specification;
                _checkpointManager = checkpointManager;
                _readerStrategy = readerStrategy;
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.ProgressChanged message)
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.EofReached message)
            {
                _specification._coreProjection.CompletePhase();
            }

            public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
            {
                throw new NotImplementedException();
            }

            public void Handle(CoreProjectionManagementMessage.GetState message)
            {
                throw new NotImplementedException();
            }

            public void Handle(CoreProjectionManagementMessage.GetResult message)
            {
                throw new NotImplementedException();
            }

            public void Initialize()
            {
                _initialized = true;
            }

            public void InitializeFromCheckpoint(CheckpointTag checkpointTag)
            {
                _initializedFromCheckpoint = true;
                _initializedFromCheckpointAt = checkpointTag;
            }

            public void Unsubscribed()
            {
                _unsubscribed = true;
            }

            public void SetState(PhaseState state)
            {
                _state = state;
            }

            public void SetFaulted()
            {
                throw new Exception("Faulted");
            }

            public void ProcessEvent()
            {
            }

            public void Subscribed(Guid subscriptionId)
            {
                _subscriptionId = subscriptionId;
            }

            public ReaderSubscriptionOptions GetSubscriptionOptions()
            {
                return new ReaderSubscriptionOptions(10000, 100, true, null);
            }

            public ICoreProjectionCheckpointManager CheckpointManager
            {
                get { return _checkpointManager; }
            }

            public IReaderStrategy ReaderStrategy
            {
                get { return _readerStrategy; }
            }

            public bool Initialized
            {
                get { return _initialized; }
            }

            public bool InitializedFromCheckpoint
            {
                get { return _initializedFromCheckpoint; }
            }

            public CheckpointTag InitializedFromCheckpointAt
            {
                get { return _initializedFromCheckpointAt; }
            }

            public PhaseState State
            {
                get { return _state; }
            }

            public Guid SubscriptionId
            {
                get { return _subscriptionId; }
            }

            public bool Unsubscribed_
            {
                get { return _unsubscribed; }
            }

            public void GetStatistics(ProjectionStatistics info)
            {
            }
        }

        internal class FakeCheckpointManager : ICoreProjectionCheckpointManager
        {
            private readonly IPublisher _publisher;
            private readonly Guid _projectionCorrelationId;

            private bool _started;
            private CheckpointTag _startedAt;
            private CheckpointTag _lastEvent;
            private float _progress;
            private bool _stopped;
            private bool _stopping;

            public FakeCheckpointManager(IPublisher publisher, Guid projectionCorrelationId)
            {
                _publisher = publisher;
                _projectionCorrelationId = projectionCorrelationId;
            }

            public void Initialize()
            {
            }

            public void Start(CheckpointTag checkpointTag)
            {
                _started = true;
                _startedAt = checkpointTag;
                _lastEvent = checkpointTag;
            }

            public void Stopping()
            {
                _stopping = true;
                _publisher.Publish(
                    new CoreProjectionProcessingMessage.CheckpointCompleted(_projectionCorrelationId, _lastEvent));
            }

            public void Stopped()
            {
                _stopped = true;
            }

            public void GetStatistics(ProjectionStatistics info)
            {
            }

            public void NewPartition(string partition, CheckpointTag eventCheckpointTag)
            {
                throw new NotImplementedException();
            }

            public void EventsEmitted(EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId)
            {
                throw new NotImplementedException();
            }

            public void StateUpdated(string partition, PartitionState oldState, PartitionState newState)
            {
                throw new NotImplementedException();
            }

            public void EventProcessed(CheckpointTag checkpointTag, float progress)
            {
                throw new NotImplementedException();
            }

            public bool CheckpointSuggested(CheckpointTag checkpointTag, float progress)
            {
                throw new NotImplementedException();
            }

            public void Progress(float progress)
            {
                _progress = progress;
            }

            public void BeginLoadState()
            {
                _publisher.Publish(
                    new CoreProjectionProcessingMessage.CheckpointLoaded(
                        _projectionCorrelationId, CheckpointTag.FromPosition(0, 0, -1), ""));
            }

            public void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag)
            {
                _publisher.Publish(
                    new CoreProjectionProcessingMessage.PrerecordedEventsLoaded(_projectionCorrelationId, checkpointTag));
            }

            public void BeginLoadPartitionStateAt(string statePartition, CheckpointTag requestedStateCheckpointTag, Action<PartitionState> loadCompleted)
            {
                throw new NotImplementedException();
            }

            public void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action committed)
            {
                throw new NotImplementedException();
            }

            public CheckpointTag LastProcessedEventPosition
            {
                get { throw new NotImplementedException(); }
            }

            public bool Started
            {
                get { return _started; }
            }

            public CheckpointTag StartedAt
            {
                get { return _startedAt; }
            }

            public float Progress_
            {
                get { return _progress; }
            }

            public bool Stopped_
            {
                get { return _stopped; }
            }

            public bool Stopping_
            {
                get { return _stopping; }
            }
        }

        class FakeReaderStrategy : IReaderStrategy
        {
            public bool IsReadingOrderRepeatable
            {
                get { throw new NotImplementedException(); }
            }

            public EventFilter EventFilter
            {
                get { throw new NotImplementedException(); }
            }

            public PositionTagger PositionTagger
            {
                get { throw new NotImplementedException(); }
            }

            public IReaderSubscription CreateReaderSubscription(
                IPublisher publisher, CheckpointTag fromCheckpointTag, Guid subscriptionId,
                ReaderSubscriptionOptions readerSubscriptionOptions)
            {
                throw new NotImplementedException();
            }

            public IEventReader CreatePausedEventReader(
                Guid eventReaderId, IPublisher publisher, CheckpointTag checkpointTag, bool stopOnEof, int? stopAfterNEvents)
            {
                throw new NotImplementedException();
            }
        }

        public FakeCheckpointManager Phase1CheckpointManager
        {
            get { return _phase1checkpointManager; }
        }
        public FakeCheckpointManager Phase2CheckpointManager
        {
            get { return _phase2checkpointManager; }
        }


        public FakeProjectionProcessingPhase Phase1
        {
            get { return _phase1; }
        }

        public FakeProjectionProcessingPhase Phase2
        {
            get { return _phase2; }
        }

        protected override ProjectionProcessingStrategy GivenProjectionProcessingStrategy()
        {
            _phase1checkpointManager = new FakeCheckpointManager(_bus, _projectionCorrelationId);
            _phase2checkpointManager = new FakeCheckpointManager(_bus, _projectionCorrelationId);
            _phase1readerStrategy = new FakeReaderStrategy();
            _phase2readerStrategy = new FakeReaderStrategy();
            _phase1 = new FakeProjectionProcessingPhase(this, Phase1CheckpointManager, _phase1readerStrategy);
            _phase2 = new FakeProjectionProcessingPhase(this, Phase2CheckpointManager, _phase2readerStrategy);
            return new FakeProjectionProcessingStrategy(
                _projectionName, _version, new ConsoleLogger("logger"), Phase1, Phase2);
        }

    }

}