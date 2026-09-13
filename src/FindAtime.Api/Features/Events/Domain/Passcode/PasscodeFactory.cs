public class PasscodeFactory
{
    private PasscodeHasher _passcodeHasher;
    private PasscodeGenerator _passcodeGenerator;

    public PasscodeFactory(PasscodeHasher passcodeHasher, PasscodeGenerator passcodeGenerator)
    {
        this._passcodeHasher = passcodeHasher;
        this._passcodeGenerator = passcodeGenerator;
    }

    public PasscodeCreated CreatePasscode()
    {
        Passcode passcode = this._passcodeGenerator.Generate();
        PasscodeHash passcodeHash = this._passcodeHasher.Hash(passcode.Value);
        return PasscodeCreated.Create(passcode, passcodeHash);
    }
}
