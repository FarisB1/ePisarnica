// CertificateHelper.cs
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using System.IO;
using System.Linq;

namespace ePisarnica.Helpers
{
    public static class CertificateHelper
    {
        public static (AsymmetricKeyParameter PrivateKey, X509Certificate Certificate) LoadCertificate(
            string certificatePath, string certificatePassword)
        {
            using (FileStream pfxStream = new FileStream(certificatePath, FileMode.Open, FileAccess.Read))
            {
                // Create Pkcs12StoreBuilder and build the store
                Pkcs12StoreBuilder builder = new Pkcs12StoreBuilder();
                Pkcs12Store store = builder.Build();

                // Load the store from the stream
                store.Load(pfxStream, certificatePassword.ToCharArray());

                string alias = null;
                foreach (string al in store.Aliases)
                {
                    if (store.IsKeyEntry(al))
                    {
                        alias = al;
                        break;
                    }
                }

                if (alias == null)
                {
                    throw new Exception("Private key not found in PFX file.");
                }

                // Get the key entry
                var keyEntry = store.GetKey(alias);
                if (keyEntry == null || keyEntry.Key.IsPrivate == false)
                {
                    throw new Exception("Valid private key not found.");
                }

                // Get the certificate
                var certEntry = store.GetCertificate(alias);
                if (certEntry == null)
                {
                    throw new Exception("Certificate not found.");
                }

                return (keyEntry.Key, certEntry.Certificate);
            }
        }

        public static X509Certificate[] GetCertificateChain(string certificatePath, string certificatePassword)
        {
            using (FileStream pfxStream = new FileStream(certificatePath, FileMode.Open, FileAccess.Read))
            {
                Pkcs12StoreBuilder builder = new Pkcs12StoreBuilder();
                Pkcs12Store store = builder.Build();
                store.Load(pfxStream, certificatePassword.ToCharArray());

                string alias = store.Aliases.Cast<string>()
                    .FirstOrDefault(a => store.IsKeyEntry(a));

                if (alias == null)
                {
                    throw new Exception("Private key not found in PFX file.");
                }

                // Get certificate chain
                var chain = store.GetCertificateChain(alias);
                return chain.Select(c => c.Certificate).ToArray();
            }
        }
    }
}