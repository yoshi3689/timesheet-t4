using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TimesheetApp.Helpers;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

// Think of this like a notary stamp on a paper document.
// When you sign a timesheet, you're producing a unique stamp that proves:
//   (a) YOU specifically signed it (only your private key could produce it), and
//   (b) the document hasn't been altered since you signed it (any change breaks the stamp).
//
// How it works at a high level:
//   1. The timesheet's data is flattened into a single string.
//   2. That string is hashed with SHA-256 (a one-way fingerprint of the content).
//   3. The hash is encrypted with the signer's RSA private key → this is the "signature".
//   4. To verify later: decrypt the signature with the public key, re-hash the current data,
//      compare the two hashes. If they match, the signature is valid and the data is untouched.
public class SignatureService : ISignatureService
{
    // Signs a timesheet on behalf of a user and returns the signature bytes (EmployeeHash or ApproverHash).
    // The private key is stored encrypted in the DB — the password is needed to unlock it first.
    // Returns null if the password is wrong (decryption fails).
    public byte[]? HashTimesheet(Timesheet timesheet, string password, byte[] encryptedPrivateKey)
    {
        using (RSA rsa = RSA.Create())
        {
            // Step 1: Unlock the private key using the user's password.
            // If the password is wrong, Decrypt returns null and we bail out.
            byte[]? unencrypt = KeyHelper.Decrypt(encryptedPrivateKey, password);
            if (unencrypt == null)
            {
                return null;
            }

            // Step 2: Load the private key into the RSA engine.
            rsa.ImportRSAPrivateKey(unencrypt, out _);

            // Step 3: Build a string representation of the timesheet contents.
            string data = CreateDataString(timesheet);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            // Step 4: Sign the data. Internally this SHA-256 hashes the bytes, then encrypts
            // that hash with the private key. The result is the signature (stored as EmployeeHash
            // or ApproverHash on the Timesheet).
            return rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    // Checks whether a stored signature is still valid for the current state of the timesheet.
    // Uses the employee's PUBLIC key (no password needed — public keys are not secret).
    // Returns false if the data has been tampered with since signing, or if the signature is forged.
    public bool VerifySignature(Timesheet timesheet, byte[] publicKey, byte[] hashedSignature)
    {
        using (RSA rsa = RSA.Create())
        {
            // Load the public key (stored unencrypted on ApplicationUser).
            rsa.ImportRSAPublicKey(publicKey, out _);

            // Re-build the same string from the current timesheet data.
            string data = CreateDataString(timesheet);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            // VerifyData decrypts the signature using the public key, re-hashes the current data,
            // and compares the two. True = data matches what was signed. False = tampered or wrong key.
            return rsa.VerifyData(dataBytes, hashedSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    // Flattens the timesheet into a single string that uniquely represents its contents.
    // This string is what gets signed and verified — any change to hours, dates, WP assignments,
    // or labour codes will produce a different string and break the signature.
    // Note: row order matters here. If rows are reordered in the DB, verification would fail.
    private string CreateDataString(Timesheet timesheet)
    {
        StringBuilder dataBuilder = new StringBuilder();
        dataBuilder.Append(timesheet.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        dataBuilder.Append(timesheet.TotalHours.ToString(CultureInfo.InvariantCulture));
        dataBuilder.Append(timesheet.FlexHours.ToString(CultureInfo.InvariantCulture));
        dataBuilder.Append(timesheet.Overtime.ToString(CultureInfo.InvariantCulture));
        foreach (TimesheetRow row in timesheet.TimesheetRows)
        {
            dataBuilder.Append(row.WorkPackageId);
            dataBuilder.Append(row.WorkPackageProjectId);
            dataBuilder.Append(row.OriginalLabourCode);
            dataBuilder.Append(row.packedHours);  // packed hours = all 7 days bit-packed into one long
        }
        return dataBuilder.ToString();
    }
}
