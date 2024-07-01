namespace Telegram.Bot.Types.Passport;

/// <summary>This object represents an error in the Telegram Passport element which was submitted that should be resolved by the user. It should be one of:<br/><see cref="PassportElementErrorDataField"/>, <see cref="PassportElementErrorFrontSide"/>, <see cref="PassportElementErrorReverseSide"/>, <see cref="PassportElementErrorSelfie"/>, <see cref="PassportElementErrorFile"/>, <see cref="PassportElementErrorFiles"/>, <see cref="PassportElementErrorTranslationFile"/>, <see cref="PassportElementErrorTranslationFiles"/>, <see cref="PassportElementErrorUnspecified"/></summary>
public abstract partial class PassportElementError
{
    /// <summary>Error source</summary>
    public abstract PassportElementErrorSource Source { get; }

    /// <summary>Type of element of the user's Telegram Passport which has the issue</summary>
    public EncryptedPassportElementType Type { get; set; }

    /// <summary>Error message</summary>
    public string Message { get; set; } = default!;
}

/// <summary>Represents an issue in one of the data fields that was provided by the user. The error is considered resolved when the field's value changes.</summary>
public partial class PassportElementErrorDataField : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.Data"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.Data;

    /// <summary>Name of the data field which has the error</summary>
    public string FieldName { get; set; } = default!;

    /// <summary>Base64-encoded data hash</summary>
    public string DataHash { get; set; } = default!;
}

/// <summary>Represents an issue with the front side of a document. The error is considered resolved when the file with the front side of the document changes.</summary>
public partial class PassportElementErrorFrontSide : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.FrontSide"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.FrontSide;

    /// <summary>Base64-encoded hash of the file with the front side of the document</summary>
    public string FileHash { get; set; } = default!;
}

/// <summary>Represents an issue with the reverse side of a document. The error is considered resolved when the file with reverse side of the document changes.</summary>
public partial class PassportElementErrorReverseSide : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.ReverseSide"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.ReverseSide;

    /// <summary>Base64-encoded hash of the file with the reverse side of the document</summary>
    public string FileHash { get; set; } = default!;
}

/// <summary>Represents an issue with the selfie with a document. The error is considered resolved when the file with the selfie changes.</summary>
public partial class PassportElementErrorSelfie : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.Selfie"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.Selfie;

    /// <summary>Base64-encoded hash of the file with the selfie</summary>
    public string FileHash { get; set; } = default!;
}

/// <summary>Represents an issue with a document scan. The error is considered resolved when the file with the document scan changes.</summary>
public partial class PassportElementErrorFile : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.File"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.File;

    /// <summary>Base64-encoded file hash</summary>
    public string FileHash { get; set; } = default!;
}

/// <summary>Represents an issue with a list of scans. The error is considered resolved when the list of files containing the scans changes.</summary>
public partial class PassportElementErrorFiles : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.Files"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.Files;

    /// <summary>List of base64-encoded file hashes</summary>
    public string[] FileHashes { get; set; } = default!;
}

/// <summary>Represents an issue with one of the files that constitute the translation of a document. The error is considered resolved when the file changes.</summary>
public partial class PassportElementErrorTranslationFile : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.TranslationFile"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.TranslationFile;

    /// <summary>Base64-encoded file hash</summary>
    public string FileHash { get; set; } = default!;
}

/// <summary>Represents an issue with the translated version of a document. The error is considered resolved when a file with the document translation change.</summary>
public partial class PassportElementErrorTranslationFiles : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.TranslationFiles"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.TranslationFiles;

    /// <summary>List of base64-encoded file hashes</summary>
    public string[] FileHashes { get; set; } = default!;
}

/// <summary>Represents an issue in an unspecified place. The error is considered resolved when new data is added.</summary>
public partial class PassportElementErrorUnspecified : PassportElementError
{
    /// <summary>Error source, always <see cref="PassportElementErrorSource.Unspecified"/></summary>
    public override PassportElementErrorSource Source => PassportElementErrorSource.Unspecified;

    /// <summary>Base64-encoded element hash</summary>
    public string ElementHash { get; set; } = default!;
}
