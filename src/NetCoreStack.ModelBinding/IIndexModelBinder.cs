using System.Threading.Tasks;

namespace NetCoreStack.ModelBinding
{
    public interface IIndexModelBinder
    {
        Task BindModelAsync(IndexModelBindingContext bindingContext);
    }
}
