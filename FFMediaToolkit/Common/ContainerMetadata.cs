namespace FFMediaToolkit.Common;

using System.Collections.Generic;
using FFmpeg.AutoGen;

/// <summary>
/// Represents multimedia file metadata info.
/// </summary>
public class ContainerMetadata
{
    private const string TITLE_KEY = "title";
    private const string AUTHOR_KEY = "author";
    private const string ALBUM_KEY = "album";
    private const string YEAR_KEY = "year";
    private const string GENRE_KEY = "genre";
    private const string DESCRIPTION_KEY = "description";
    private const string LANGUAGE_KEY = "language";
    private const string COPYRIGHT_KEY = "copyright";
    private const string RATING_KEY = "rating";
    private const string TRACK_KEY = "track";
    private const string DATE_KEY = "date";

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerMetadata"/> class.
    /// </summary>
    public ContainerMetadata() => Metadata = new Dictionary<string, string>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerMetadata"/> class.
    /// </summary>
    /// <param name="sourceMetadata">The source metadata dictionary.</param>
    internal unsafe ContainerMetadata(AVDictionary* sourceMetadata)
        => Metadata = FFDictionary.ToDictionary(sourceMetadata, true);

    /// <summary>
    /// Gets or sets the multimedia title.
    /// </summary>
    public string Title
    {
        get => Metadata.TryGetValue(TITLE_KEY, out string value) ? value : string.Empty;
        set => Metadata[TITLE_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the multimedia author info.
    /// </summary>
    public string Author
    {
        get => Metadata.TryGetValue(AUTHOR_KEY, out string value) ? value : string.Empty;
        set => Metadata[AUTHOR_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the multimedia album name.
    /// </summary>
    public string Album
    {
        get => Metadata.TryGetValue(ALBUM_KEY, out string value) ? value : string.Empty;
        set => Metadata[ALBUM_KEY] = value;
    }

    /// <summary>
    /// Gets or sets multimedia release date/year.
    /// </summary>
    public string Year
    {
        get => Metadata.TryGetValue(YEAR_KEY, out string value) ? value : (Metadata.TryGetValue(DATE_KEY, out string value2) ? value2 : string.Empty);
        set => Metadata[YEAR_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the multimedia genre.
    /// </summary>
    public string Genre
    {
        get => Metadata.TryGetValue(GENRE_KEY, out string value) ? value : string.Empty;
        set => Metadata[GENRE_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the multimedia description.
    /// </summary>
    public string Description
    {
        get => Metadata.TryGetValue(DESCRIPTION_KEY, out string value) ? value : string.Empty;
        set => Metadata[DESCRIPTION_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the multimedia language.
    /// </summary>
    public string Language
    {
        get => Metadata.TryGetValue(LANGUAGE_KEY, out string value) ? value : string.Empty;
        set => Metadata[LANGUAGE_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the multimedia copyright info.
    /// </summary>
    public string Copyright
    {
        get => Metadata.TryGetValue(COPYRIGHT_KEY, out string value) ? value : string.Empty;
        set => Metadata[COPYRIGHT_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the multimedia rating.
    /// </summary>
    public string Rating
    {
        get => Metadata.TryGetValue(RATING_KEY, out string value) ? value : string.Empty;
        set => Metadata[RATING_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the multimedia track number string.
    /// </summary>
    public string TrackNumber
    {
        get => Metadata.TryGetValue(TRACK_KEY, out string value) ? value : string.Empty;
        set => Metadata[TRACK_KEY] = value;
    }

    /// <summary>
    /// Gets or sets the dictionary containing all metadata fields.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}