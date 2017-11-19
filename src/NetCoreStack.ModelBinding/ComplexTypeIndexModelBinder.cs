using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace NetCoreStack.ModelBinding
{
    public class ComplexTypeIndexModelBinder : IIndexModelBinder
    {
        private readonly IDictionary<ModelMetadata, IIndexModelBinder> _propertyBinders;
        private Func<object> _modelCreator;

        public ComplexTypeIndexModelBinder(IDictionary<ModelMetadata, IIndexModelBinder> propertyBinders)
        {
            if (propertyBinders == null)
            {
                throw new ArgumentNullException(nameof(propertyBinders));
            }

            _propertyBinders = propertyBinders;
        }

        public Task BindModelAsync(IndexModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            if (!CanCreateModel(bindingContext))
            {
                return Task.CompletedTask;
            }

            return BindModelCoreAsync(bindingContext);
        }

        private async Task BindModelCoreAsync(IndexModelBindingContext bindingContext)
        {
            if (bindingContext.Model == null)
            {
                bindingContext.Model = CreateModel(bindingContext);
            }

            for (var i = 0; i < bindingContext.ModelMetadata.Properties.Count; i++)
            {
                var property = bindingContext.ModelMetadata.Properties[i];
                if (!CanBindProperty(bindingContext, property))
                {
                    continue;
                }

                object propertyModel = null;
                if (property.PropertyGetter != null &&
                    property.IsComplexType &&
                    !property.ModelType.IsArray)
                {
                    propertyModel = property.PropertyGetter(bindingContext.Model);
                }

                var fieldName = property.BinderModelName ?? property.PropertyName;
                var modelName = ModelNames.CreatePropertyModelName(bindingContext.ModelName, fieldName);

                ModelBindingResult result;
                using (bindingContext.EnterNestedScope(
                    modelMetadata: property,
                    fieldName: fieldName,
                    modelName: modelName,
                    model: propertyModel))
                {
                    await BindProperty(bindingContext);
                    result = bindingContext.Result;
                }

                if (result.IsModelSet)
                {
                    SetProperty(bindingContext, modelName, property, result);
                }
                else if (property.IsBindingRequired)
                {
                    var message = property.ModelBindingMessageProvider.MissingBindRequiredValueAccessor(fieldName);
                    bindingContext.ModelState.TryAddModelError(modelName, message);
                }
            }

            bindingContext.Result = ModelBindingResult.Success(bindingContext.Model);
        }

        protected virtual bool CanBindProperty(IndexModelBindingContext bindingContext, ModelMetadata propertyMetadata)
        {
            var metadataProviderFilter = bindingContext.ModelMetadata.PropertyFilterProvider?.PropertyFilter;
            if (metadataProviderFilter?.Invoke(propertyMetadata) == false)
            {
                return false;
            }

            if (bindingContext.PropertyFilter?.Invoke(propertyMetadata) == false)
            {
                return false;
            }

            if (!propertyMetadata.IsBindingAllowed)
            {
                return false;
            }

            if (!CanUpdatePropertyInternal(propertyMetadata))
            {
                return false;
            }

            return true;
        }

        protected virtual Task BindProperty(IndexModelBindingContext bindingContext)
        {
            var binder = _propertyBinders[bindingContext.ModelMetadata];
            return binder.BindModelAsync(bindingContext);
        }

        internal bool CanCreateModel(IndexModelBindingContext bindingContext)
        {
            var isTopLevelObject = bindingContext.IsTopLevelObject;

            var bindingSource = bindingContext.BindingSource;
            if (!isTopLevelObject && bindingSource != null && bindingSource.IsGreedy)
            {
                return false;
            }

            if (isTopLevelObject)
            {
                return true;
            }

            if (CanValueBindAnyModelProperties(bindingContext))
            {
                return true;
            }

            return false;
        }

        private bool CanValueBindAnyModelProperties(IndexModelBindingContext bindingContext)
        {
            if (bindingContext.ModelMetadata.Properties.Count == 0)
            {
                return false;
            }

            var hasBindableProperty = false;
            var isAnyPropertyEnabledForValueProviderBasedBinding = false;
            for (var i = 0; i < bindingContext.ModelMetadata.Properties.Count; i++)
            {
                var propertyMetadata = bindingContext.ModelMetadata.Properties[i];
                if (!CanBindProperty(bindingContext, propertyMetadata))
                {
                    continue;
                }

                hasBindableProperty = true;

                var bindingSource = propertyMetadata.BindingSource;
                if (bindingSource == null || !bindingSource.IsGreedy)
                {
                    isAnyPropertyEnabledForValueProviderBasedBinding = true;

                    var fieldName = propertyMetadata.BinderModelName ?? propertyMetadata.PropertyName;
                    var modelName = ModelNames.CreatePropertyModelName(
                        bindingContext.ModelName,
                        fieldName);

                    using (bindingContext.EnterNestedScope(
                        modelMetadata: propertyMetadata,
                        fieldName: fieldName,
                        modelName: modelName,
                        model: null))
                    {
                        if (bindingContext.ValueProvider.ContainsPrefix(bindingContext.ModelName))
                        {
                            return true;
                        }
                    }
                }
            }

            if (hasBindableProperty && !isAnyPropertyEnabledForValueProviderBasedBinding)
            {
                return true;
            }

            return false;
        }

        internal static bool CanUpdatePropertyInternal(ModelMetadata propertyMetadata)
        {
            return !propertyMetadata.IsReadOnly || CanUpdateReadOnlyProperty(propertyMetadata.ModelType);
        }

        private static bool CanUpdateReadOnlyProperty(Type propertyType)
        {
            if (propertyType.GetTypeInfo().IsValueType)
            {
                return false;
            }

            if (propertyType.IsArray)
            {
                return false;
            }

            if (propertyType == typeof(string))
            {
                return false;
            }

            return true;
        }

        protected virtual object CreateModel(IndexModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            if (_modelCreator == null)
            {
                var modelTypeInfo = bindingContext.ModelType.GetTypeInfo();
                if (modelTypeInfo.IsAbstract || modelTypeInfo.GetConstructor(Type.EmptyTypes) == null)
                {
                    if (bindingContext.IsTopLevelObject)
                    {
                        throw new InvalidOperationException($"{modelTypeInfo.FullName} - NoParameterlessConstructor-TopLevelObject");
                    }

                    throw new InvalidOperationException($"{modelTypeInfo.FullName},{bindingContext.ModelName},{bindingContext.ModelMetadata.ContainerType.FullName}, " +
                        $"NoParameterlessConstructor_ForProperty");
                }

                _modelCreator = Expression
                    .Lambda<Func<object>>(Expression.New(bindingContext.ModelType))
                    .Compile();
            }

            return _modelCreator();
        }

        protected virtual void SetProperty(
            IndexModelBindingContext bindingContext,
            string modelName,
            ModelMetadata propertyMetadata,
            ModelBindingResult result)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            if (modelName == null)
            {
                throw new ArgumentNullException(nameof(modelName));
            }

            if (propertyMetadata == null)
            {
                throw new ArgumentNullException(nameof(propertyMetadata));
            }

            if (!result.IsModelSet)
            {
                return;
            }

            if (propertyMetadata.IsReadOnly)
            {
                return;
            }

            var value = result.Model;
            try
            {
                propertyMetadata.PropertySetter(bindingContext.Model, value);
            }
            catch (Exception exception)
            {
                AddModelError(exception, modelName, bindingContext);
            }
        }

        private static void AddModelError(
            Exception exception,
            string modelName,
            IndexModelBindingContext bindingContext)
        {
            var targetInvocationException = exception as TargetInvocationException;
            if (targetInvocationException?.InnerException != null)
            {
                exception = targetInvocationException.InnerException;
            }
            
            var modelState = bindingContext.ModelState;
            var validationState = modelState.GetFieldValidationState(modelName);
            if (validationState == ModelValidationState.Unvalidated)
            {
                modelState.AddModelError(modelName, exception, bindingContext.ModelMetadata);
            }
        }
    }
}