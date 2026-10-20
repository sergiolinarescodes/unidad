using System;
using System.Collections.Generic;
using Unidad.Adapter.Models;

namespace Unidad.Adapter
{
    internal sealed class ModelCatalog : IModelCatalog
    {
        public ModelCatalog(IReadOnlyList<IModelSource> sources) => throw new NotImplementedException();

        public IReadOnlyList<string> Ids => throw new NotImplementedException();

        public bool Has(string id) => throw new NotImplementedException();

        public ModelEntry Get(string id) => throw new NotImplementedException();
    }
}
