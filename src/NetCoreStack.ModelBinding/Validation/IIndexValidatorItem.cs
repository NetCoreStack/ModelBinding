namespace NetCoreStack.ModelBinding
{
    public class IndexValidatorItem
    {
        public IndexValidatorItem()
        {
        }

        public IndexValidatorItem(object validatorMetadata)
        {
            ValidatorMetadata = validatorMetadata;
        }

        public object ValidatorMetadata { get; }

        public IIndexModelValidator Validator { get; set; }

        public bool IsReusable { get; set; }
    }
}
