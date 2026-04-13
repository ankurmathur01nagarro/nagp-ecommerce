namespace ECOM.WebApi.Dtos;

public record ProductMetadataDto(
    List<ProductColorDto>? Colors,
    List<string>? Sizes,
    List<string>? Tags,
    List<TechSpecDto>? TechSpecs,
    int? Rating,
    string? AdditionalInfo);
