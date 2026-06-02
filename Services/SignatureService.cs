using System.Security.Cryptography;
using System.Text;
using TimesheetApp.Helpers;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public class SignatureService : ISignatureService
{
    public byte[]? HashTimesheet(Timesheet timesheet, string password, byte[] encryptedPrivateKey)
    {
        using (RSA rsa = RSA.Create())
        {
            byte[]? unencrypt = KeyHelper.Decrypt(encryptedPrivateKey, password);
            if (unencrypt == null)
            {
                return null;
            }
            rsa.ImportRSAPrivateKey(unencrypt, out _);
            string data = CreateDataString(timesheet);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            return rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    public bool VerifySignature(Timesheet timesheet, byte[] publicKey, byte[] hashedSignature)
    {
        using (RSA rsa = RSA.Create())
        {
            rsa.ImportRSAPublicKey(publicKey, out _);
            string data = CreateDataString(timesheet);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            return rsa.VerifyData(dataBytes, hashedSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    private string CreateDataString(Timesheet timesheet)
    {
        StringBuilder dataBuilder = new StringBuilder();
        dataBuilder.Append(timesheet.EndDate);
        dataBuilder.Append(timesheet.TotalHours);
        dataBuilder.Append(timesheet.FlexHours);
        dataBuilder.Append(timesheet.Overtime);
        foreach (TimesheetRow row in timesheet.TimesheetRows)
        {
            dataBuilder.Append(row.WorkPackageId);
            dataBuilder.Append(row.WorkPackageProjectId);
            dataBuilder.Append(row.OriginalLabourCode);
            dataBuilder.Append(row.packedHours);
        }
        return dataBuilder.ToString();
    }
}
