using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Fluent builder for creating navigation graph entities.
    /// Primarily for testing and initial setup — real games build graphs dynamically at runtime
    /// via NavigationUtility.AddNode(), AddBidirectionalEdge(), etc.
    /// </summary>
    public struct NavGraphBuilder
    {
        EntityManager _em;
        int _graphId;

        // Grid mode
        bool _isGrid;
        int _gridWidth, _gridHeight;
        float _gridCellSize;
        bool _eightWay;

        // Freeform mode
        List<NavNodeElement> _nodes;
        List<NavEdgeElement> _edges;

        public static NavGraphBuilder Create(EntityManager em)
        {
            return new NavGraphBuilder
            {
                _em = em,
                _nodes = new List<NavNodeElement>(32),
                _edges = new List<NavEdgeElement>(64)
            };
        }

        public NavGraphBuilder WithId(int graphId)
        {
            _graphId = graphId;
            return this;
        }

        public NavGraphBuilder FromGrid(int width, int height, float cellSize, bool eightWay = false)
        {
            _isGrid = true;
            _gridWidth = width;
            _gridHeight = height;
            _gridCellSize = cellSize;
            _eightWay = eightWay;
            return this;
        }

        public NavGraphBuilder AddNode(int nodeId, float3 worldPosition, int flags = 0, float baseCost = 0f)
        {
            _nodes.Add(new NavNodeElement
            {
                NodeId = nodeId,
                WorldPosition = worldPosition,
                Flags = flags,
                BaseCost = baseCost
            });
            return this;
        }

        public NavGraphBuilder AddEdge(int from, int to, float cost, int requiredFlags = 0)
        {
            _edges.Add(new NavEdgeElement
            {
                FromNodeId = from,
                ToNodeId = to,
                Cost = cost,
                RequiredFlags = requiredFlags
            });
            return this;
        }

        public NavGraphBuilder AddBidirectionalEdge(int nodeA, int nodeB, float cost, int requiredFlags = 0)
        {
            _edges.Add(new NavEdgeElement { FromNodeId = nodeA, ToNodeId = nodeB, Cost = cost, RequiredFlags = requiredFlags });
            _edges.Add(new NavEdgeElement { FromNodeId = nodeB, ToNodeId = nodeA, Cost = cost, RequiredFlags = requiredFlags });
            return this;
        }

        public Entity Build()
        {
            var types = new NativeList<ComponentType>(8, Allocator.Temp);
            types.Add(ComponentType.ReadWrite<NavGraphData>());
            types.Add(ComponentType.ReadWrite<NavNodeElement>());
            types.Add(ComponentType.ReadWrite<NavEdgeElement>());
            types.Add(ComponentType.ReadWrite<NavGraphChanged>());
            types.Add(ComponentType.ReadWrite<NavGraphChangeRecord>());

            var archetype = _em.CreateArchetype(types.AsArray());
            var entity = _em.CreateEntity(archetype);
            types.Dispose();

            var nodes = _em.GetBuffer<NavNodeElement>(entity);
            var edges = _em.GetBuffer<NavEdgeElement>(entity);

            if (_isGrid)
            {
                NavigationUtility.BuildGridGraph(_gridWidth, _gridHeight, _gridCellSize,
                    ref nodes, ref edges, _eightWay);

                _em.SetComponentData(entity, new NavGraphData
                {
                    GraphId = _graphId,
                    GraphType = NavGraphType.Grid,
                    NodeCount = _gridWidth * _gridHeight
                });
            }
            else
            {
                for (int i = 0; i < _nodes.Count; i++)
                    nodes.Add(_nodes[i]);
                for (int i = 0; i < _edges.Count; i++)
                    edges.Add(_edges[i]);

                _em.SetComponentData(entity, new NavGraphData
                {
                    GraphId = _graphId,
                    GraphType = NavGraphType.Freeform,
                    NodeCount = _nodes.Count
                });
            }

            return entity;
        }
    }
}
