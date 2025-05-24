using System.Text.Json.Serialization;

namespace GistBackend.Types;

public record SimilarDocument(
    string Reference,
    float Similarity
);
