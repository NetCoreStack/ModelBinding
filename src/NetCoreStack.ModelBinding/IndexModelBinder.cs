using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace NetCoreStack.ModelBinding
{
    public class IndexModelBinder
    {
        private readonly IModelMetadataProvider _modelMetadataProvider;
        private readonly IObjectIndexModelValidator _validator;
        private readonly DefaultModelBinderFactory _modelBinderFactory;

        public IndexModelBinder(
            IModelMetadataProvider modelMetadataProvider,
            IObjectIndexModelValidator validator,
            IOptions<IndexOptions> options)
        {
            if (modelMetadataProvider == null)
            {
                throw new ArgumentNullException(nameof(modelMetadataProvider));
            }

            if (validator == null)
            {
                throw new ArgumentNullException(nameof(validator));
            }

            _modelMetadataProvider = modelMetadataProvider;
            _validator = validator;
            _modelBinderFactory = new DefaultModelBinderFactory(_modelMetadataProvider, options);
        }

        public Task<ModelBindingResult> BindModelAsync(
            IValueProvider valueProvider,
            ModelStateDictionary modelState,
            ParameterDescriptor parameter)
        {
            return BindModelAsync(valueProvider, modelState, parameter, value: null);
        }

        public virtual Task<ModelBindingResult> BindModelAsync(
            IValueProvider valueProvider,
            ModelStateDictionary modelState,
            ParameterDescriptor parameter,
            object value)
        {
            if (valueProvider == null)
            {
                throw new ArgumentNullException(nameof(valueProvider));
            }

            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            var metadata = _modelMetadataProvider.GetMetadataForType(parameter.ParameterType);
            var binder = _modelBinderFactory.CreateBinder(new ModelBinderFactoryContext
            {
                BindingInfo = parameter.BindingInfo,
                Metadata = metadata,
                CacheToken = parameter,
            });
            
            return BindModelAsync(
                binder,
                valueProvider,
                modelState,
                parameter,
                metadata,
                value);
        }

        public virtual async Task<ModelBindingResult> BindModelAsync(
            IIndexModelBinder modelBinder,
            IValueProvider valueProvider,
            ModelStateDictionary modelState,
            ParameterDescriptor parameter,
            ModelMetadata metadata,
            object value)
        {

            if (modelBinder == null)
            {
                throw new ArgumentNullException(nameof(modelBinder));
            }

            if (valueProvider == null)
            {
                throw new ArgumentNullException(nameof(valueProvider));
            }

            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            var modelBindingContext = DefaultIndexModelBindingContext.CreateBindingContext(
                valueProvider,
                modelState,
                metadata,
                parameter.BindingInfo,
                parameter.Name);
            modelBindingContext.Model = value;

            var parameterModelName = parameter.BindingInfo?.BinderModelName ?? metadata.BinderModelName;
            if (parameterModelName != null)
            {
                modelBindingContext.ModelName = parameterModelName;
            }
            else if (modelBindingContext.ValueProvider.ContainsPrefix(parameter.Name))
            {
                modelBindingContext.ModelName = parameter.Name;
            }
            else
            {
                modelBindingContext.ModelName = string.Empty;
            }

            await modelBinder.BindModelAsync(modelBindingContext);

            var modelBindingResult = modelBindingContext.Result;
            if (modelBindingResult.IsModelSet)
            {
                _validator.Validate(
                    modelBindingContext.ModelState,
                    modelBindingContext.ValidationState,
                    modelBindingContext.ModelName,
                    modelBindingResult.Model);
            }

            return modelBindingResult;
        }
    }
}
