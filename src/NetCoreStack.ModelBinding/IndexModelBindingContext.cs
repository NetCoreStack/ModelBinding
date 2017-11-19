using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;

namespace NetCoreStack.ModelBinding
{
    public abstract class IndexModelBindingContext
    {
        public abstract string BinderModelName { get; set; }

        public abstract BindingSource BindingSource { get; set; }

        public abstract string FieldName { get; set; }

        public abstract bool IsTopLevelObject { get; set; }

        public abstract object Model { get; set; }

        public abstract ModelMetadata ModelMetadata { get; set; }

        public abstract string ModelName { get; set; }

        public abstract ModelStateDictionary ModelState { get; set; }

        public virtual Type ModelType => ModelMetadata.ModelType;

        public abstract Func<ModelMetadata, bool> PropertyFilter { get; set; }

        public abstract ValidationStateDictionary ValidationState { get; set; }

        public abstract IValueProvider ValueProvider { get; set; }

        public abstract ModelBindingResult Result { get; set; }

        public abstract NestedScope EnterNestedScope(ModelMetadata modelMetadata, string fieldName, string modelName, object model);
  
        public abstract NestedScope EnterNestedScope();

        protected abstract void ExitNestedScope();

        public struct NestedScope : IDisposable
        {
            private readonly IndexModelBindingContext _context;

            public NestedScope(IndexModelBindingContext context)
            {
                _context = context;
            }

            public void Dispose()
            {
                _context.ExitNestedScope();
            }
        }
    }
}
