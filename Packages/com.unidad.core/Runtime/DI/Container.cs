using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unidad.Core.Registry;

namespace Unidad.Core.DI
{
    /// <summary>
    /// Immutable resolved container. Built from a <see cref="ContainerBuilder"/>. Resolves
    /// services by contract, caches singletons, does greedy constructor injection for
    /// concrete-type registrations, and detects circular dependencies with a clear error.
    /// Single-threaded resolution (the bootstrap resolves on the main thread); boring on purpose.
    /// </summary>
    public sealed class Container : IDisposable
    {
        private readonly Dictionary<Type, Registration> _byContract = new();
        private readonly Dictionary<Type, List<Registration>> _many = new();
        private readonly Dictionary<Registration, object> _singletons = new();
        private readonly List<Registration> _registrations;
        // Container-owned IDisposable singletons in the order they were CREATED (resolution
        // order). Disposed in REVERSE so dependents are torn down before their dependencies,
        // matching Reflex/MS.DI. Caller-owned pre-built instances are excluded (see Registration.CallerOwned).
        private readonly List<IDisposable> _disposables = new();
        // Resolution stack for cycle detection, keyed on Registration (not contract Type) so a
        // multi-contract singleton that resolves one of its OTHER contracts mid-construction is
        // caught as a cycle rather than silently re-instantiated. Contract kept only for the message.
        private readonly List<(Registration Registration, Type Contract)> _resolving = new();
        private readonly Type[] _stubbed;
        private readonly List<string> _winningStubs = new();
        private bool _disposed;

        internal Container(IEnumerable<Registration> registrations)
        {
            _registrations = registrations.ToList();
            // Highest order wins a contract; among equal orders the later registration (10's override rule).
            foreach (var reg in _registrations.Where(r => !r.Multi))
                foreach (var contract in reg.Contracts)
                    if (!_byContract.TryGetValue(contract, out var current) || reg.Order >= current.Order)
                        _byContract[contract] = reg;
            _stubbed = _byContract.Where(p => p.Value.Order == Order.Stub).Select(p => p.Key)
                                  .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
            _winningStubs.AddRange(_stubbed.Select(t => t.FullName));
            // Multi-bindings keep every entry: stable sort by order, ties in registration order.
            foreach (var group in _registrations.Where(r => r.Multi).GroupBy(r => r.Contracts[0]))
                _many[group.Key] = group.OrderBy(r => r.Order).ToList();
        }

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public object Resolve(Type contract)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Container));
            if (contract == null) throw new ArgumentNullException(nameof(contract));

            // The container can resolve itself (installers occasionally want it).
            if (contract == typeof(Container)) return this;

            if (!_byContract.TryGetValue(contract, out var registration))
                throw new InvalidOperationException(
                    $"No registration for '{contract.FullName}'. Register it via ContainerBuilder before Build().");

            return Instantiate(registration, contract);
        }

        /// <summary>
        /// Every <c>AddMany&lt;T&gt;</c> singleton, sorted by order then registration; empty when none was added.
        /// Each entry is created once and disposed with the container like any singleton.
        /// </summary>
        public IReadOnlyList<T> ResolveAll<T>()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Container));
            if (!_many.TryGetValue(typeof(T), out var registrations)) return Array.Empty<T>();
            var result = new T[registrations.Count];
            for (var i = 0; i < registrations.Count; i++)
                result[i] = (T)Instantiate(registrations[i], typeof(T));
            return result;
        }

        public bool HasBinding(Type type) => type == typeof(Container) || _byContract.ContainsKey(type);

        /// <summary>Single contracts whose winning registration is a stub (<see cref="Order.Stub"/>), sorted by FullName. Nothing is instantiated.</summary>
        public IReadOnlyList<Type> StubbedContracts() => _stubbed;

        /// <summary>
        /// "&lt;contract FullName&gt;" for every single contract a stub still wins, then "&lt;T FullName&gt;:&lt;id&gt;" for every
        /// keyed handler id a <see cref="ResolveKeyed{T}"/> call resolved to an <see cref="IStub"/>.
        /// </summary>
        public IReadOnlyList<string> WinningStubs => _winningStubs;

        /// <summary>
        /// <see cref="ResolveAll{T}"/> folded into a registry by id: a real handler replaces a stub with the same id, two real
        /// handlers with one id throw. Cached per <typeparamref name="T"/>.
        /// </summary>
        public OrderedRegistry<T> ResolveKeyed<T>(Func<T, string> idOf) => throw new NotImplementedException();

        public bool HasBinding<T>() => HasBinding(typeof(T));

        private object Instantiate(Registration registration, Type contract)
        {
            if (registration.Lifetime == Lifetime.Singleton &&
                _singletons.TryGetValue(registration, out var cached))
                return cached;

            if (_resolving.Any(e => ReferenceEquals(e.Registration, registration)))
            {
                var cycle = string.Join(" -> ", _resolving.Select(e => e.Contract.Name).Concat(new[] { contract.Name }));
                throw new InvalidOperationException($"Circular dependency detected: {cycle}");
            }

            _resolving.Add((registration, contract));
            object instance;
            try
            {
                instance = registration.Factory(this);
            }
            finally
            {
                _resolving.RemoveAt(_resolving.Count - 1);
            }

            if (registration.Lifetime == Lifetime.Singleton)
            {
                _singletons[registration] = instance;
                // Track for disposal only container-owned IDisposables, in creation order.
                if (!registration.CallerOwned && instance is IDisposable disposable && !ReferenceEquals(instance, this))
                    _disposables.Add(disposable);
            }

            return instance;
        }

        /// <summary>
        /// Construct a concrete type via its greediest resolvable constructor, injecting
        /// each parameter from this container. Used by concrete-type registrations.
        /// </summary>
        internal object Construct(Type concrete)
        {
            var ctors = concrete
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length);

            foreach (var ctor in ctors)
            {
                var parameters = ctor.GetParameters();
                if (!parameters.All(p => HasBinding(p.ParameterType)))
                    continue;

                var args = parameters.Select(p => Resolve(p.ParameterType)).ToArray();
                return ctor.Invoke(args);
            }

            throw new InvalidOperationException(
                $"Cannot construct '{concrete.FullName}': no public constructor whose parameters are all registered.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // Reverse resolution order: dependents dispose before the dependencies they were
            // constructed from. Never touch caller-owned instances (they were never tracked here).
            for (var i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i].Dispose();
            _disposables.Clear();
            _singletons.Clear();
        }
    }
}
