namespace MyNovelBuilder.WebApi.Enums;

/// <summary>
/// Enumeration of image generation providers.
/// </summary>
public enum ImageGenerationProvider
{
    /// <summary>
    /// Custom image generation provider defined by the user.
    /// </summary>
    Custom,
    
    /// <summary>
    /// DeAPI image generation provider.
    /// </summary>
    DeApi,
    
    /// <summary>
    /// NanoGPT image generation provider.
    /// </summary>
    NanoGpt,
}