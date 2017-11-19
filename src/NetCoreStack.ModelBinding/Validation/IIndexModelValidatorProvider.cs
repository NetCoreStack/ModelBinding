namespace NetCoreStack.ModelBinding
{
    public interface IIndexModelValidatorProvider
    {
        void CreateValidators(IndexModelValidatorProviderContext context);
    }
}
