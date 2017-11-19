using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NetCoreStack.ModelBinding
{
    public interface IIndexCollectionModelBinder : IIndexModelBinder
    {
        bool CanCreateInstance(Type targetType);
    }

    public class CollectionIndexModelBinder<TElement> : IIndexCollectionModelBinder
    {
        private Func<object> _modelCreator;

        public CollectionIndexModelBinder(IIndexModelBinder elementBinder)
        {
            if (elementBinder == null)
            {
                throw new ArgumentNullException(nameof(elementBinder));
            }

            ElementBinder = elementBinder;
        }

        protected IIndexModelBinder ElementBinder { get; }

        public virtual async Task BindModelAsync(IndexModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var model = bindingContext.Model;
            if (!bindingContext.ValueProvider.ContainsPrefix(bindingContext.ModelName))
            {
                if (bindingContext.IsTopLevelObject)
                {
                    if (model == null)
                    {
                        model = CreateEmptyCollection(bindingContext.ModelType);
                    }

                    bindingContext.Result = ModelBindingResult.Success(model);
                }

                return;
            }

            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

            CollectionResult result;
            if (valueProviderResult == ValueProviderResult.None)
            {
                result = await BindComplexCollection(bindingContext);
            }
            else
            {
                result = await BindSimpleCollection(bindingContext, valueProviderResult);
            }

            var boundCollection = result.Model;
            if (model == null)
            {
                model = ConvertToCollectionType(bindingContext.ModelType, boundCollection);
            }
            else
            {
                CopyToModel(model, boundCollection);
            }

            Debug.Assert(model != null);
            if (result.ValidationStrategy != null)
            {
                bindingContext.ValidationState.Add(model, new ValidationStateEntry()
                {
                    Strategy = result.ValidationStrategy,
                });
            }

            if (valueProviderResult != ValueProviderResult.None)
            {
                bindingContext.ModelState.SetModelValue(
                    bindingContext.ModelName,
                    valueProviderResult);
            }

            bindingContext.Result = ModelBindingResult.Success(model);
        }
        
        public virtual bool CanCreateInstance(Type targetType)
        {
            if (targetType.IsAssignableFrom(typeof(List<TElement>)))
            {
                return true;
            }

            return targetType.GetTypeInfo().IsClass &&
                !targetType.GetTypeInfo().IsAbstract &&
                typeof(ICollection<TElement>).IsAssignableFrom(targetType);
        }

        protected virtual object CreateEmptyCollection(Type targetType)
        {
            if (targetType.IsAssignableFrom(typeof(List<TElement>)))
            {
                // Simple case such as ICollection<TElement>, IEnumerable<TElement> and IList<TElement>.
                return new List<TElement>();
            }

            return CreateInstance(targetType);
        }

        protected object CreateInstance(Type targetType)
        {
            if (_modelCreator == null)
            {
                _modelCreator = Expression
                    .Lambda<Func<object>>(Expression.New(targetType))
                    .Compile();
            }

            return _modelCreator();

        }

        internal async Task<CollectionResult> BindSimpleCollection(
            IndexModelBindingContext bindingContext,
            ValueProviderResult values)
        {
            var boundCollection = new List<TElement>();

            var elementMetadata = bindingContext.ModelMetadata.ElementMetadata;

            foreach (var value in values)
            {
                bindingContext.ValueProvider = new CompositeValueProvider
                {
                    // our temporary provider goes at the front of the list
                    new ElementalValueProvider(bindingContext.ModelName, value, values.Culture),
                    bindingContext.ValueProvider
                };

                using (bindingContext.EnterNestedScope(
                    elementMetadata,
                    fieldName: bindingContext.FieldName,
                    modelName: bindingContext.ModelName,
                    model: null))
                {
                    await ElementBinder.BindModelAsync(bindingContext);

                    if (bindingContext.Result.IsModelSet)
                    {
                        var boundValue = bindingContext.Result.Model;
                        boundCollection.Add(IndexModelBinderHelper.CastOrDefault<TElement>(boundValue));
                    }
                }
            }

            return new CollectionResult
            {
                Model = boundCollection
            };
        }

        // Used when the ValueProvider contains the collection to be bound as multiple elements, e.g. foo[0], foo[1].
        private Task<CollectionResult> BindComplexCollection(IndexModelBindingContext bindingContext)
        {
            var indexPropertyName = ModelNames.CreatePropertyModelName(bindingContext.ModelName, "index");
            var valueProviderResultIndex = bindingContext.ValueProvider.GetValue(indexPropertyName);
            var indexNames = GetIndexNamesFromValueProviderResult(valueProviderResultIndex);

            return BindComplexCollectionFromIndexes(bindingContext, indexNames);
        }
        
        internal async Task<CollectionResult> BindComplexCollectionFromIndexes(
            IndexModelBindingContext bindingContext,
            IEnumerable<string> indexNames)
        {
            bool indexNamesIsFinite;
            if (indexNames != null)
            {
                indexNamesIsFinite = true;
            }
            else
            {
                indexNamesIsFinite = false;
                indexNames = Enumerable.Range(0, int.MaxValue)
                                       .Select(i => i.ToString(CultureInfo.InvariantCulture));
            }

            var elementMetadata = bindingContext.ModelMetadata.ElementMetadata;

            var boundCollection = new List<TElement>();

            foreach (var indexName in indexNames)
            {
                var fullChildName = ModelNames.CreateIndexModelName(bindingContext.ModelName, indexName);

                var didBind = false;
                object boundValue = null;
                ModelBindingResult? result;
                using (bindingContext.EnterNestedScope(
                    elementMetadata,
                    fieldName: indexName,
                    modelName: fullChildName,
                    model: null))
                {
                    await ElementBinder.BindModelAsync(bindingContext);
                    result = bindingContext.Result;
                }

                if (result != null && result.Value.IsModelSet)
                {
                    didBind = true;
                    boundValue = result.Value.Model;
                }

                // infinite size collection stops on first bind failure
                if (!didBind && !indexNamesIsFinite)
                {
                    break;
                }

                boundCollection.Add(IndexModelBinderHelper.CastOrDefault<TElement>(boundValue));
            }

            return new CollectionResult
            {
                Model = boundCollection,

                ValidationStrategy = indexNamesIsFinite ?
                    new ExplicitIndexCollectionValidationStrategy(indexNames) :
                    null,
            };
        }
        
        internal class CollectionResult
        {
            public IEnumerable<TElement> Model { get; set; }

            public IValidationStrategy ValidationStrategy { get; set; }
        }
        
        protected virtual object ConvertToCollectionType(Type targetType, IEnumerable<TElement> collection)
        {
            if (collection == null)
            {
                return null;
            }

            if (targetType.IsAssignableFrom(typeof(List<TElement>)))
            {
                return collection;
            }

            var newCollection = CreateInstance(targetType);
            CopyToModel(newCollection, collection);

            return newCollection;
        }

        protected virtual void CopyToModel(object target, IEnumerable<TElement> sourceCollection)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var targetCollection = target as ICollection<TElement>;
            Debug.Assert(targetCollection != null, "This binder is instantiated only for ICollection<T> model types.");

            if (sourceCollection != null && targetCollection != null && !targetCollection.IsReadOnly)
            {
                targetCollection.Clear();
                foreach (var element in sourceCollection)
                {
                    targetCollection.Add(element);
                }
            }
        }

        private static IEnumerable<string> GetIndexNamesFromValueProviderResult(ValueProviderResult valueProviderResult)
        {
            IEnumerable<string> indexNames = null;
            if (valueProviderResult != null)
            {
                var indexes = (string[])valueProviderResult;
                if (indexes != null && indexes.Length > 0)
                {
                    indexNames = indexes;
                }
            }

            return indexNames;
        }
    }
}
