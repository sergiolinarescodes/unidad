using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Clears navigation events from the previous frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct NavEventClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavAgent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<NavAgent>>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<PathFound>(entity, false);
                ecb.SetComponentEnabled<PathNotFound>(entity, false);
                ecb.SetComponentEnabled<PathCompleted>(entity, false);
                ecb.SetComponentEnabled<NavNodeReached>(entity, false);
                ecb.SetComponentEnabled<PathInvalidated>(entity, false);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Processes PathRequest components. Runs A* on main thread, throttled by MaxPathsPerFrame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PathRequestSystem : ISystem
    {
        EntityQuery _graphQuery;

        public void OnCreate(ref SystemState state)
        {
            _graphQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NavGraphData>()
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            int maxPaths = 32;
            if (SystemAPI.HasSingleton<PathRequestConfig>())
                maxPaths = SystemAPI.GetSingleton<PathRequestConfig>().MaxPathsPerFrame;

            var graphEntities = _graphQuery.ToEntityArray(Allocator.Temp);
            var graphDatas = _graphQuery.ToComponentDataArray<NavGraphData>(Allocator.Temp);

            int pathsSolved = 0;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, navAgent, pathNodes, pathProgress, entity) in
                SystemAPI.Query<
                    RefRO<PathRequest>,
                    RefRW<NavAgent>,
                    DynamicBuffer<PathNodeElement>,
                    RefRW<PathProgress>>()
                    .WithAll<PathRequest>()
                    .WithEntityAccess())
            {
                if (pathsSolved >= maxPaths)
                    break;

                int graphId = navAgent.ValueRO.GraphId;
                Entity graphEntity = Entity.Null;

                for (int g = 0; g < graphDatas.Length; g++)
                {
                    if (graphDatas[g].GraphId == graphId)
                    {
                        graphEntity = graphEntities[g];
                        break;
                    }
                }

                if (graphEntity == Entity.Null)
                {
                    navAgent.ValueRW.Status = NavAgentStatus.PathFailed;
                    ecb.SetComponentEnabled<PathNotFound>(entity, true);
                    ecb.SetComponentEnabled<PathRequest>(entity, false);
                    pathsSolved++;
                    continue;
                }

                var nodes = SystemAPI.GetBuffer<NavNodeElement>(graphEntity);
                var edges = SystemAPI.GetBuffer<NavEdgeElement>(graphEntity);

                int startNodeId = navAgent.ValueRO.CurrentNodeId;
                int targetNodeId = request.ValueRO.TargetNodeId;

                if (targetNodeId < 0)
                    targetNodeId = NavigationUtility.FindNearestNode(in nodes, request.ValueRO.TargetWorldPosition);

                if (startNodeId < 0)
                {
                    var transform = SystemAPI.GetComponent<LocalTransform>(entity);
                    startNodeId = NavigationUtility.FindNearestNode(in nodes, transform.Position);
                    navAgent.ValueRW.CurrentNodeId = startNodeId;
                }

                var path = new NativeList<int>(64, Allocator.Temp);
                bool found = NavigationUtility.FindPath(
                    in nodes, in edges,
                    startNodeId, targetNodeId,
                    navAgent.ValueRO.CapabilityFlags,
                    ref path);

                pathNodes.Clear();
                if (found)
                {
                    for (int i = 0; i < path.Length; i++)
                    {
                        int nodeIdx = NavigationUtility.FindNodeIndex(in nodes, path[i]);
                        if (nodeIdx >= 0)
                        {
                            pathNodes.Add(new PathNodeElement
                            {
                                NodeId = path[i],
                                WorldPosition = nodes[nodeIdx].WorldPosition
                            });
                        }
                    }

                    navAgent.ValueRW.Status = NavAgentStatus.FollowingPath;
                    pathProgress.ValueRW.CurrentPathIndex = 0;
                    pathProgress.ValueRW.PathLength = pathNodes.Length;
                    ecb.SetComponentEnabled<PathFound>(entity, true);
                }
                else
                {
                    navAgent.ValueRW.Status = NavAgentStatus.PathFailed;
                    pathProgress.ValueRW = default;
                    ecb.SetComponentEnabled<PathNotFound>(entity, true);
                }

                path.Dispose();
                ecb.SetComponentEnabled<PathRequest>(entity, false);
                pathsSolved++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            graphEntities.Dispose();
            graphDatas.Dispose();
        }
    }

    /// <summary>
    /// Moves agents along their computed paths using AgentLocomotion speed.
    /// Uses main-thread foreach with ECB for enableable flag writes.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PathRequestSystem))]
    public partial struct PathFollowSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavAgent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            state.EntityManager.CompleteAllTrackedJobs();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (navAgent, progress, locomotion, transform, pathNodes, entity) in
                SystemAPI.Query<
                    RefRW<NavAgent>,
                    RefRW<PathProgress>,
                    RefRW<AgentLocomotion>,
                    RefRW<LocalTransform>,
                    DynamicBuffer<PathNodeElement>>()
                    .WithEntityAccess())
            {
                if (navAgent.ValueRO.Status != NavAgentStatus.FollowingPath)
                    continue;

                if (progress.ValueRO.CurrentPathIndex >= pathNodes.Length)
                {
                    navAgent.ValueRW.Status = NavAgentStatus.Arrived;
                    locomotion.ValueRW.IsMoving = false;
                    locomotion.ValueRW.DesiredDirection = float3.zero;
                    ecb.SetComponentEnabled<PathCompleted>(entity, true);
                    continue;
                }

                var targetNode = pathNodes[progress.ValueRO.CurrentPathIndex];
                var toTarget = targetNode.WorldPosition - transform.ValueRO.Position;
                float distance = math.length(toTarget);

                if (distance <= locomotion.ValueRO.StoppingDistance)
                {
                    navAgent.ValueRW.CurrentNodeId = targetNode.NodeId;
                    progress.ValueRW.CurrentPathIndex++;
                    ecb.SetComponentEnabled<NavNodeReached>(entity, true);

                    if (progress.ValueRO.CurrentPathIndex >= pathNodes.Length)
                    {
                        navAgent.ValueRW.Status = NavAgentStatus.Arrived;
                        locomotion.ValueRW.IsMoving = false;
                        locomotion.ValueRW.DesiredDirection = float3.zero;
                        ecb.SetComponentEnabled<PathCompleted>(entity, true);
                    }
                    continue;
                }

                var direction = math.normalize(toTarget);
                locomotion.ValueRW.DesiredDirection = direction;
                locomotion.ValueRW.IsMoving = true;

                float step = locomotion.ValueRO.CurrentMoveSpeed * dt;
                step = math.min(step, distance);
                transform.ValueRW.Position += direction * step;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Checks if agents' active paths are invalidated by graph changes.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PathFollowSystem))]
    public partial struct PathInvalidationSystem : ISystem
    {
        EntityQuery _changedGraphQuery;

        public void OnCreate(ref SystemState state)
        {
            _changedGraphQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NavGraphData, NavGraphChanged>()
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_changedGraphQuery.IsEmpty)
                return;

            var changedNodes = new NativeHashSet<int>(64, Allocator.Temp);

            foreach (var changes in
                SystemAPI.Query<DynamicBuffer<NavGraphChangeRecord>>()
                    .WithAll<NavGraphChanged>())
            {
                for (int i = 0; i < changes.Length; i++)
                {
                    if (changes[i].ChangeType == NavGraphChangeType.NodeRemoved ||
                        changes[i].ChangeType == NavGraphChangeType.NodeFlagsChanged)
                    {
                        changedNodes.Add(changes[i].NodeId);
                    }
                }
            }

            if (changedNodes.Count == 0)
            {
                changedNodes.Dispose();
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (navAgent, pathNodes, pathProgress, entity) in
                SystemAPI.Query<
                    RefRW<NavAgent>,
                    DynamicBuffer<PathNodeElement>,
                    RefRW<PathProgress>>()
                    .WithEntityAccess())
            {
                if (navAgent.ValueRO.Status != NavAgentStatus.FollowingPath)
                    continue;

                bool invalidated = false;
                for (int i = pathProgress.ValueRO.CurrentPathIndex; i < pathNodes.Length; i++)
                {
                    if (changedNodes.Contains(pathNodes[i].NodeId))
                    {
                        invalidated = true;
                        break;
                    }
                }

                if (invalidated)
                {
                    navAgent.ValueRW.Status = NavAgentStatus.Idle;
                    ecb.SetComponentEnabled<PathInvalidated>(entity, true);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            changedNodes.Dispose();

            var clearEcb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (changes, entity) in
                SystemAPI.Query<DynamicBuffer<NavGraphChangeRecord>>()
                    .WithAll<NavGraphChanged>()
                    .WithEntityAccess())
            {
                changes.Clear();
                clearEcb.SetComponentEnabled<NavGraphChanged>(entity, false);
            }
            clearEcb.Playback(state.EntityManager);
            clearEcb.Dispose();
        }
    }
}
