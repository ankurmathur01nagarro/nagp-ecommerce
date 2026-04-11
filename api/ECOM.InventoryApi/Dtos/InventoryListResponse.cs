namespace ECOM.InventoryApi.Dtos;

public record InventoryListResponse(
    List<InventoryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
