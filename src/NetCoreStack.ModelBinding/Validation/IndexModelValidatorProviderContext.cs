using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Generic;

namespace NetCoreStack.ModelBinding
{
    public class IndexModelValidatorProviderContext
    {
        public IndexModelValidatorProviderContext(ModelMetadata modelMetadata, IList<IndexValidatorItem> items)
        {
            ModelMetadata = modelMetadata;
            Results = items;
        }

        public ModelMetadata ModelMetadata { get; set; }

        public IReadOnlyList<object> ValidatorMetadata => ModelMetadata.ValidatorMetadata;

        public IList<IndexValidatorItem> Results { get; }
    }
}
