using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TimesheetApp.Models;

namespace TimesheetApp.Helpers
{
    public class KeyHelper
    {
        public static byte[] CreateKeyPair(string currentUser)
        {
            var parameters = new CspParameters
            {
                KeyContainerName = currentUser
            };
            using var rsa = new RSACryptoServiceProvider(parameters);
            return rsa.ExportRSAPublicKey();
        }


        public static void GetKeyFromContainer(string currentUser)
        {
            // Create the CspParameters object and set the key container
            // name used to store the RSA key pair.
            var parameters = new CspParameters
            {
                KeyContainerName = currentUser
            };

            // Create a new instance of RSACryptoServiceProvider that accesses
            // the key container MyKeyContainerName.
            using var rsa = new RSACryptoServiceProvider(parameters);

            // Display the key information to the console.
            Console.WriteLine($"Key retrieved from container : \n {rsa.ToXmlString(true)}");
        }
        public static void DeleteKeyFromContainer(string currentUser)
        {
            // Create the CspParameters object and set the key container
            // name used to store the RSA key pair.
            var parameters = new CspParameters
            {
                KeyContainerName = currentUser
            };

            // Create a new instance of RSACryptoServiceProvider that accesses
            // the key container.
            using var rsa = new RSACryptoServiceProvider(parameters)
            {
                // Delete the key entry in the container.
                PersistKeyInCsp = false
            };

            // Call Clear to release resources and delete the key from the container.
            rsa.Clear();
        }
    }
}