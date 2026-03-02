using System;
using System.Collections.Generic;
using Unidad.Core.EventBus;
using Unidad.Core.Testing;
using UnityEngine;

namespace Unidad.Core.Progression.Scenarios
{
    /// <summary>
    /// Visual scenario: tree of cubes — red=Locked, yellow=Available, green=Unlocked.
    /// Auto-unlocks root, then one node/second via MonoBehaviour updater.
    /// </summary>
    internal sealed class ProgressionScenario : DataDrivenScenario
    {
        private static readonly ScenarioParameter NodeCountParam = new(
            "nodeCount", "Node Count", typeof(int), 6, 2, 12);

        private static readonly ScenarioParameter BranchFactorParam = new(
            "branchFactor", "Branch Factor", typeof(int), 2, 1, 3);

        private IEventBus _eventBus;
        private ProgressionService _progressionService;
        private readonly List<IDisposable> _subscriptions = new();
        private ProgressionTreeId _treeId;
        private readonly List<(ProgressionNodeId Id, GameObject Go)> _nodeVisuals = new();
        private int _expectedNodeCount;
        private bool _treeCreated;
        private bool _rootUnlocked;

        private static readonly Color LockedColor = new(0.9f, 0.2f, 0.2f);
        private static readonly Color AvailableColor = new(0.9f, 0.8f, 0.1f);
        private static readonly Color UnlockedColor = new(0.2f, 0.8f, 0.3f);

        public ProgressionScenario() : base(new TestScenarioDefinition(
            "progression-tree",
            "Progression Tree (Visual)",
            "Displays a tree of cubes — red=Locked, yellow=Available, green=Unlocked. " +
            "Auto-unlocks root, then one node/second. Events logged to Console.",
            new[] { NodeCountParam, BranchFactorParam }
        )) { }

        protected override void ExecuteInternal(ScenarioParameterOverrides overrides)
        {
            var nodeCount = Mathf.Clamp(ResolveParam<int>(overrides, "nodeCount"), 2, 12);
            var branchFactor = Mathf.Clamp(ResolveParam<int>(overrides, "branchFactor"), 1, 3);

            _expectedNodeCount = nodeCount;
            _treeCreated = false;
            _rootUnlocked = false;
            _nodeVisuals.Clear();

            _eventBus = new EventBus.EventBus();
            _progressionService = new ProgressionService(_eventBus);

            // Subscribe
            _subscriptions.Add(_eventBus.Subscribe<TreeCreatedEvent>(evt =>
                Debug.Log($"[ProgressionScenario] Tree created: {evt.TreeId}")));
            _subscriptions.Add(_eventBus.Subscribe<NodeUnlockedEvent>(evt =>
            {
                Debug.Log($"[ProgressionScenario] Node unlocked: {evt.NodeId}");
                UpdateNodeColor(evt.NodeId, UnlockedColor);
            }));
            _subscriptions.Add(_eventBus.Subscribe<NodeBecameAvailableEvent>(evt =>
            {
                Debug.Log($"[ProgressionScenario] Node available: {evt.NodeId}");
                UpdateNodeColor(evt.NodeId, AvailableColor);
            }));
            _subscriptions.Add(_eventBus.Subscribe<NodeRelockedEvent>(evt =>
            {
                Debug.Log($"[ProgressionScenario] Node relocked: {evt.NodeId}");
                UpdateNodeColor(evt.NodeId, LockedColor);
            }));

            // Create tree
            _treeId = new ProgressionTreeId("skill-tree");
            _progressionService.CreateTree(_treeId);
            _treeCreated = _progressionService.HasTree(_treeId);

            // Build nodes in a tree structure
            var nodeIds = new List<ProgressionNodeId>();
            for (int i = 0; i < nodeCount; i++)
                nodeIds.Add(new ProgressionNodeId($"node-{i}"));

            // Root node (no prerequisites)
            _progressionService.AddNode(_treeId, new ProgressionNodeDefinition(
                nodeIds[0], "Root", Array.Empty<ProgressionNodeId>(), Array.Empty<ResourceCost>()));

            // Children: each node's parent is (index - 1) / branchFactor
            for (int i = 1; i < nodeCount; i++)
            {
                var parentIndex = (i - 1) / branchFactor;
                var prereqs = new[] { nodeIds[parentIndex] };
                _progressionService.AddNode(_treeId, new ProgressionNodeDefinition(
                    nodeIds[i], $"Node {i}", prereqs, Array.Empty<ResourceCost>()));
            }

            // Unlock root immediately
            _progressionService.TryUnlock(_treeId, nodeIds[0]);
            _rootUnlocked = _progressionService.GetStatus(_treeId, nodeIds[0]) == ProgressionNodeStatus.Unlocked;

            // Build visual tree
            var spacing = 2.5f;
            for (int i = 0; i < nodeCount; i++)
            {
                var depth = 0;
                var idx = i;
                while (idx > 0)
                {
                    idx = (idx - 1) / branchFactor;
                    depth++;
                }

                // Horizontal position: index within current depth level
                var posInLevel = i;
                var levelStart = 0;
                var levelSize = 1;
                for (int d = 0; d < depth; d++)
                {
                    levelStart += levelSize;
                    levelSize *= branchFactor;
                }
                var localIndex = i - levelStart;
                var xOffset = (localIndex - (levelSize - 1) * 0.5f) * spacing;

                var status = _progressionService.GetStatus(_treeId, nodeIds[i]);
                var color = status switch
                {
                    ProgressionNodeStatus.Unlocked => UnlockedColor,
                    ProgressionNodeStatus.Available => AvailableColor,
                    _ => LockedColor
                };

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"[Scenario] {nodeIds[i]}";
                cube.transform.SetParent(SceneRoot.transform);
                cube.transform.localPosition = new Vector3(xOffset, -depth * spacing, 0f);
                SetColor(cube, color);
                _nodeVisuals.Add((nodeIds[i], cube));
            }

            // Add auto-unlock updater
            var availableNodes = _progressionService.GetAvailableNodes(_treeId);
            if (availableNodes.Count > 0)
            {
                var updater = SceneRoot.AddComponent<ProgressionScenarioUpdater>();
                updater.Initialize(_progressionService, _treeId);
            }

            Debug.Log($"[ProgressionScenario] Complete — {nodeCount} nodes, branchFactor={branchFactor}, root unlocked={_rootUnlocked}");
        }

