using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Fluent builder for spawning ECS entities with common component sets.
    /// Minimizes structural changes by building an EntityArchetype before creation.
    ///
    /// Usage:
    ///   EntitySpawner.Create(entityManager)
    ///       .WithRenderable(meshId, materialId)
    ///       .WithColor(new float4(1, 0, 0, 1))
    ///       .WithTransform(position, quaternion.identity, 0.5f)
    ///       .WithPhysics(mass: 1f, bounciness: 0.5f)
    ///       .Build();
    /// </summary>
    public struct EntitySpawner
    {
        EntityManager _em;

        bool _hasRenderable;
        int _meshId, _materialId;

        bool _hasColor;
        float4 _color;

        bool _hasTransform;
        float3 _position;
        quaternion _rotation;
        float _scale;

        bool _hasPhysics;
        PhysicsBody _physicsBody;

        bool _hasVelocity;
        float3 _linearVel;
        float3 _angularVel;

        bool _hasHoverable;

        List<ComponentType> _extraTypes;
        List<Action<EntityManager, Entity>> _extraSetters;

        public static EntitySpawner Create(EntityManager em)
        {
            return new EntitySpawner
            {
                _em = em,
                _rotation = quaternion.identity,
                _scale = 1f,
                _color = new float4(1, 1, 1, 1),
                _physicsBody = PhysicsBody.Default
            };
        }

        public EntitySpawner WithRenderable(int meshId, int materialId)
        {
            _hasRenderable = true;
            _meshId = meshId;
            _materialId = materialId;
            return this;
        }

        public EntitySpawner WithColor(float4 color)
        {
            _hasColor = true;
            _color = color;
            return this;
        }

        public EntitySpawner WithTransform(float3 position, quaternion rotation, float scale = 1f)
        {
            _hasTransform = true;
            _position = position;
            _rotation = rotation;
            _scale = scale;
            return this;
        }

        public EntitySpawner AtPosition(float3 position)
        {
            _hasTransform = true;
            _position = position;
            return this;
        }

        public EntitySpawner WithPhysics(
            float mass = 1f, float bounciness = 0.5f,
            float drag = 0.1f, float gravityScale = 1f)
        {
            _hasPhysics = true;
            _physicsBody = new PhysicsBody
            {
                Mass = mass,
                Bounciness = bounciness,
                Drag = drag,
                GravityScale = gravityScale
            };
            return this;
        }

        public EntitySpawner WithVelocity(float3 linear, float3 angular = default)
        {
            _hasVelocity = true;
            _linearVel = linear;
            _angularVel = angular;
            return this;
        }

        public EntitySpawner WithHoverable()
        {
            _hasHoverable = true;
            return this;
        }

        /// <summary>Add a custom IComponentData with a value. No reflection.</summary>
        public EntitySpawner With<T>(T value) where T : unmanaged, IComponentData
        {
            _extraTypes ??= new List<ComponentType>(4);
            _extraSetters ??= new List<Action<EntityManager, Entity>>(4);

            _extraTypes.Add(ComponentType.ReadWrite<T>());
            // Capture the value in a closure — generic type is resolved at call site, no reflection
            _extraSetters.Add((em, entity) => em.SetComponentData(entity, value));
            return this;
        }

        /// <summary>Add a custom tag component (no data).</summary>
        public EntitySpawner WithTag<T>() where T : unmanaged, IComponentData
        {
            _extraTypes ??= new List<ComponentType>(4);
            _extraTypes.Add(ComponentType.ReadWrite<T>());
            return this;
        }

        public Entity Build()
        {
            var types = new NativeList<ComponentType>(16, Allocator.Temp);

            types.Add(ComponentType.ReadWrite<LocalTransform>());
            types.Add(ComponentType.ReadWrite<LocalToWorld>());

            if (_hasRenderable)
                types.Add(ComponentType.ReadWrite<InstanceColor>());

            if (_hasPhysics)
            {
                types.Add(ComponentType.ReadWrite<Velocity>());
                types.Add(ComponentType.ReadWrite<PhysicsBody>());
            }

            if (_hasHoverable)
            {
                types.Add(ComponentType.ReadWrite<BaseColor>());
                types.Add(ComponentType.ReadWrite<Hovered>());
            }

            if (_extraTypes != null)
                foreach (var t in _extraTypes) types.Add(t);

            var archetype = _em.CreateArchetype(types.AsArray());
            var entity = _em.CreateEntity(archetype);
            types.Dispose();

            // Set component data
            var pos = _hasTransform ? _position : float3.zero;
            var rot = _hasTransform ? _rotation : quaternion.identity;
            var scl = _hasTransform ? _scale : 1f;

            _em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(pos, rot, scl));
            _em.SetComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(pos, rot, new float3(scl))
            });

            if (_hasRenderable)
            {
                _em.AddSharedComponentManaged(entity, new InstanceRenderable
                {
                    MeshId = _meshId,
                    MaterialId = _materialId
                });
                _em.SetComponentData(entity, new InstanceColor
                {
                    Value = _hasColor ? _color : new float4(1, 1, 1, 1)
                });
            }

            if (_hasPhysics)
            {
                _em.SetComponentData(entity, _physicsBody);
                _em.SetComponentData(entity, new Velocity
                {
                    Linear = _hasVelocity ? _linearVel : float3.zero,
                    Angular = _hasVelocity ? _angularVel : float3.zero
                });
            }

            if (_hasHoverable)
            {
                _em.SetComponentData(entity, new BaseColor { Value = _hasColor ? _color : new float4(1, 1, 1, 1) });
                _em.SetComponentEnabled<Hovered>(entity, false);
            }

            // Custom components — each setter is a typed closure, no reflection
            if (_extraSetters != null)
                foreach (var setter in _extraSetters) setter(_em, entity);

            return entity;
        }
    }
}
