using System.Collections.Generic;

namespace NetCoreStack.ModelBinding
{
    public class IndexOptions
    {
        public IList<IIndexModelBinderProvider> ModelBinderProviders { get; }

        public IndexOptions()
        {
            ModelBinderProviders = new List<IIndexModelBinderProvider>
            {
                new SimpleTypeIndexModelBinderProvider(),
                new CollectionIndexModelBinderProvider(),
                new ComplexTypeModelBinderProvider()
            };
        }
    }
}
