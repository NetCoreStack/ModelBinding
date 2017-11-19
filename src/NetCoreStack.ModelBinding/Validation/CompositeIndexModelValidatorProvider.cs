using System;
using System.Collections.Generic;

namespace NetCoreStack.ModelBinding
{
    public class CompositeIndexModelValidatorProvider : IIndexModelValidatorProvider
    {
        public CompositeIndexModelValidatorProvider(IList<IIndexModelValidatorProvider> providers)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            ValidatorProviders = providers;
        }

        public IList<IIndexModelValidatorProvider> ValidatorProviders { get; }

        public void CreateValidators(IndexModelValidatorProviderContext context)
        {
            for (var i = 0; i < ValidatorProviders.Count; i++)
            {
                ValidatorProviders[i].CreateValidators(context);
            }
        }
    }
}
