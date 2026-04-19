using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using tmsserver.Models;

namespace tmsserver.Data.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly string _connectionString;

    public InventoryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") 
            ?? throw new InvalidOperationException("Connection string not found");
    }

    public async Task<List<InventoryItem>> GetAllItemsAsync()
    {
        var items = new List<InventoryItem>();
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var cmd = new SqlCommand("SELECT Id, Name, Description, Quantity, Category, CreatedAt, UpdatedAt, Condition FROM Inventory", conn);
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    items.Add(new InventoryItem
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Quantity = reader.GetInt32(3),
                        Category = reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5),
                        UpdatedAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                        Condition = reader.IsDBNull(7) ? null : reader.GetString(7)
                    });
                }
            }
        }
        return items;
    }

    public async Task<InventoryItem> AddItemAsync(InventoryItem item)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var cmd = new SqlCommand(
                "INSERT INTO Inventory (Name, Description, Quantity, Category, Condition, CreatedAt) OUTPUT INSERTED.Id VALUES (@Name, @Description, @Quantity, @Category, @Condition, @CreatedAt)", conn);
            cmd.Parameters.AddWithValue("@Name", item.Name ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", item.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
            cmd.Parameters.AddWithValue("@Category", item.Category ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Condition", item.Condition ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
            item.Id = (int)await cmd.ExecuteScalarAsync();
            item.CreatedAt = DateTime.UtcNow;
        }
        return item;
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var txCmd = new SqlCommand("DELETE FROM InventoryTransaction WHERE InventoryItemId = @Id", conn);
            txCmd.Parameters.AddWithValue("@Id", id);
            await txCmd.ExecuteNonQueryAsync();

            var cmd = new SqlCommand("DELETE FROM Inventory WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
    }

    public async Task<bool> UpdateItemConditionAsync(int id, string? condition)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var cmd = new SqlCommand("UPDATE Inventory SET Condition = @Condition, UpdatedAt = @UpdatedAt WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Condition", string.IsNullOrWhiteSpace(condition) ? (object)DBNull.Value : condition);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@Id", id);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
    }

    public async Task<List<InventoryTransaction>> GetTransactionsAsync(bool returnsOnly = false)
    {
        var txs = new List<InventoryTransaction>();
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            string query = returnsOnly 
                ? "SELECT Id, InventoryItemId, IssuedToUserId, QuantityChanged, Comment, Timestamp, PerformedByAdminId FROM InventoryTransaction WHERE QuantityChanged > 0"
                : "SELECT Id, InventoryItemId, IssuedToUserId, QuantityChanged, Comment, Timestamp, PerformedByAdminId FROM InventoryTransaction WHERE QuantityChanged <= 0";
            
            var cmd = new SqlCommand(query, conn);
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    txs.Add(new InventoryTransaction
                    {
                        Id = reader.GetInt32(0),
                        InventoryItemId = reader.GetInt32(1),
                        IssuedToUserId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                        QuantityChanged = reader.GetInt32(3),
                        Comment = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Timestamp = reader.GetDateTime(5),
                        PerformedByAdminId = reader.IsDBNull(6) ? null : reader.GetInt32(6)
                    });
                }
            }
        }
        return txs;
    }

    public async Task<bool> IssueItemAsync(IssueRequest req)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var checkCmd = new SqlCommand("SELECT Quantity FROM Inventory WHERE Id = @Id", conn);
            checkCmd.Parameters.AddWithValue("@Id", req.InventoryItemId);
            var qtyObj = await checkCmd.ExecuteScalarAsync();
            if (qtyObj == null) return false;
            int qty = (int)qtyObj;
            if (qty < req.Quantity) return false;

            var updateCmd = new SqlCommand("UPDATE Inventory SET Quantity = Quantity - @Qty WHERE Id = @Id", conn);
            updateCmd.Parameters.AddWithValue("@Qty", req.Quantity);
            updateCmd.Parameters.AddWithValue("@Id", req.InventoryItemId);
            await updateCmd.ExecuteNonQueryAsync();

            var checkReqCmd = new SqlCommand("SELECT TOP 1 Id FROM InventoryTransaction WHERE InventoryItemId = @ItemId AND IssuedToUserId = @UserId AND QuantityChanged = 0 ORDER BY Timestamp ASC", conn);
            checkReqCmd.Parameters.AddWithValue("@ItemId", req.InventoryItemId);
            checkReqCmd.Parameters.AddWithValue("@UserId", req.IssuedToUserId);
            var reqIdObj = await checkReqCmd.ExecuteScalarAsync();

            if (reqIdObj != null)
            {
                int reqId = (int)reqIdObj;
                var txCmd = new SqlCommand("UPDATE InventoryTransaction SET QuantityChanged = @QtyChanged, Comment = @Comment, Timestamp = @Timestamp, PerformedByAdminId = @AdminId WHERE Id = @ReqId", conn);
                txCmd.Parameters.AddWithValue("@QtyChanged", -req.Quantity);
                txCmd.Parameters.AddWithValue("@Comment", req.Comment ?? (object)DBNull.Value);
                txCmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                txCmd.Parameters.AddWithValue("@AdminId", req.PerformedByAdminId);
                txCmd.Parameters.AddWithValue("@ReqId", reqId);
                await txCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var txCmd = new SqlCommand(
                    "INSERT INTO InventoryTransaction (InventoryItemId, IssuedToUserId, QuantityChanged, Comment, Timestamp, PerformedByAdminId) VALUES (@ItemId, @UserId, @QtyChanged, @Comment, @Timestamp, @AdminId)", conn);
                txCmd.Parameters.AddWithValue("@ItemId", req.InventoryItemId);
                txCmd.Parameters.AddWithValue("@UserId", req.IssuedToUserId);
                txCmd.Parameters.AddWithValue("@QtyChanged", -req.Quantity);
                txCmd.Parameters.AddWithValue("@Comment", req.Comment ?? (object)DBNull.Value);
                txCmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                txCmd.Parameters.AddWithValue("@AdminId", req.PerformedByAdminId);
                await txCmd.ExecuteNonQueryAsync();
            }
            return true;
        }
    }

    public async Task<bool> RequestItemAsync(RequestItemRequest req)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var checkCmd = new SqlCommand("SELECT Quantity FROM Inventory WHERE Id = @Id", conn);
            checkCmd.Parameters.AddWithValue("@Id", req.InventoryItemId);
            var qtyObj = await checkCmd.ExecuteScalarAsync();
            if (qtyObj == null) return false;
            int qty = (int)qtyObj;
            if (qty < req.Quantity) return false;

            var txCmd = new SqlCommand(
                "INSERT INTO InventoryTransaction (InventoryItemId, IssuedToUserId, QuantityChanged, Comment, Timestamp, PerformedByAdminId) VALUES (@ItemId, @UserId, @QtyChanged, @Comment, @Timestamp, NULL)", conn);
            txCmd.Parameters.AddWithValue("@ItemId", req.InventoryItemId);
            txCmd.Parameters.AddWithValue("@UserId", req.RequestedByUserId);
            txCmd.Parameters.AddWithValue("@QtyChanged", 0);
            txCmd.Parameters.AddWithValue("@Comment", $"Request [Qty: {req.Quantity}]: " + (req.Comment ?? ""));
            txCmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
            await txCmd.ExecuteNonQueryAsync();
            return true;
        }
    }

    public async Task<bool> ReturnTransactionAsync(int transactionId)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var txCmd = new SqlCommand("SELECT InventoryItemId, QuantityChanged FROM InventoryTransaction WHERE Id = @Id", conn);
            txCmd.Parameters.AddWithValue("@Id", transactionId);
            using (var reader = await txCmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) return false;
                int itemId = reader.GetInt32(0);
                int qtyChanged = reader.GetInt32(1);
                if (qtyChanged >= 0) return false;
                reader.Close();

                var updateCmd = new SqlCommand("UPDATE Inventory SET Quantity = Quantity - @Qty WHERE Id = @Id", conn);
                updateCmd.Parameters.AddWithValue("@Qty", qtyChanged);
                updateCmd.Parameters.AddWithValue("@Id", itemId);
                await updateCmd.ExecuteNonQueryAsync();

                var returnLogCmd = new SqlCommand("UPDATE InventoryTransaction SET QuantityChanged = @PosQty, Timestamp = @Timestamp, Comment = CONCAT(Comment, ' (Returned)') WHERE Id = @TxId", conn);
                returnLogCmd.Parameters.AddWithValue("@PosQty", -qtyChanged);
                returnLogCmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                returnLogCmd.Parameters.AddWithValue("@TxId", transactionId);
                await returnLogCmd.ExecuteNonQueryAsync();
                return true;
            }
        }
    }
}
