using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;

namespace NetCoreStack.ModelBinding
{
    public class DefaultIndexModelBindingContext : IndexModelBindingContext
    {
        private IValueProvider _originalValueProvider;
        private ModelStateDictionary _modelState;
        private ValidationStateDictionary _validationState;

        private State _state;
        private readonly Stack<State> _stack = new Stack<State>();

        public override string FieldName
        {
            get { return _state.FieldName; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _state.FieldName = value;
            }
        }

        public override object Model
        {
            get { return _state.Model; }
            set { _state.Model = value; }
        }

        public override ModelMetadata ModelMetadata
        {
            get { return _state.ModelMetadata; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _state.ModelMetadata = value;
            }
        }

        public override string ModelName
        {
            get { return _state.ModelName; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _state.ModelName = value;
            }
        }

        public override ModelStateDictionary ModelState
        {
            get { return _modelState; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _modelState = value;
            }
        }

        public override string BinderModelName
        {
            get { return _state.BinderModelName; }
            set { _state.BinderModelName = value; }
        }

        public override BindingSource BindingSource
        {
            get { return _state.BindingSource; }
            set { _state.BindingSource = value; }
        }

        public override bool IsTopLevelObject
        {
            get { return _state.IsTopLevelObject; }
            set { _state.IsTopLevelObject = value; }
        }

        public IValueProvider OriginalValueProvider
        {
            get { return _originalValueProvider; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _originalValueProvider = value;
            }
        }

        public override IValueProvider ValueProvider
        {
            get { return _state.ValueProvider; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _state.ValueProvider = value;
            }
        }

        public override Func<ModelMetadata, bool> PropertyFilter
        {
            get { return _state.PropertyFilter; }
            set { _state.PropertyFilter = value; }
        }

        public override ValidationStateDictionary ValidationState
        {
            get { return _validationState; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _validationState = value;
            }
        }

        public override ModelBindingResult Result
        {
            get
            {
                return _state.Result;
            }
            set
            {
                _state.Result = value;
            }
        }

        public static IndexModelBindingContext CreateBindingContext(
            IValueProvider valueProvider,
            ModelStateDictionary modelState,
            ModelMetadata metadata,
            BindingInfo bindingInfo,
            string modelName)
        {
            if (valueProvider == null)
            {
                throw new ArgumentNullException(nameof(valueProvider));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (modelName == null)
            {
                throw new ArgumentNullException(nameof(modelName));
            }

            var binderModelName = bindingInfo?.BinderModelName ?? metadata.BinderModelName;
            var propertyFilterProvider = bindingInfo?.PropertyFilterProvider ?? metadata.PropertyFilterProvider;

            var bindingSource = bindingInfo?.BindingSource ?? metadata.BindingSource;

            return new DefaultIndexModelBindingContext()
            {
                BinderModelName = binderModelName,
                BindingSource = bindingSource,
                PropertyFilter = propertyFilterProvider?.PropertyFilter,

                // Because this is the top-level context, FieldName and ModelName should be the same.
                FieldName = binderModelName ?? modelName,
                ModelName = binderModelName ?? modelName,

                IsTopLevelObject = true,
                ModelMetadata = metadata,
                ModelState = modelState,

                OriginalValueProvider = valueProvider,
                ValueProvider = FilterValueProvider(valueProvider, bindingSource),

                ValidationState = new ValidationStateDictionary(),
            };
        }

        public override NestedScope EnterNestedScope(
            ModelMetadata modelMetadata,
            string fieldName,
            string modelName,
            object model)
        {
            if (modelMetadata == null)
            {
                throw new ArgumentNullException(nameof(modelMetadata));
            }

            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            if (modelName == null)
            {
                throw new ArgumentNullException(nameof(modelName));
            }

            var scope = EnterNestedScope();

            if (modelMetadata.BindingSource != null && !modelMetadata.BindingSource.IsGreedy)
            {
                ValueProvider = FilterValueProvider(OriginalValueProvider, modelMetadata.BindingSource);
            }

            Model = model;
            ModelMetadata = modelMetadata;
            ModelName = modelName;
            FieldName = fieldName;
            BinderModelName = modelMetadata.BinderModelName;
            BindingSource = modelMetadata.BindingSource;
            PropertyFilter = modelMetadata.PropertyFilterProvider?.PropertyFilter;

            IsTopLevelObject = false;

            return scope;
        }

        public override NestedScope EnterNestedScope()
        {
            _stack.Push(_state);

            Result = default(ModelBindingResult);

            return new NestedScope(this);
        }

        protected override void ExitNestedScope()
        {
            _state = _stack.Pop();
        }

        private static IValueProvider FilterValueProvider(IValueProvider valueProvider, BindingSource bindingSource)
        {
            if (bindingSource == null || bindingSource.IsGreedy)
            {
                return valueProvider;
            }

            var bindingSourceValueProvider = valueProvider as IBindingSourceValueProvider;
            if (bindingSourceValueProvider == null)
            {
                return valueProvider;
            }

            return bindingSourceValueProvider.Filter(bindingSource) ?? new CompositeValueProvider();
        }

        private struct State
        {
            public string FieldName;
            public object Model;
            public ModelMetadata ModelMetadata;
            public string ModelName;

            public IValueProvider ValueProvider;
            public Func<ModelMetadata, bool> PropertyFilter;

            public string BinderModelName;
            public BindingSource BindingSource;
            public bool IsTopLevelObject;

            public ModelBindingResult Result;
        };
    }
}
