namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors ProductApi's ColorFacetResponse for deserialization.</summary>
public record ColorFacetDto(string Name, string? HexCode, int Count);
