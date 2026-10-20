using System;
using System.Collections.Generic;
using System.Linq;

namespace Unidad.Core.DI
{
    /// <summary>
    /// Small, boring DI container builder that replaces Reflex's ContainerBuilder.
    /// Deliberately mirrors the subset of Reflex's shape that Unidad installers and the
    /// bootstrap actually use — factory singletons keyed by contract, plus concrete-type
    /// registration with constructor injection — so B2/B4 port over without touching call sites.
    ///
    /// Registrations accumulate here; <see cref="Build"/> produces an immutable
    /// <see cref="Container"/>. For one contract the registration with the highest order wins, and among equal orders the
    /// later one (override semantics). A stub registers at <see cref="Order.Stub"/>, so any real registration beats it.
    /// </summary>
    public sealed class ContainerBuilder
    {
        private readonly List<Registration> _registrations = new();

        // --- Factory singletons (the shape every installer uses) ---

        public ContainerBuilder AddSingleton<T>(Func<Container, T> factory, params Type[] contracts)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Add(Contracts(contracts, typeof(T)), Lifetime.Singleton, c => factory(c));
        }

        public ContainerBuilder AddSingleton<T>(Func<Container, T> factory)
            => AddSingleton(factory, typeof(T));

        /// <summary>A factory singleton at <paramref name="order"/>; the highest order wins its contracts, ties go to the later registration.</summary>
        public ContainerBuilder AddSingleton<T>(Func<Container, T> factory, int order, params Type[] contracts)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Add(Contracts(contracts, typeof(T)), Lifetime.Singleton, c => factory(c), order: order);
        }

        /// <summary>A stand-in that any real registration replaces (<see cref="Order.Stub"/>); reported by <see cref="Container.StubbedContracts"/> while it wins.</summary>
        public ContainerBuilder AddStub<T>(Func<Container, T> factory, params Type[] contracts)
            => AddSingleton(factory, Order.Stub, contracts);

        // --- Pre-built instance singletons ---

        public ContainerBuilder AddSingleton(object instance, params Type[] contracts)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            // Caller-owned: the caller built this instance and keeps ownership of its lifetime,
            // so the container must NOT dispose it on Dispose(). Marked here at registration.
            return Add(Contracts(contracts, instance.GetType()), Lifetime.Singleton, _ => instance, callerOwned: true);
        }

        public ContainerBuilder AddSingleton(object instance)
            => AddSingleton(instance, instance.GetType());

        // --- Concrete-type singletons (constructor injection) ---

        public ContainerBuilder AddSingleton(Type concrete, params Type[] contracts)
        {
            if (concrete == null) throw new ArgumentNullException(nameof(concrete));
            return Add(Contracts(contracts, concrete), Lifetime.Singleton, c => c.Construct(concrete));
        }

        public ContainerBuilder AddSingletonType(Type concrete) => AddSingleton(concrete, concrete);

        // --- Transient factory / concrete ---

        public ContainerBuilder AddTransient<T>(Func<Container, T> factory, params Type[] contracts)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return Add(Contracts(contracts, typeof(T)), Lifetime.Transient, c => factory(c));
        }

        public ContainerBuilder AddTransient<T>(Func<Container, T> factory)
            => AddTransient(factory, typeof(T));

        public ContainerBuilder AddTransient(Type concrete, params Type[] contracts)
        {
            if (concrete == null) throw new ArgumentNullException(nameof(concrete));
            return Add(Contracts(contracts, concrete), Lifetime.Transient, c => c.Construct(concrete));
        }

        // --- Multi-binding (one contract, many singletons, read back with Container.ResolveAll) ---

        /// <summary>
        /// Adds one more singleton to the list of <typeparamref name="T"/>; never overrides, never overridden.
        /// <see cref="Container.ResolveAll{T}"/> returns the list sorted by <paramref name="order"/>, ties in registration order.
        /// A multi-binding is not a single binding: <see cref="HasBinding"/> and <c>Resolve&lt;T&gt;</c> do not see it.
        /// </summary>
        public ContainerBuilder AddMany<T>(Func<Container, T> factory, int order = 0)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _registrations.Add(new Registration(new[] { typeof(T) }, Lifetime.Singleton, c => factory(c), multi: true, order: order));
            return this;
        }

        /// <summary>True when at least one <see cref="AddMany{T}"/> registration exists for <typeparamref name="T"/>.</summary>
        public bool HasMany<T>() => _registrations.Any(r => r.Multi && r.Contracts.Contains(typeof(T)));

        public bool HasBinding(Type type) => _registrations.Any(r => !r.Multi && r.Contracts.Contains(type));

        public bool HasBinding<T>() => HasBinding(typeof(T));

        /// <summary>Freeze the registrations into an immutable, resolvable container.</summary>
        public Container Build() => new(_registrations);

        private ContainerBuilder Add(Type[] contracts, Lifetime lifetime, Func<Container, object> factory, bool callerOwned = false,
            int order = Order.Default)
        {
            _registrations.Add(new Registration(contracts, lifetime, factory, callerOwned, order: order));
            return this;
        }

        private static Type[] Contracts(Type[] contracts, Type fallback)
            => contracts == null || contracts.Length == 0 ? new[] { fallback } : contracts;
    }

    internal enum Lifetime
    {
        Singleton,
        Transient
    }

    /// <summary>One registration: which contracts it satisfies, its lifetime, and how to build it.</summary>
    internal sealed class Registration
    {
        public Type[] Contracts { get; }
        public Lifetime Lifetime { get; }
        public Func<Container, object> Factory { get; }

        /// <summary>
        /// True when the caller supplied a pre-built instance (via <c>AddSingleton(object)</c>)
        /// and retains ownership of its lifetime. The container will NOT dispose such instances.
        /// </summary>
        public bool CallerOwned { get; }

        /// <summary>True for an <c>AddMany</c> entry: one of a list, resolved only through <c>ResolveAll</c>.</summary>
        public bool Multi { get; }

        /// <summary>A multi-binding's sort key in <c>ResolveAll</c>; for a single binding, its rank (highest wins).</summary>
        public int Order { get; }

        public Registration(Type[] contracts, Lifetime lifetime, Func<Container, object> factory, bool callerOwned = false,
            bool multi = false, int order = 0)
        {
            Contracts = contracts;
            Lifetime = lifetime;
            Factory = factory;
            CallerOwned = callerOwned;
            Multi = multi;
            Order = order;
        }
    }
}
