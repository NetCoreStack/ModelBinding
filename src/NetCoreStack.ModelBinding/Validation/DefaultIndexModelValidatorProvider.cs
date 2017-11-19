namespace NetCoreStack.ModelBinding
{
    public class DefaultIndexModelValidatorProvider : IIndexModelValidatorProvider
    {
        public void CreateValidators(IndexModelValidatorProviderContext context)
        {
            for (var i = 0; i < context.Results.Count; i++)
            {
                var validatorItem = context.Results[i];
                
                if (validatorItem.Validator != null)
                {
                    continue;
                }

                var validator = validatorItem.ValidatorMetadata as IIndexModelValidator;
                if (validator != null)
                {
                    validatorItem.Validator = validator;
                    validatorItem.IsReusable = true;
                }
            }
        }
    }
}
