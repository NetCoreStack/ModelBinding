using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace NetCoreStack.ModelBinding
{
    public interface IIndexModelBinderProvider
    {
        IIndexModelBinder GetBinder(IndexModelBinderProviderContext context);
    }

    public class ComplexTypeModelBinderProvider : IIndexModelBinderProvider
    {
        public IIndexModelBinder GetBinder(IndexModelBinderProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Metadata.IsComplexType && !context.Metadata.IsCollectionType)
            {
                var propertyBinders = new Dictionary<ModelMetadata, IIndexModelBinder>();
                for (var i = 0; i < context.Metadata.Properties.Count; i++)
                {
                    var property = context.Metadata.Properties[i];
                    propertyBinders.Add(property, context.CreateBinder(property));
                }

                return new ComplexTypeIndexModelBinder(propertyBinders);
            }

            return null;
        }
    }

    public class SimpleTypeIndexModelBinderProvider : IIndexModelBinderProvider
    {
        public IIndexModelBinder GetBinder(IndexModelBinderProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!context.Metadata.IsComplexType)
            {
                return new SimpleTypeIndexModelBinder(context.Metadata.ModelType);
            }

            return null;
        }
    }

    public class CollectionIndexModelBinderProvider : IIndexModelBinderProvider
    {
        public IIndexModelBinder GetBinder(IndexModelBinderProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var modelType = context.Metadata.ModelType;

            if (modelType.IsArray)
            {
                return null;
            }
            
            var collectionType = ClosedGenericMatcher.ExtractGenericInterface(modelType, typeof(ICollection<>));
            if (collectionType != null)
            {
                var elementType = collectionType.GenericTypeArguments[0];
                var elementBinder = context.CreateBinder(context.MetadataProvider.GetMetadataForType(elementType));

                var binderType = typeof(CollectionIndexModelBinder<>).MakeGenericType(collectionType.GenericTypeArguments);
                return (IIndexModelBinder)Activator.CreateInstance(binderType, elementBinder);
            }

            var enumerableType = ClosedGenericMatcher.ExtractGenericInterface(modelType, typeof(IEnumerable<>));
            if (enumerableType != null)
            {
                var listType = typeof(List<>).MakeGenericType(enumerableType.GenericTypeArguments);
                if (modelType.GetTypeInfo().IsAssignableFrom(listType.GetTypeInfo()))
                {
                    var elementType = enumerableType.GenericTypeArguments[0];
                    var elementBinder = context.CreateBinder(context.MetadataProvider.GetMetadataForType(elementType));

                    var binderType = typeof(CollectionIndexModelBinder<>).MakeGenericType(enumerableType.GenericTypeArguments);
                    return (IIndexModelBinder)Activator.CreateInstance(binderType, elementBinder);
                }
            }

            return null;
        }
    }
}
