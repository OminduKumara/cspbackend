using System.Collections.Generic;
using tmsserver.Models;

namespace tmsserver.Data.Repositories;

public interface IInventoryRepository
{
    Task<List<InventoryItem>> GetAllItemsAsync();
    Task<InventoryItem> AddItemAsync(InventoryItem item);
    Task<bool> DeleteItemAsync(int id);
    Task<bool> UpdateItemConditionAsync(int id, string? condition);
    Task<List<InventoryTransaction>> GetTransactionsAsync(bool returnsOnly = false);
    Task<bool> IssueItemAsync(IssueRequest request);
    Task<bool> RequestItemAsync(RequestItemRequest request);
    Task<bool> ReturnTransactionAsync(int transactionId);
}

public class RequestItemRequest
{
    public int InventoryItemId { get; set; }
    public int RequestedByUserId { get; set; }
    public int Quantity { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class IssueRequest
{
    public int InventoryItemId { get; set; }
    public int IssuedToUserId { get; set; }
    public int Quantity { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int PerformedByAdminId { get; set; }
}
