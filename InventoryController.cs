using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using tmsserver.Models;
using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace tmsserver
{
    [ApiController]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly string _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING");

        [HttpGet]
        public ActionResult<IEnumerable<InventoryItem>> GetInventory()
        {
            var items = new List<InventoryItem>();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Id, Name, Description, Quantity, Category, CreatedAt, UpdatedAt, Condition FROM Inventory", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
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
            return Ok(items);
        }

        [HttpPost("add")]
        public ActionResult AddItem([FromBody] InventoryItem item)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(
                    "INSERT INTO Inventory (Name, Description, Quantity, Category, Condition, CreatedAt) OUTPUT INSERTED.Id VALUES (@Name, @Description, @Quantity, @Category, @Condition, @CreatedAt)", conn);
                cmd.Parameters.AddWithValue("@Name", item.Name ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", item.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                cmd.Parameters.AddWithValue("@Category", item.Category ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Condition", item.Condition ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                item.Id = (int)cmd.ExecuteScalar();
                item.CreatedAt = DateTime.UtcNow;
            }
            return Ok(item);
        }

        [HttpDelete("delete/{id}")]
        public ActionResult DeleteItem(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                // Handled cascaded deletion to avoid foreign key errors on InventoryTransaction table
                var txCmd = new SqlCommand("DELETE FROM InventoryTransaction WHERE InventoryItemId = @Id", conn);
                txCmd.Parameters.AddWithValue("@Id", id);
                txCmd.ExecuteNonQuery();

                var cmd = new SqlCommand("DELETE FROM Inventory WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return NotFound();
            }
            return Ok();
        }

        public class UpdateConditionRequest
        {
            public string? Condition { get; set; }
        }

        [HttpPut("condition/{id}")]
        public ActionResult UpdateCondition(int id, [FromBody] UpdateConditionRequest req)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("UPDATE Inventory SET Condition = @Condition, UpdatedAt = @UpdatedAt WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Condition", string.IsNullOrWhiteSpace(req.Condition) ? (object)DBNull.Value : req.Condition);
                cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@Id", id);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return NotFound();
            }
            return Ok();
        }

        [HttpGet("transactions")]
        public ActionResult<IEnumerable<InventoryTransaction>> GetTransactions()
        {
            var txs = new List<InventoryTransaction>();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Id, InventoryItemId, IssuedToUserId, QuantityChanged, Comment, Timestamp, PerformedByAdminId FROM InventoryTransaction WHERE QuantityChanged <= 0", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
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
            return Ok(txs);
        }

        [HttpPost("issue")]
        public ActionResult IssueItem([FromBody] IssueRequest req)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                // Check inventory
                var checkCmd = new SqlCommand("SELECT Quantity FROM Inventory WHERE Id = @Id", conn);
                checkCmd.Parameters.AddWithValue("@Id", req.InventoryItemId);
                var qtyObj = checkCmd.ExecuteScalar();
                if (qtyObj == null) return NotFound("Item not found");
                int qty = (int)qtyObj;
                if (qty < req.Quantity) return BadRequest("Not enough items in inventory");
                // Update inventory
                var updateCmd = new SqlCommand("UPDATE Inventory SET Quantity = Quantity - @Qty WHERE Id = @Id", conn);
                updateCmd.Parameters.AddWithValue("@Qty", req.Quantity);
                updateCmd.Parameters.AddWithValue("@Id", req.InventoryItemId);
                updateCmd.ExecuteNonQuery();
                // Add transaction or Update existing Request
                var checkReqCmd = new SqlCommand("SELECT TOP 1 Id FROM InventoryTransaction WHERE InventoryItemId = @ItemId AND IssuedToUserId = @UserId AND QuantityChanged = 0 ORDER BY Timestamp ASC", conn);
                checkReqCmd.Parameters.AddWithValue("@ItemId", req.InventoryItemId);
                checkReqCmd.Parameters.AddWithValue("@UserId", req.IssuedToUserId);
                var reqIdObj = checkReqCmd.ExecuteScalar();

                if (reqIdObj != null)
                {
                    int reqId = (int)reqIdObj;
                    var txCmd = new SqlCommand("UPDATE InventoryTransaction SET QuantityChanged = @QtyChanged, Comment = @Comment, Timestamp = @Timestamp, PerformedByAdminId = @AdminId WHERE Id = @ReqId", conn);
                    txCmd.Parameters.AddWithValue("@QtyChanged", -req.Quantity);
                    txCmd.Parameters.AddWithValue("@Comment", req.Comment ?? (object)DBNull.Value);
                    txCmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                    txCmd.Parameters.AddWithValue("@AdminId", req.PerformedByAdminId);
                    txCmd.Parameters.AddWithValue("@ReqId", reqId);
                    txCmd.ExecuteNonQuery();
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
                    txCmd.ExecuteNonQuery();
                }
            }
            return Ok();
        }

        [HttpPost("request")]
        public ActionResult RequestItem([FromBody] RequestItemRequest req)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                // Check inventory
                var checkCmd = new SqlCommand("SELECT Quantity FROM Inventory WHERE Id = @Id", conn);
                checkCmd.Parameters.AddWithValue("@Id", req.InventoryItemId);
                var qtyObj = checkCmd.ExecuteScalar();
                if (qtyObj == null) return NotFound("Item not found");
                int qty = (int)qtyObj;
                if (qty < req.Quantity) return BadRequest("Not enough items in inventory");
                // Add transaction (request)
                var txCmd = new SqlCommand(
                    "INSERT INTO InventoryTransaction (InventoryItemId, IssuedToUserId, QuantityChanged, Comment, Timestamp, PerformedByAdminId) VALUES (@ItemId, @UserId, @QtyChanged, @Comment, @Timestamp, NULL)", conn);
                txCmd.Parameters.AddWithValue("@ItemId", req.InventoryItemId);
                txCmd.Parameters.AddWithValue("@UserId", req.RequestedByUserId);
                txCmd.Parameters.AddWithValue("@QtyChanged", 0);
                txCmd.Parameters.AddWithValue("@Comment", $"Request [Qty: {req.Quantity}]: " + (req.Comment ?? ""));
                txCmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                txCmd.ExecuteNonQuery();
            }
            return Ok();
        }

        [HttpPost("return/{transactionId}")]
        public ActionResult ReturnTransaction(int transactionId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                // Get transaction
                var txCmd = new SqlCommand("SELECT InventoryItemId, QuantityChanged FROM InventoryTransaction WHERE Id = @Id", conn);
                txCmd.Parameters.AddWithValue("@Id", transactionId);
                using (var reader = txCmd.ExecuteReader())
                {
                    if (!reader.Read()) return NotFound();
                    int itemId = reader.GetInt32(0);
                    int qtyChanged = reader.GetInt32(1);
                    // Only allow return for issued items (negative quantity)
                    if (qtyChanged >= 0) return BadRequest("Not an issued transaction");
                    // Update inventory
                    reader.Close();
                    var updateCmd = new SqlCommand("UPDATE Inventory SET Quantity = Quantity - @Qty WHERE Id = @Id", conn);
                    updateCmd.Parameters.AddWithValue("@Qty", qtyChanged); // qtyChanged is negative, so subtracting negative = add
                    updateCmd.Parameters.AddWithValue("@Id", itemId);
                    updateCmd.ExecuteNonQuery();

                    // Mark transaction as returned
                    var returnLogCmd = new SqlCommand("UPDATE InventoryTransaction SET QuantityChanged = @PosQty, Timestamp = @Timestamp, Comment = CONCAT(Comment, ' (Returned)') WHERE Id = @TxId", conn);
                    returnLogCmd.Parameters.AddWithValue("@PosQty", -qtyChanged);
                    returnLogCmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                    returnLogCmd.Parameters.AddWithValue("@TxId", transactionId);
                    returnLogCmd.ExecuteNonQuery();
                }
            }
            return Ok();
        }

        [HttpGet("returned-transactions")]
        public ActionResult<IEnumerable<InventoryTransaction>> GetReturnedTransactions()
        {
            // For demo, just return all transactions with positive QuantityChanged (i.e., returns)
            var txs = new List<InventoryTransaction>();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Id, InventoryItemId, IssuedToUserId, QuantityChanged, Comment, Timestamp, PerformedByAdminId FROM InventoryTransaction WHERE QuantityChanged > 0", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
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
            return Ok(txs);
        }

        [HttpGet("user/{id}")]
        public ActionResult<User> GetUserById(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Id, Username, IdentityNumber, Email, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return NotFound();
                    var user = new User
                    {
                        Id = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        IdentityNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        PasswordHash = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Role = (UserRole)reader.GetInt32(5),
                        IsApproved = reader.GetBoolean(6),
                        ApprovedByAdminId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        CreatedAt = reader.GetDateTime(8),
                        ApprovedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
                    };
                    return Ok(user);
                }
            }
        }

        [HttpGet("user-by-username/{username}")]
        public ActionResult<User> GetUserByUsername(string username)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Id, Username, IdentityNumber, Email, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users WHERE Username = @Username OR IdentityNumber = @Username", conn);
                cmd.Parameters.AddWithValue("@Username", username);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return NotFound();
                    var user = new User
                    {
                        Id = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        IdentityNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        PasswordHash = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Role = (UserRole)reader.GetInt32(5),
                        IsApproved = reader.GetBoolean(6),
                        ApprovedByAdminId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        CreatedAt = reader.GetDateTime(8),
                        ApprovedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
                    };
                    return Ok(user);
                }
            }
        }

        public class RequestItemRequest
        {
            public int InventoryItemId { get; set; }
            public int RequestedByUserId { get; set; }
            public int Quantity { get; set; }
            public string Comment { get; set; }
        }

        public class IssueRequest
        {
            public int InventoryItemId { get; set; }
            public int IssuedToUserId { get; set; }
            public int Quantity { get; set; }
            public string Comment { get; set; }
            public int PerformedByAdminId { get; set; }
        }
    }
}
