namespace AzureDeployer
{
    public class IisConfig
    {
        public static readonly IisConfig Default = new IisConfig
        {
            IdleTimeout = 20,
        };
        public bool AutoStart { get; set; }
        public int IdleTimeout { get; set; }
        public bool Enable32Bit { get; set; }
    }
}
