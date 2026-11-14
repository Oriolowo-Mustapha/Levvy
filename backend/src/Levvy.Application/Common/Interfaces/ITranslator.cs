namespace Levvy.Application.Common.Interfaces;

/// <summary>
/// Translates finalized transcript segments into a viewer's preferred language.
/// </summary>
public interface ITranslator
{
    Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct);
}
