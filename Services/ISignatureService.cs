using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public interface ISignatureService
{
    byte[]? HashTimesheet(Timesheet timesheet, string password, byte[] encryptedPrivateKey);
    bool VerifySignature(Timesheet timesheet, byte[] publicKey, byte[] hashedSignature);
}
