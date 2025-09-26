// ConfigurationValidatorService.cs
public class ConfigurationValidatorService
{
    private readonly IConfiguration _configuration;

    public ConfigurationValidatorService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool ValidateDigitalSigningConfig()
    {
        var certPath = _configuration["DigitalSigning:CertificatePath"];
        var certPassword = _configuration["DigitalSigning:CertificatePassword"];

        if (string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(certPassword))
        {
            return false;
        }

        return File.Exists(certPath);
    }
}