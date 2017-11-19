using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace NetCoreStack.ModelBinding
{
    public class IndexOptionsManager<TOptions> : IOptions<TOptions> where TOptions : class, new()
    {
        public IndexOptionsManager()
            : this(new TOptions())
        {
        }

        public IndexOptionsManager(TOptions value)
        {
            Value = value;
        }

        public TOptions Value { get; }
    }

    public static class IndexModelBinderHelper
    {
        private static CompositeIndexModelValidatorProvider CreateDefaultValidatorProvider()
        {
            var providers = new IIndexModelValidatorProvider[]
            {
                new DefaultIndexModelValidatorProvider()
            };

            return new CompositeIndexModelValidatorProvider(providers);
        }

        public static TModel CastOrDefault<TModel>(object model)
        {
            return (model is TModel) ? (TModel)model : default(TModel);
        }

        public static IModelMetadataProvider CreateDefaultProvider()
        {
            var detailsProviders = new IMetadataDetailsProvider[]
            {
                new DefaultBindingMetadataProvider(),
                new DefaultValidationMetadataProvider()
            };

            var compositeDetailsProvider = new DefaultCompositeMetadataDetailsProvider(detailsProviders);
            return new DefaultModelMetadataProvider(compositeDetailsProvider, new IndexOptionsManager<MvcOptions>());
        }

        public static IndexModelBinder GetIndexModelBinder()
        {
            var metadataProvider = CreateDefaultProvider();
            var objectValidator = new DefaultIndexObjectValidator(metadataProvider, CreateDefaultValidatorProvider().ValidatorProviders);

            // var options = _serviceProvider.GetRequiredService<IOptions<MvcOptions>>();

            var options = Options.Create(new IndexOptions());
            return new IndexModelBinder(metadataProvider, objectValidator, options);
        }

        public static IList<IValueProvider> GetValueProviders(IFormCollection collection)
        {
            var list = new List<IValueProvider>
            {
                new FormValueProvider(BindingSource.Form, collection, culture: null)
            };

            return list;
        }
    }
}
