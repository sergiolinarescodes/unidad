using System;
using System.Collections.Generic;
using Unidad.Core.Abstractions;
using Unidad.Core.Testing;

namespace Unidad.Core.ModelCatalog
{
    internal sealed class ModelCatalogTestFactory : ISystemTestFactory
    {
        readonly Dictionary<string, Func<UnityEngine.GameObject, Views.ModelViewBase>> _viewFactories;

        public ModelCatalogTestFactory(
            Dictionary<string, Func<UnityEngine.GameObject, Views.ModelViewBase>> viewFactories = null)
        {
            _viewFactories = viewFactories;
        }

        public Type[] TestedServices => new[] { typeof(IModelCatalogService) };

        public object CreateForTesting(TestDependencies deps)
        {
            var factory = new LocalGameObjectFactory();
            var database = ModelCatalogDatabase.LoadFromResources();
            return new ModelCatalogService(deps.EventBus, factory, new InstantAnimationResolver(), database, _viewFactories);
        }

        public IEnumerable<ITestScenario> GetScenarios()
        {
            yield return new Scenarios.ModelCatalogScenario();
        }
    }
}