        private void UpdateNodeColor(ProgressionNodeId nodeId, Color color)
        {
            foreach (var (id, go) in _nodeVisuals)
            {
                if (id == nodeId && go != null)
                {
                    SetColor(go, color);
                    break;
                }
            }
        }

        protected override ScenarioVerificationResult VerifyInternal(ScenarioParameterOverrides overrides)
        {
            var checks = new List<ScenarioVerificationResult.CheckResult>
            {
                new("Scene root created", SceneRoot != null,
                    SceneRoot != null ? null : "No scene root"),
                new("Tree created", _treeCreated,
                    _treeCreated ? null : "Tree was not created"),
                new("Root node unlocked", _rootUnlocked,
                    _rootUnlocked ? null : "Root was not unlocked"),
                new($"All {_expectedNodeCount} node visuals spawned",
                    _nodeVisuals.Count == _expectedNodeCount,
                    _nodeVisuals.Count == _expectedNodeCount ? null
                        : $"Expected {_expectedNodeCount}, got {_nodeVisuals.Count}")
            };
            return new ScenarioVerificationResult(checks);
        }

        protected override void OnCleanup()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();
            _nodeVisuals.Clear();

            _eventBus?.ClearAllSubscriptions();
            _eventBus = null;
            _progressionService = null;
        }

        private static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }

    internal sealed class ProgressionScenarioUpdater : MonoBehaviour
    {
        private ProgressionService _service;
        private ProgressionTreeId _treeId;
        private float _timer;

        public void Initialize(ProgressionService service, ProgressionTreeId treeId)
        {
            _service = service;
            _treeId = treeId;
        }

        private void Update()
        {
            if (_service == null) return;

            _timer += Time.deltaTime;
            if (_timer < 1f) return;
            _timer = 0f;

            var available = _service.GetAvailableNodes(_treeId);
            if (available.Count > 0)
            {
                _service.TryUnlock(_treeId, available[0]);
            }
        }
    }
}
