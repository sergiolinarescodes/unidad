using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class NavigationUtility
    {
        /// <summary>
        /// A* pathfinding on the node graph. Returns true if a path was found.
        /// Path is filled with node IDs from start to goal (exclusive of start).
        /// All allocations use Allocator.Temp (bump allocator, nearly free).
        /// </summary>
        // FUTURE: For >100 simultaneous path requests, consider:
        // - Pre-allocated per-thread scratch buffers (Allocator.Persistent) indexed by thread ID
        // - IJobParallelFor with NativeQueue<PathRequestData> as input
        // - Hierarchical pathfinding (HPA*) for large graphs: coarse path on cluster graph,
        //   fine path on local subgraph
        // - Flow field for many-agents-same-destination scenarios
        // Current approach: sequential on main thread, MaxPathsPerFrame throttling
        public static bool FindPath(
            in DynamicBuffer<NavNodeElement> nodes,
            in DynamicBuffer<NavEdgeElement> edges,
            int startNodeId, int goalNodeId,
            int agentCapabilityFlags,
            ref NativeList<int> path)
        {
            path.Clear();

            int startIdx = FindNodeIndex(in nodes, startNodeId);
            int goalIdx = FindNodeIndex(in nodes, goalNodeId);
            if (startIdx < 0 || goalIdx < 0)
                return false;

            float3 goalPos = nodes[goalIdx].WorldPosition;
            int nodeCount = nodes.Length;

            var openSet = new NativeList<int>(64, Allocator.Temp);
            var fScores = new NativeArray<float>(nodeCount, Allocator.Temp);
            var gScores = new NativeArray<float>(nodeCount, Allocator.Temp);
            var cameFrom = new NativeArray<int>(nodeCount, Allocator.Temp);
            var inClosedSet = new NativeArray<bool>(nodeCount, Allocator.Temp);

            for (int i = 0; i < nodeCount; i++)
            {
                fScores[i] = float.MaxValue;
                gScores[i] = float.MaxValue;
                cameFrom[i] = -1;
            }

            gScores[startIdx] = 0;
            fScores[startIdx] = Heuristic(nodes[startIdx].WorldPosition, goalPos);
            openSet.Add(startIdx);

            bool found = false;

            while (openSet.Length > 0)
            {
                // Find node with lowest fScore (linear scan — good for typical graph sizes)
                int bestOpen = 0;
                float bestF = fScores[openSet[0]];
                for (int i = 1; i < openSet.Length; i++)
                {
                    if (fScores[openSet[i]] < bestF)
                    {
                        bestF = fScores[openSet[i]];
                        bestOpen = i;
                    }
                }

                int currentIdx = openSet[bestOpen];
                openSet.RemoveAtSwapBack(bestOpen);

                if (currentIdx == goalIdx)
                {
                    found = true;
                    break;
                }

                inClosedSet[currentIdx] = true;
                int currentNodeId = nodes[currentIdx].NodeId;

                for (int e = 0; e < edges.Length; e++)
                {
                    if (edges[e].FromNodeId != currentNodeId)
                        continue;

                    if (!StrategyUtility.CheckPreconditions(edges[e].RequiredFlags, agentCapabilityFlags))
                        continue;

                    int neighborIdx = FindNodeIndex(in nodes, edges[e].ToNodeId);
                    if (neighborIdx < 0 || inClosedSet[neighborIdx])
                        continue;

                    var neighborNode = nodes[neighborIdx];
                    if (neighborNode.Flags != 0 &&
                        !StrategyUtility.CheckPreconditions(neighborNode.Flags, agentCapabilityFlags))
                        continue;

                    float tentativeG = gScores[currentIdx] + edges[e].Cost + neighborNode.BaseCost;

                    if (tentativeG < gScores[neighborIdx])
                    {
                        cameFrom[neighborIdx] = currentIdx;
                        gScores[neighborIdx] = tentativeG;
                        fScores[neighborIdx] = tentativeG + Heuristic(neighborNode.WorldPosition, goalPos);

                        bool inOpen = false;
                        for (int o = 0; o < openSet.Length; o++)
                        {
                            if (openSet[o] == neighborIdx)
                            {
                                inOpen = true;
                                break;
                            }
                        }
                        if (!inOpen)
                            openSet.Add(neighborIdx);
                    }
                }
            }

            if (found)
            {
                var reversePath = new NativeList<int>(32, Allocator.Temp);
                int traceIdx = goalIdx;
                while (traceIdx >= 0)
                {
                    reversePath.Add(nodes[traceIdx].NodeId);
                    traceIdx = cameFrom[traceIdx];
                }

                // Reverse into path, skip start node
                for (int i = reversePath.Length - 2; i >= 0; i--)
                    path.Add(reversePath[i]);

                reversePath.Dispose();
            }

            openSet.Dispose();
            fScores.Dispose();
            gScores.Dispose();
            cameFrom.Dispose();
            inClosedSet.Dispose();

            return found;
        }

        public static float Heuristic(float3 a, float3 b)
        {
            return math.distance(a, b);
        }

        public static int FindNodeIndex(in DynamicBuffer<NavNodeElement> nodes, int nodeId)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NodeId == nodeId)
                    return i;
            }
            return -1;
        }

        public static int FindNearestNode(in DynamicBuffer<NavNodeElement> nodes, float3 worldPos)
        {
            int bestId = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < nodes.Length; i++)
            {
                float dist = math.distancesq(nodes[i].WorldPosition, worldPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = nodes[i].NodeId;
                }
            }
            return bestId;
        }

        // === Dynamic Graph Manipulation ===

        /// <summary>Add a node and return its NodeId.</summary>
        public static int AddNode(ref DynamicBuffer<NavNodeElement> nodes, float3 worldPos, int flags,
            float baseCost = 0f)
        {
            int nodeId = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NodeId >= nodeId)
                    nodeId = nodes[i].NodeId + 1;
            }
            nodes.Add(new NavNodeElement
            {
                NodeId = nodeId,
                WorldPosition = worldPos,
                Flags = flags,
                BaseCost = baseCost
            });
            return nodeId;
        }

        /// <summary>Add a bidirectional edge between two nodes.</summary>
        public static void AddBidirectionalEdge(ref DynamicBuffer<NavEdgeElement> edges,
            int nodeA, int nodeB, float cost, int requiredFlags = 0)
        {
            edges.Add(new NavEdgeElement { FromNodeId = nodeA, ToNodeId = nodeB, Cost = cost, RequiredFlags = requiredFlags });
            edges.Add(new NavEdgeElement { FromNodeId = nodeB, ToNodeId = nodeA, Cost = cost, RequiredFlags = requiredFlags });
        }

        /// <summary>Remove a node and all its edges.</summary>
        public static void RemoveNode(ref DynamicBuffer<NavNodeElement> nodes,
            ref DynamicBuffer<NavEdgeElement> edges, int nodeId)
        {
            for (int i = nodes.Length - 1; i >= 0; i--)
            {
                if (nodes[i].NodeId == nodeId)
                {
                    nodes.RemoveAtSwapBack(i);
                    break;
                }
            }

            for (int i = edges.Length - 1; i >= 0; i--)
            {
                if (edges[i].FromNodeId == nodeId || edges[i].ToNodeId == nodeId)
                    edges.RemoveAtSwapBack(i);
            }
        }

        /// <summary>Set flags on a specific node.</summary>
        public static void SetNodeFlags(ref DynamicBuffer<NavNodeElement> nodes, int nodeId, int flags)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NodeId == nodeId)
                {
                    var node = nodes[i];
                    node.Flags = flags;
                    nodes[i] = node;
                    return;
                }
            }
        }

        /// <summary>Update edge cost between two nodes.</summary>
        public static void SetEdgeCost(ref DynamicBuffer<NavEdgeElement> edges,
            int fromId, int toId, float newCost)
        {
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i].FromNodeId == fromId && edges[i].ToNodeId == toId)
                {
                    var edge = edges[i];
                    edge.Cost = newCost;
                    edges[i] = edge;
                    return;
                }
            }
        }

        /// <summary>
        /// Build a grid-based nav graph using the existing GridUtility.
        /// </summary>
        public static void BuildGridGraph(
            int width, int height, float cellSize,
            ref DynamicBuffer<NavNodeElement> nodes,
            ref DynamicBuffer<NavEdgeElement> edges,
            bool eightWay)
        {
            nodes.Clear();
            edges.Clear();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int nodeId = GridUtility.ToIndex(x, y, width);
                    float3 worldPos = GridUtility.GridToWorld(new int2(x, y), cellSize);
                    nodes.Add(new NavNodeElement
                    {
                        NodeId = nodeId,
                        WorldPosition = worldPos,
                        Flags = 0,
                        BaseCost = 0f
                    });
                }
            }

            var neighbors = new NativeArray<int2>(8, Allocator.Temp);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int fromId = GridUtility.ToIndex(x, y, width);
                    var coord = new int2(x, y);

                    int neighborCount = eightWay
                        ? GridUtility.GetEightWayNeighbors(in coord, width, height, ref neighbors)
                        : GridUtility.GetCardinalNeighbors(in coord, width, height, ref neighbors);

                    for (int n = 0; n < neighborCount; n++)
                    {
                        int toId = GridUtility.ToIndex(neighbors[n].x, neighbors[n].y, width);
                        float3 fromPos = GridUtility.GridToWorld(coord, cellSize);
                        float3 toPos = GridUtility.GridToWorld(neighbors[n], cellSize);

                        edges.Add(new NavEdgeElement
                        {
                            FromNodeId = fromId,
                            ToNodeId = toId,
                            Cost = math.distance(fromPos, toPos),
                            RequiredFlags = 0
                        });
                    }
                }
            }
            neighbors.Dispose();
        }
    }
}
